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
        foreach (bool active in new[] { false, true })
        {
            start();
            for (uint i = 2; i <= 4; i++) add(i, (int)i * 3, (int)i, 0);
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                for (int i = 0; i < 100; i++) spawner.PlayerAvatar.customStats["native-fixture-" + i] = i;
                for (int i = 0; i < 24; i++)
                    spawner.PlayerAvatar.Inventory.inventoryMatrix[new ItemPosition { x = (sbyte)(i % 6), y = (sbyte)(i / 6) }] = new();
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
            Console.WriteLine($"Four players / {(active ? "all active" : "no settings")}: {allocated:F0} bytes/tick, {us:F2} us/tick ({iterations} iterations)");
            if (checkBudget && allocated > 1024)
                throw new Exception("Unchanged synchronization exceeds the 1 KiB/tick allocation budget.");
        }
        SessionSettings.Stop();
    }
}
