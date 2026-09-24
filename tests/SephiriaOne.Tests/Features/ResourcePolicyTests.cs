using SephiriaOne;

internal static class ResourcePolicyTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        ResourceCommand Parse(string input)
        {
            Check(ResourceCommand.Parse("/resources " + input, out var command, out _) == ResourceParseResult.Valid, "Parse " + input);
            return command;
        }
        foreach (var definition in ResourceCatalog.All)
        {
            var policy = new ResourcePolicy();
            var command = Parse(definition.Name + " +6");
            policy.Record(command); policy.Record(Parse(definition.Name + " +2"));
            Check(policy.TryGet(definition.Kind, out var setting) && setting.Mode == ResourceMode.Offset && setting.Amount == 8,
                "Relative resource additions accumulate: " + definition.Name);
            policy.Record(Parse(definition.Name + " set 20"));
            string firstIntent = policy.Intent(definition.Kind);
            policy.Record(Parse(definition.Name + " set 20"));
            Check(firstIntent != policy.Intent(definition.Kind), "Reissued absolute command has a new checkpoint identity");
            policy.Record(Parse(definition.Name + " +2"));
            Check(policy.TryGet(definition.Kind, out setting) && setting.Mode == ResourceMode.Offset && setting.Amount == 2,
                "Add after Set starts native relative mode");
            policy.Record(Parse(definition.Name + " x3"));
            policy.Record(Parse(definition.Name + " x2"));
            Check(policy.TryGet(definition.Kind, out setting) && setting.Amount == 2 && setting.Mode == ResourceMode.Multiplier,
                "Resource factors replace rather than compound");
            policy.Record(Parse(definition.Name + " x1"));
            Check(!policy.HasChanges, "Identity clears resource policy");
            policy.Record(Parse(definition.Name + " x0"));
            Check(policy.HasChanges, "Zero factor remains retained policy");
            policy.Record(Parse(definition.Name + " reset"));
            Check(!policy.HasChanges, "Resource reset clears policy");
            Check(policy.TryGetIntent(definition.Kind, out setting) && setting.Empty && policy.HasIntent,
                "Reset is retained for offline checkpoints within this host session");
            policy.Clear();
            Check(!policy.HasIntent && !policy.TryGetIntent(definition.Kind, out _) && policy.Intent(definition.Kind) == "",
                "Scope clear forgets checkpoint intent and reset history");
        }
        foreach (string input in new[] { "unknown 1", "slots -1.5", "dice add x3", "fruit x-2", "leaves 1000000001", "talents set -2", "slots x1.001" })
            Check(ResourceCommand.Parse("/resources " + input, out _, out _) == ResourceParseResult.Invalid, "Reject bad resource syntax " + input);
        var slots = ResourceCatalog.Get(ResourceKind.Slots);
        var twice = new ResourceSetting(ResourceMode.Multiplier, 2);
        Check(ResourcePlanner.TryPlan(twice, new ResourceSnapshot(slots, 30, 6), false, out var update, out _) &&
            update.Raw == 48 && update.Owned == 24, "Multiplier excludes previous owned slots");
        Check(!ResourcePlanner.TryPlan(new ResourceSetting(ResourceMode.Set, 24), new ResourceSnapshot(slots, 30, 6, minimumSafe: 29),
            false, out _, out _), "An occupied trailing slot blocks shrink regardless of item count");
        Check(!ResourcePlanner.TryPlan(default, new ResourceSnapshot(slots, 30, 6, minimumSafe: 29), true, out _, out _),
            "Reset cannot discard occupied capacity");
        Check(!ResourcePlanner.TryPlan(default, new ResourceSnapshot(slots, 30, 6, busy: true), true, out _, out _),
            "Pending native interaction blocks a reduction");
        var talent = ResourceCatalog.Get(ResourceKind.Talents);
        Check(!ResourcePlanner.TryPlan(new ResourceSetting(ResourceMode.Set, 10), new ResourceSnapshot(talent, 20, 15, minimumSafe: 12),
            false, out _, out _), "Talent budget cannot fall below allocated points");
        var fruit = ResourceCatalog.Get(ResourceKind.Fruit);
        Check(ResourcePlanner.TryPlan(twice, new ResourceSnapshot(fruit, 6, 4, amplifier: 100, displayOffset: 6, minimumSafe: 5),
            false, out update, out _) && update.Target == 20 && update.Raw == 7 && update.Owned == 5,
            "Fruit multiplier includes native default and amplified baseline");
        Check(!ResourcePlanner.TryPlan(new ResourceSetting(ResourceMode.Set, 7), new ResourceSnapshot(fruit, 0, 0, amplifier: 100, displayOffset: 6),
            false, out _, out _), "Unrepresentable fruit target is rejected");
        Check(!ResourcePlanner.TryPlan(new ResourceSetting(ResourceMode.Set, 0), new ResourceSnapshot(fruit, 0, 0, displayOffset: 6, minimumSafe: 1),
            false, out _, out _), "Zero fruit budget rejects committed selection before native zero-grant bug");
        Check(ResourcePlanner.TryPlan(default, new ResourceSnapshot(fruit, 4, 4, amplifier: -100, displayOffset: 6),
            true, out update, out _) && update.Raw == 0 && update.Owned == 0 && update.Target == 6,
            "Fruit reset can remove ownership while native multiplier is zero");
        var leaves = ResourceCatalog.Get(ResourceKind.Leaves);
        Check(ResourcePlanner.TryPlan(twice, new ResourceSnapshot(leaves, 100000000, 0), false, out update, out _) && update.Raw == 200000000,
            "Non-stat resources do not use native stat-amplifier overflow bounds");
        Check(!new ResourceSetting(ResourceMode.Multiplier, 1.5m).TryTarget(3, 0, 100, out _, out _), "Fractional integer target rejected");
        Check(!new ResourceSetting(ResourceMode.Offset, -11).TryTarget(10, 0, 100, out _, out _), "Underflow rejected without clamping");
        Check(!new ResourceSetting(ResourceMode.Multiplier, 10000).TryTarget(int.MaxValue, 0, int.MaxValue, out _, out _), "Overflow rejected");
        var preset = new SessionPolicy();
        preset.Record(new StatCommand(StatCatalog.Find("luck"), StatOperation.Multiply, 3));
        preset.Resources.Record(Parse("dice +5"));
        preset.Resources.Record(Parse("slots x2"));
        preset.Resources.Record(Parse("talents set 20"));
        preset.Resources.Record(Parse("fruit -2"));
        preset.Resources.Record(Parse("leaves x0"));
        string saved = preset.ToPresetText();
        Check(saved.StartsWith("SephiriaOne preset v3\n") && SessionPolicy.TryReadPreset(saved, out var restored, out _) &&
            restored.ToPresetText() == saved, "Resource settings round trip in v3 alongside legacy multipliers");
        foreach (string row in new[] { "resources slots set 0", "resources dice set 1001", "resources fruit offset -2147483648", "resources leaves set 1.5",
            "resources slots multiplier -1", "resources slots multiplier 1.001", "resources slots multiplier 10001", "resources bogus set 1",
            "resources talents set 10\nresources talents offset 5" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v3\n" + row, out var bad, out _) && !bad.HasChanges,
                "Invalid resource preset rejects all rows: " + row);
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v2\nresources dice set 5", out _, out _), "Old schema does not silently accept new resources");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v3\nresources slots offset -97\n", out var wide, out _) &&
            wide.Resources.TryGet(ResourceKind.Slots, out var offset) && offset.TryTarget(120, 6, 96, out int capacity, out _) && capacity == 23,
            "Cumulative reductions from larger native baselines survive preset loading");
        preset.Clear();
        Check(!preset.HasChanges && preset.ToPresetText() == "SephiriaOne preset v1\n", "Full clear removes resource intent");
        return checks;
    }
}
