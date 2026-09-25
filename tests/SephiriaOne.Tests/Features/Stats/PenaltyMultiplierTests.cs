using SephiriaOne;

internal static class PenaltyMultiplierTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        foreach (var (name, raw, owned, bonus, amp, factor) in new[]
        {
            ("cooldown", -30, 20, 0, 0, 3m),
            ("cooldown", -100, 0, 0, 0, 3m),
            ("cooldown", -150, 0, 0, 0, 3m),
            ("defense", -20, 0, 0, 0, 3m),
            ("cooldown", 20, 20, -50, 50, 3m),
            ("luck", 6010, 10, 0, 0, 2m),
            ("luck", 13, 10, 0, 0, 1.5m),
            ("luck", 11, 10, 0, 100, 1.5m),
            ("luck", 15, 10, 0, -100, 3m),
            ("luck", 15, 10, 0, -200, 3m),
            ("luck", int.MaxValue, 10, 100, 0, 3m)
        })
        {
            var stat = StatCatalog.Find(name)!;
            var value = new StatSnapshot(stat, raw, owned, bonus, amp);
            var command = new StatCommand(stat, StatOperation.Multiply, factor);
            var policy = new SessionPolicy();
            Check(policy.TryPlanStatCommand(command, new[] { value }, out var writes, out _) &&
                writes[0].Raw == raw - owned && writes[0].Contribution == 0,
                "Invalid multiplier preserves exact native raw and removes only owned adjustment: " + value);
            policy.Record(command);
            Check(policy.TryPlanRelativeStat(value, out var maintained, out _) && maintained.Raw == raw - owned && maintained.Contribution == 0,
                "Maintenance uses the same native fallback");
            var snapshot = new SessionPlayerSnapshot(new Dictionary<string, int> { [stat.Key] = raw, [stat.Marker] = owned },
                new Dictionary<string, int> { [stat.Key] = bonus }, new Dictionary<string, int> { [stat.Key] = amp }, 4, 0, 12, null, null, true);
            Check(policy.TryPlan(snapshot, out var joined, out _) && joined.Stats.Single().Raw == raw - owned && joined.Stats.Single().Contribution == 0,
                "Join fallback does not reject enrollment");
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var loaded, out _) && loaded.TryPlan(snapshot, out var saved, out _) &&
                saved.Stats.Single().Raw == raw - owned && saved.Stats.Single().Contribution == 0,
                "Saved preset uses the same fallback");
        }
        var cooldown = StatCatalog.Find("cooldown")!;
        var mixed = new SessionPolicy();
        var timesThree = new StatCommand(cooldown, StatOperation.Multiply, 3);
        Check(mixed.TryPlanStatCommand(timesThree, new[] { new StatSnapshot(cooldown, -50, 0, 0, 0),
            new StatSnapshot(cooldown, 10, 0, 0, 0) }, out var results, out _) && results[0].Raw == -50 && results[1].Raw == 30,
            "One negative costume does not block another character's multiplier");
        Check(!mixed.TryPlanStatCommand(timesThree, new[] { new StatSnapshot(cooldown, int.MinValue, 1, 0, 0) }, out results, out _) && results.Length == 0,
            "Unrecoverable tracked baseline stays rejected");
        Check(!mixed.TryPlanStatCommand(new StatCommand(cooldown, StatOperation.Multiply, -1),
            new[] { new StatSnapshot(cooldown, -50, 0, 0, 0) }, out _, out _), "Malformed factor is never a successful fallback");
        foreach (var (name, raw, expected) in new[] { ("attackspeed", -50, 50), ("criticaldamage", -20, 40) })
        {
            var stat = StatCatalog.Find(name)!;
            Check(mixed.TryPlanStatCommand(new StatCommand(stat, StatOperation.Multiply, 3),
                new[] { new StatSnapshot(stat, raw, 0, 0, 0) }, out results, out _) &&
                results[0].Raw == expected && !results[0].UsesNativeFallback,
                "Negative raw with valid displayed target still multiplies: " + name);
        }
        return checks;
    }
}
