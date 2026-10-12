using SephiriaOne;

internal static class ScalarStatPlannerTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        { if (!condition) throw new Exception(scenario); checks++; }

        // Both public entry points must keep identical validation and results,
        // including penalties, rounded amplifiers and reset outside normal bounds.
        foreach (var stat in StatCatalog.All)
        foreach (var (raw, owned, bonus, amp) in new[] {
            (20, 10, 5, 0), (20, 10, 5, 100), (3, 0, 0, -50),
            (-20, 5, 0, 0), (20, 10, 5, -100), (int.MinValue, 1, 0, 0),
            (int.MaxValue, 0, 1, 0), (10, int.MaxValue, 0, 0) })
        foreach (var (operation, amount) in new[] {
            (StatOperation.Set, 100m), (StatOperation.Add, 1m), (StatOperation.Subtract, 1m),
            (StatOperation.Multiply, 3m), (StatOperation.Multiply, 1m), (StatOperation.Reset, 0m) })
        {
            var command = new StatCommand(stat, operation, amount);
            var value = new StatSnapshot(stat, raw, owned, bonus, amp);
            bool scalar = StatPlanner.TryPlan(command, value, out var update, out string error);
            bool batch = StatPlanner.TryPlan(command, new[] { value }, out var updates, out string batchError);
            Check(scalar == batch && error == batchError && (scalar
                ? error == "" && updates.Length == 1 && update.Raw == updates[0].Raw && update.Contribution == updates[0].Contribution
                : error.Length > 0 && updates.Length == 0 && update.Raw == 0 && update.Contribution == 0),
                $"Scalar/batch validation parity: {stat.Name}, {operation}, raw={raw}, owned={owned}, bonus={bonus}, amp={amp}");
        }

        var luck = StatCatalog.Find("luck")!;
        var set = new StatCommand(luck, StatOperation.Set, 101);
        var normal = new StatSnapshot(luck, 10, 0, 0, 100);
        Check(!StatPlanner.TryPlan(set, normal, out _, out string failure) &&
            failure == "The exact luck value is not representable with a player's current multiplier. Try another amount. Nobody was changed.",
            "Unrepresentable values retain their exact error rather than rounding");
        Check(!StatPlanner.TryPlan(new StatCommand(luck, StatOperation.Set, 10001), normal, out _, out failure) &&
            failure == "Every player's resulting luck must be 0..10000 points. Nobody was changed.",
            "Range errors retain their exact limits and precedence over representability");
        Check(!StatPlanner.TryPlan(set, default(StatSnapshot), out _, out failure) &&
            failure == "Stat selection mismatch. Nobody was changed.", "Scalar entry rejects missing definition");
        Check(!StatPlanner.TryPlan(set, new StatSnapshot(StatCatalog.Find("defense")!, 10, 0, 0, 0), out _, out failure) &&
            failure == "Stat selection mismatch. Nobody was changed.", "Scalar entry rejects mismatched definition");
        Check(!StatPlanner.TryPlan(new StatCommand(null, StatOperation.Add, 1), normal, out _, out failure) &&
            failure == "Stat selection mismatch. Nobody was changed.", "Null selection only permits reset-all");
        Check(StatPlanner.TryPlan(new StatCommand(null, StatOperation.Reset, 0),
            new StatSnapshot(luck, -15, 5, int.MaxValue, int.MaxValue), out var reset, out failure) &&
            reset.Raw == -20 && reset.Contribution == 0 && failure == "", "Scalar reset-all restores exact penalized baseline");
        Check(!StatPlanner.TryPlan(new StatCommand(luck, StatOperation.Multiply, -1), normal, out _, out failure) &&
            failure == RelativeMultiplier.Usage, "Invalid multiplier retains syntax guidance");
        return checks;
    }
}
