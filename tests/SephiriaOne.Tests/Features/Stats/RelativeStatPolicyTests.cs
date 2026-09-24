using SephiriaOne;

internal static class RelativeStatPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }
        // Independent outcome checks across rounded, amplified, decimal-unit,
        // and display-offset stats. Previously applied raw contributions may be
        // stale after a native multiplier change; they must never become baseline.
        foreach (string name in new[] { "luck", "critical", "attackspeed", "defense" })
        foreach (int amplifier in new[] { -50, 0, 25, 100, 200 })
        foreach (int baseline in new[] { 5, 19, 100 })
        {
            StatDefinition stat = StatCatalog.Find(name)!;
            decimal first = stat.Scale == 100 ? 0.25m : 10m;
            decimal second = stat.Scale == 100 ? 0.50m : 5m;
            var policy = new SessionPolicy();
            policy.Record(new StatCommand(stat, StatOperation.Add, first));
            var command = new StatCommand(stat, StatOperation.Add, second);
            var snapshot = new StatSnapshot(stat, baseline + 31, 31, 3, amplifier);
            string label = $"{name}, amp={amplifier}, baseline={baseline}";
            bool live = policy.TryPlanStatCommand(command, new[] { snapshot }, out var updates, out _);
            policy.Record(command);
            var arriving = new SessionPlayerSnapshot(new Dictionary<string, int> { [stat.Key] = baseline },
                new Dictionary<string, int> { [stat.Key] = 3 }, new Dictionary<string, int> { [stat.Key] = amplifier },
                0, 0, 12, null, null, true);
            bool inherited = policy.TryPlan(arriving, out var plan, out _);
            bool maintained = policy.TryPlanRelativeStat(snapshot, out var maintainedValue, out _);
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _), "Round-trip relative policy: " + label);
            bool restored = saved.TryPlan(arriving, out var restoredPlan, out _);
            Check(live == inherited && live == maintained && live == restored, "Identical accept/reject decisions: " + label);
            if (!live) continue;
            var expectedWrite = plan.Stats.Single();
            Check(updates[0].Raw == expectedWrite.Raw && updates[0].Contribution == expectedWrite.Contribution &&
                updates[0].Raw == maintainedValue.Raw && updates[0].Raw == restoredPlan.Stats.Single().Raw,
                "Live, join, maintenance and reload agree: " + label);
            int native = (int)((float)((baseline + 3) * (100 + amplifier)) / 100f);
            int actual = (int)((float)((updates[0].Raw + 3) * (100 + amplifier)) / 100f);
            Check(stat.Display(actual) == stat.Display(native) + first + second &&
                updates[0].Raw - updates[0].Contribution == baseline, "Exact displayed offset and reversible baseline: " + label);
        }

        var luck = StatCatalog.Find("luck")!;
        var batch = new SessionPolicy();
        batch.Record(new StatCommand(luck, StatOperation.Add, 10));
        Check(!batch.TryPlanStatCommand(new StatCommand(luck, StatOperation.Subtract, 20),
            new[] { new StatSnapshot(luck, 110, 10, 0, 0), new StatSnapshot(luck, 15, 10, 0, 0) },
            out var failed, out _) && failed.Length == 0 && batch.ToPresetText().Contains("offset 10"),
            "One invalid character rejects entire command without changing retained policy");
        Check(batch.TryPlanStatCommand(new StatCommand(luck, StatOperation.Subtract, 10),
            new[] { new StatSnapshot(luck, 20010, 10, 0, -100) }, out var canceled, out _) &&
            canceled[0].Raw == 20000 && canceled[0].Contribution == 0,
            "Net-zero restores exact baseline even outside bounds and with non-positive multiplier");
        Check(!batch.TryPlanStatCommand(new StatCommand(luck, StatOperation.Add, 10),
            new[] { new StatSnapshot(luck, int.MinValue, 1, 0, 0) }, out failed, out _) && failed.Length == 0,
            "Baseline subtraction overflow is rejected before any writes");
        return checks;
    }
}
