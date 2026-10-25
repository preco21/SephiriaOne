using System.Diagnostics;
using SephiriaOne;

internal static class ParticipantPerformanceProbe
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add)
    {
        const int players = 9, iterations = 400_000;
        var avatars = new PlayerAvatar[players];
        var receipts = new FriendlyFireParticipants.Receipt[players];
        avatars[0] = start().PlayerAvatar;
        for (uint i = 1; i < players; i++) avatars[i] = add(i + 1).PlayerAvatar;
        for (int i = 0; i < players; i++) receipts[i] = FriendlyFireParticipants.Capture(avatars[i]) ??
            throw new Exception("Performance fixture could not capture its participant.");
        if (!FriendlyFireParticipants.SetAll(receipts)) throw new Exception("Performance fixture could not select its roster.");

        foreach (bool includes in new[] { false, true })
        {
            bool Read(int i) => includes ? FriendlyFireParticipants.Includes(avatars[i % players]) :
                ReferenceEquals(receipts[i % players], FriendlyFireParticipants.Capture(avatars[i % players]));
            for (int i = 0; i < 40_000; i++)
                if (!Read(i)) throw new Exception("Performance fixture lost its participant.");
            for (int trial = 0; trial < 3; trial++)
            {
                int accepted = 0;
                long before = GC.GetAllocatedBytesForCurrentThread();
                long time = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++) if (Read(i)) accepted++;
                long elapsed = Stopwatch.GetTimestamp() - time;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (accepted != iterations || allocated != 0)
                    throw new Exception("Stable participant reads must succeed without allocations.");
                double ns = elapsed * 1e9 / Stopwatch.Frequency / iterations;
                Console.WriteLine($"Nine selected players / {(includes ? "Includes" : "Capture")}, trial {trial + 1}: {ns:F1} ns/read, {allocated} bytes / {iterations} reads");
            }
        }
        SessionSettings.Stop();
    }
}
