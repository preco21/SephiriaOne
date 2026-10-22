using SephiriaOne;

internal static class LowerBoundMathTests
{
    internal static void Run(Action<bool, string> check)
    {
        double Lift(double damage, double hp = 150) => damage + FriendlyFireLowerBound.RequestedBonus(damage, hp, .04);
        check(Lift(1) == 6.5 && Lift(2) == 7 && Lift(12) == 12 && Lift(100) == 100,
            "Weak damage retains ordering while stronger attacks receive no lower-bound bonus");
        check(Lift(2, 300) == 13 && Lift(1, 150) * 2 == Lift(2, 300), "Assistance scales with relative damage/HP, not flat HP values");
        foreach (double bad in new[] { 0d, -1, double.NaN, double.NegativeInfinity, double.PositiveInfinity })
        {
            check(FriendlyFireLowerBound.RequestedBonus(bad, 150, .04) == 0, "No bonus for zero/invalid damage");
            check(FriendlyFireLowerBound.RequestedBonus(1, bad, .04) == 0, "No bonus for invalid victim HP");
            check(FriendlyFireLowerBound.RequestedBonus(1, 150, bad) == 0, "No bonus for invalid reference");
        }
        foreach (double hp in new[] { 1d, 100, 150, 10000, (double)float.MaxValue })
        {
            double previous = 0;
            for (int i = 1; i <= 1000; i++)
            {
                double damage = hp * i / 10000;
                double bonus = FriendlyFireLowerBound.RequestedBonus(damage, hp, .04);
                double lifted = damage + bonus;
                check(lifted >= damage && lifted > previous && bonus >= 0 && bonus <= hp * .04,
                    "Curve is increasing, non-nerfing and has a bounded requested bonus");
                previous = lifted;
            }
        }
        // Even with frequency scaling OFF, splitting cannot multiply funded bonus.
        foreach (int count in new[] { 1, 4, 16, 100, 1000 })
        {
            var budget = new FriendlyFireDamageBudget();
            double extra = 0, original = 0;
            for (int i = 0; i < count; i++)
            {
                double damage = 2d / count;
                extra += budget.Take(FriendlyFireLowerBound.RequestedBonus(damage, 150, .04), 150, 0,
                    refillPerSecond: .08, capacity: .04);
                original += damage;
            }
            check(Math.Abs(original - 2) < 1e-9 && extra <= 6.000000001,
                "Simultaneous split packets share at most one 4-percent assistance burst");
        }
        foreach (double rate in new[] { .5, 2, 10, 100 })
        {
            var budget = new FriendlyFireDamageBudget();
            double extra = 0;
            for (int i = 0; i <= rate * 10; i++)
            {
                double elapsed = i / rate;
                extra += budget.Take(FriendlyFireLowerBound.RequestedBonus(1, 150, .04), 150, elapsed,
                    refillPerSecond: .08, capacity: .04);
                check(extra <= 6 + 12 * elapsed + 1e-8,
                    "Slow and rapid attack streams cannot exceed the shared assistance capacity plus elapsed refill");
            }
        }
    }
}
