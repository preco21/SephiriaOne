using System.Diagnostics;
using SephiriaOne;

internal static class PerformanceProbe
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add, bool checkBudget)
    {
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
            }
        }
        SessionSettings.Stop();
    }

    private static double MeasureSnapshot(bool includeDiagnostics)
    {
        for (int i = 0; i < 100; i++) SessionSettings.ReadSnapshot(includeDiagnostics: includeDiagnostics);
        const int iterations = 1000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long time = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) SessionSettings.ReadSnapshot(includeDiagnostics: includeDiagnostics);
        double us = Stopwatch.GetElapsedTime(time).TotalMicroseconds / iterations;
        double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
        Console.WriteLine($"Five players / {(includeDiagnostics ? "full status" : "non-Status panel")} snapshot: {allocated:F0} bytes/refresh, {us:F2} us/refresh ({iterations} iterations)");
        return allocated;
    }
}
