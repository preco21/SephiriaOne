using System.Diagnostics;
using SephiriaOne;

internal static class PerformanceProbe
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add, bool checkBudget)
    {
        MeasureStatPlanning(checkBudget);
        MeasureResourceTarget(checkBudget);
        void Command(string text)
        {
            var result = SettingsActions.Execute(text);
            if (!result.Success) throw new Exception(string.Join("; ", result.Messages));
        }
        foreach (var (active, populated) in new[] { (false, false), (true, false), (true, true) })
        {
            start();
            for (uint i = 2; i <= 5; i++) add(i, (int)i * 3, (int)i, 0);
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                for (int i = 0; i < 100; i++) spawner.PlayerAvatar.customStats["native-fixture-" + i] = i;
                int slots = populated ? 90 : 24;
                spawner.PlayerAvatar.Inventory.CurrentInventoryStorage = (short)slots;
                for (int i = 0; i < slots; i++)
                    spawner.PlayerAvatar.Inventory.inventoryMatrix[new ItemPosition { x = (sbyte)(i % 6), y = (sbyte)(i / 6) }] = new();
                if (populated)
                {
                    spawner.PlayerAvatar.maxPassivePoint = 100;
                    for (ulong i = 0; i < 40; i++) spawner.PlayerAvatar.passiveStats[i] = 1;
                    for (int i = 0; i < 30; i++)
                        spawner.PlayerAvatar.Inventory.mysticPositions.Add(new ItemPosition { x = (sbyte)(i % 6), y = (sbyte)(i / 6) });
                }
            }
            if (active)
            {
                foreach (var stat in StatCatalog.All) Command("/stats " + stat.Name + " +1");
                Command("/fountain x2"); Command("/choices all 5");
                Command("/resources slots +6"); Command("/resources talents +5"); Command("/resources fruit +2");
            }
            for (int i = 0; i < 2000; i++) SessionSettings.Synchronize();
            const int iterations = 10000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            long time = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++) SessionSettings.Synchronize();
            double us = Stopwatch.GetElapsedTime(time).TotalMicroseconds / iterations;
            double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
            Console.WriteLine($"Five players / {(populated ? "all active, populated collections" : active ? "all active" : "no settings")}: {allocated:F0} bytes/tick, {us:F2} us/tick ({iterations} iterations)");
            if (checkBudget && allocated > 1024)
                throw new Exception("Unchanged synchronization exceeds the 1 KiB/tick allocation budget.");
            if (active && !populated)
            {
                double full = MeasureSnapshot(true), compact = MeasureSnapshot(false);
                if (checkBudget && compact >= full * 0.75)
                    throw new Exception("Non-Status snapshots must avoid at least 25% of full diagnostic allocations.");
                // Fixed five-player fixture: retain headroom without allowing the
                // capture path to allocate a second copy of each player dictionary.
                // The all-sections compatibility snapshot includes six more
                // stat values and active-preset summaries since 0.51.0 (33 stats).
                // Actual tab-specific and steady-state budgets remain unchanged.
                if (checkBudget && compact > 28 * 1024)
                    throw new Exception("Five-player non-Status snapshots exceed the 28 KiB allocation budget.");
                if (checkBudget && full > 160 * 1024)
                    throw new Exception("Five-player full reports exceed the 160 KiB allocation budget.");
                foreach (var content in new[] { SnapshotContent.None, SnapshotContent.Stats, SnapshotContent.Choices,
                    SnapshotContent.Resources, SnapshotContent.Presets, SnapshotContent.Participants })
                {
                    double selected = MeasureSnapshot(false, content);
                    int budget = content == SnapshotContent.Resources ? 16 * 1024 :
                        content == SnapshotContent.Stats ? 12 * 1024 : content == SnapshotContent.Presets ? 10 * 1024 : 4 * 1024;
                    if (checkBudget && selected > budget)
                        throw new Exception($"Five-player {content} snapshot exceeds its {budget} B allocation budget.");
                }
                foreach (var stat in StatCatalog.All) Command("/stats " + stat.Name + " x2");
                MeasureStatChanges(checkBudget);
            }
        }
        SessionSettings.Stop();
    }

    private static void MeasureStatPlanning(bool checkBudget)
    {
        var stat = StatCatalog.Find("luck");
        var policy = new SessionPolicy();
        policy.Record(new StatCommand(stat, StatOperation.Multiply, 3));
        void Plan(int i)
        {
            var value = new StatSnapshot(stat, 20, 10, i % 2 + 1, i % 2 * 100);
            if (!policy.TryPlanRelativeStat(value, out var update, out var error) || update.UsesNativeFallback ||
                update.Raw != 30 + 2 * value.Bonus || update.Raw - update.Contribution != 10)
                throw new Exception("Relative planning changed native baseline or target: " + error);
        }
        for (int i = 0; i < 2000; i++) Plan(i);
        const int iterations = 10000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long time = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) Plan(i);
        double us = Stopwatch.GetElapsedTime(time).TotalMicroseconds / iterations;
        double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
        Console.WriteLine($"Successful relative stat planning: {allocated:F0} bytes/plan, {us:F2} us/plan ({iterations} iterations)");
        if (checkBudget && allocated != 0)
            throw new Exception("Successful relative stat planning must not allocate temporary arrays or error messages.");
    }

    private static void MeasureResourceTarget(bool checkBudget)
    {
        var settings = new[] { new ResourceSetting(ResourceMode.Set, 30), new ResourceSetting(ResourceMode.Offset, 10),
            new ResourceSetting(ResourceMode.Multiplier, 1.5m) };
        void Plan(int i)
        {
            if (!settings[i % settings.Length].TryTarget(20, 0, 100, out int target, out string error) || target != 30 || error != "")
                throw new Exception("Resource target planning changed its result: " + error);
        }
        for (int i = 0; i < 2000; i++) Plan(i);
        const int iterations = 10000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long time = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) Plan(i);
        double us = Stopwatch.GetElapsedTime(time).TotalMicroseconds / iterations;
        double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
        Console.WriteLine($"Successful resource target planning: {allocated:F0} bytes/plan, {us:F2} us/plan ({iterations} iterations)");
        if (checkBudget && allocated != 0)
            throw new Exception("Successful resource target planning must not format unused errors.");
    }

    private static void MeasureStatChanges(bool checkBudget)
    {
        void Change(int i)
        {
            foreach (var spawner in PlayerSpawner.MultiplayerList)
                foreach (var stat in StatCatalog.All)
                {
                    spawner.PlayerAvatar.calculatedBonusStats[stat.Key] = i % 2 + 1;
                    spawner.PlayerAvatar.customStatsAmp[stat.Key] = i % 2 * 100;
                }
            if (!SessionSettings.EnsureFresh()) throw new Exception("Changed native stats did not reconcile.");
        }
        for (int i = 0; i < 100; i++) Change(i);
        const int iterations = 1000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long time = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) Change(i);
        double us = Stopwatch.GetElapsedTime(time).TotalMicroseconds / iterations;
        double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
        Console.WriteLine($"Five players / all {StatCatalog.All.Count} stat bonuses and amplifiers changed: {allocated:F0} bytes/reconciliation, {us:F2} us/reconciliation ({iterations} iterations)");
        // This deliberately heavy workload retains full guarded write journals.
        // Bound allocation growth without imposing a machine-dependent time limit.
        if (checkBudget && allocated > 1700 * 1024)
            throw new Exception("Five-player native stat changes exceed the 1700 KiB reconciliation allocation budget.");
    }

    private static double MeasureSnapshot(bool includeDiagnostics, SnapshotContent? content = null)
    {
        SettingsSnapshot Capture() => content.HasValue ? SessionSettings.ReadSnapshot(content.Value) :
            SessionSettings.ReadSnapshot(includeDiagnostics: includeDiagnostics);
        for (int i = 0; i < 100; i++) Capture();
        const int iterations = 1000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long time = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) Capture();
        double us = Stopwatch.GetElapsedTime(time).TotalMicroseconds / iterations;
        double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
        Console.WriteLine($"Five players / {(content.HasValue ? content.ToString() : includeDiagnostics ? "full status" : "all values, no diagnostics")} snapshot: {allocated:F0} bytes/refresh, {us:F2} us/refresh ({iterations} iterations)");
        return allocated;
    }
}
