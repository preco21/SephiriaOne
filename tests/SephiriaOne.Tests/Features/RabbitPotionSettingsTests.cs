using SephiriaOne;

internal static class RabbitPotionSettingsTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string scenario)
        { if (!value) throw new Exception(scenario); checks++; }
        RabbitCommand Parse(string text)
        {
            Check(RabbitCommand.Parse(text, out var command, out _) == RabbitParseResult.Valid, text);
            return command;
        }
        var policy = new SessionPolicy();
        Check(!policy.HasChanges && !policy.RabbitPotions.HasChanges, "Defaults are native");
        Check(RabbitPotionSettings.MpCostPerDrink == 10, "MP cost is ten");
        policy.Record(Parse("/one rabbit mp-cost on"));
        Check(policy.RabbitPotions.ConsumeMp && !policy.RabbitPotions.Infinite && !policy.RabbitPotions.Share && !policy.RabbitPotions.SuppressSurvival,
            "MP cost is independent");
        policy.Record(Parse("/one rabbit suppress-survival on"));
        Check(policy.RabbitPotions.ConsumeMp && policy.RabbitPotions.SuppressSurvival, "Survival suppression is independent");
        string balanced = policy.ToPresetText();
        Check(balanced == "SephiriaOne preset v5\nrabbit mp-cost 1\nrabbit suppress-survival 1\n", "New flags write v5");
        Check(SessionPolicy.TryReadPreset(balanced, out var balancedLoad, out _) && balancedLoad.ToPresetText() == balanced,
            "v5 round trips both new flags");
        policy.Record(Parse("/one rabbit reset"));
        Check(!policy.RabbitPotions.HasChanges, "Reset clears new flags");
        policy.Record(Parse("/one rabbit infinite on"));
        Check(policy.RabbitPotions.Infinite && !policy.RabbitPotions.Share && policy.HasChanges, "Infinite is independent");
        policy.Record(Parse(" /ONE RABBIT SHARE ON "));
        Check(policy.RabbitPotions.Infinite && policy.RabbitPotions.Share, "Both can be enabled");
        string saved = policy.ToPresetText();
        Check(saved == "SephiriaOne preset v4\nrabbit infinite 1\nrabbit share 1\n", "Canonical v4 serialization");
        Check(SessionPolicy.TryReadPreset(saved, out var loaded, out _) && loaded.ToPresetText() == saved, "Both flags round trip");
        policy.Record(Parse("/one rabbit infinite off"));
        Check(!policy.RabbitPotions.Infinite && policy.RabbitPotions.Share, "Turning off one preserves the other");
        policy.Record(Parse("/one rabbit reset"));
        Check(!policy.HasChanges && policy.ToPresetText() == "SephiriaOne preset v1\n", "Reset returns to native defaults");
        policy.Record(Parse("/one rabbit share on")); policy.Clear();
        Check(!policy.RabbitPotions.HasChanges, "New session clears rabbit intent");
        foreach (string text in new[] { "", "/mod rabbit infinite on", "/onefoo rabbit infinite on", "/one save", "/one rabbits share on" })
            Check(RabbitCommand.Parse(text, out _, out _) == RabbitParseResult.NotCommand, "Unrelated namespace: " + text);
        foreach (string text in new[] { "/one rabbit", "/one rabbit help" })
            Check(RabbitCommand.Parse(text, out _, out _) == RabbitParseResult.Help, "Rabbit help: " + text);
        Check(RabbitCommand.Parse("/one rabbit status", out _, out _) == RabbitParseResult.Status, "Status is read-only");
        foreach (string text in new[] { "/one rabbit infinite", "/one rabbit infinite 1", "/one rabbit share yes", "/one rabbit share x2", "/one rabbit reset on", "/one rabbit share on extra", "/one rabbit unknown on" })
            Check(RabbitCommand.Parse(text, out _, out _) == RabbitParseResult.Invalid, "Reject malformed command: " + text);
        foreach (string row in new[] { "rabbit unknown 1", "rabbit infinite 2", "rabbit infinite -1", "rabbit share 0.5", "rabbit share on", "rabbit infinite 1\nrabbit infinite 0", "rabbit share 1 extra" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v4\nrabbit share 1\n" + row, out loaded, out _) && !loaded.HasChanges, "Reject preset atomically: " + row);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v4\nrabbit infinite 0\nrabbit share 0\n", out loaded, out _) && !loaded.HasChanges, "Explicit off rows restore defaults");
        foreach (int version in new[] { 1, 2, 3 })
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out loaded, out _) && !loaded.HasChanges, "Old preset still accepted");
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\nrabbit infinite 1\n", out _, out _), "Old schema rejects new rows");
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v4\nstats luck multiplier 3\nresources leaves multiplier 2\nchoices item 2\nrabbit infinite 1\n", out loaded, out _) && loaded.RabbitPotions.Infinite && loaded.DescribeSettings().Count == 4, "v4 preserves all existing families");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v4\nrabbit mp-cost 1\n", out loaded, out _) && !loaded.HasChanges,
            "v4 rejects new options atomically");
        foreach (string row in new[] { "rabbit mp-cost 2", "rabbit mp-cost 01", "rabbit suppress-survival -1", "rabbit suppress-survival 1.0", "rabbit mp-cost 1\nrabbit mp-cost 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v5\nrabbit infinite 1\n" + row, out loaded, out _) && !loaded.HasChanges,
                "v5 rejects malformed row atomically: " + row);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v5\nresources leaves multiplier 2\nrabbit share 1\nrabbit mp-cost 1\n", out loaded, out _) &&
            loaded.RabbitPotions.Share && loaded.RabbitPotions.ConsumeMp && loaded.Resources.HasChanges,
            "v5 accepts previous families and new flags");
        return checks;
    }
}
