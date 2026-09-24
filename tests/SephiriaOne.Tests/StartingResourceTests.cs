using System;
using SephiriaOne;

internal static class StartingResourceTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception("Starting resources: " + scenario);
            checks++;
        }
        void Allocation(ResourceMode mode, decimal amount, int seed, int bonus,
            int expectedSeed, int expectedDeparture, bool expectedFallback = false)
        {
            var setting = new ResourceSetting(mode, amount);
            Check(StartingResourcePlan.TrySeed(seed, setting, 1000000000, out int grant, out _), "seed accepted");
            Check(grant == expectedSeed, "safe initial allocation");
            Check(StartingResourcePlan.TryDeparture(seed, grant, bonus, true, setting, 1000000000,
                out int departure, out bool fallback, out _), "departure representable");
            Check(departure == expectedDeparture && fallback == expectedFallback, "exact final allocation/fallback");
            Check(grant >= 0 && grant <= seed && departure >= 0, "grants never reclaim a wallet");
        }
        Allocation(ResourceMode.Set, 20, 200, 70, 20, 0);
        Allocation(ResourceMode.Set, 500, 200, 0, 200, 300);
        Allocation(ResourceMode.Offset, -250, 200, 100, 0, 50);
        Allocation(ResourceMode.Offset, -250, 200, 0, 0, 200, true);
        Allocation(ResourceMode.Offset, 50, 200, 70, 200, 120);
        Allocation(ResourceMode.Multiplier, 1.5m, 100, 2, 100, 53);
        Allocation(ResourceMode.Multiplier, 0.5m, 101, 1, 50, 1);
        Allocation(ResourceMode.Multiplier, 0.5m, 101, 0, 50, 51, true);
        Allocation(ResourceMode.Multiplier, 1.5m, 100, 1, 100, 1, true);
        Allocation(ResourceMode.Set, 0, 200, 70, 0, 0);
        var offset = new ResourceSetting(ResourceMode.Offset, 50);
        Check(!StartingResourcePlan.TrySeed(200, new ResourceSetting(ResourceMode.Set, -1), 1000, out _, out _), "negative set rejected");
        Check(StartingResourcePlan.TryDeparture(200, 150, 100, false, default, 1000000000,
            out int native, out bool usedFallback, out _) && native == 150 && !usedFallback, "absent policy restores native allowance");
        Check(StartingResourcePlan.TryDeparture(200, 200, 999999800, true, offset, 1000000000,
            out int tooLarge, out bool limitFallback, out _) && tooLarge == 999999800 && limitFallback,
            "future bonus exceeding limit falls back without wallet subtraction");
        Check(!StartingResourcePlan.TryDeparture(200, 201, 0, true, offset, 1000,
            out _, out _, out _), "corrupt checkpoint rejected");
        return checks;
    }
}
