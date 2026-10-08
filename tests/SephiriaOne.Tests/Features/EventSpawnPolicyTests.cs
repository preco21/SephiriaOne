using SephiriaOne;
internal static class EventSpawnPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.EventSpawns.HasChanges && policy.EventSpawns.Probability(.043) == .043, "Default events native");
        foreach (string text in new[] { "x0", "x0.5", "x2", "x10000", "X3.25" })
        {
            Check(EventSpawnCommand.Parse("/ONE EVENTS chance " + text, out var settings) == EventSpawnParseResult.Valid, "Valid multiplier " + text);
            policy.RecordEventSpawns(settings);
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v16\n"), "Event preset schema v16");
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var restored, out _) && restored.EventSpawns.Equals(settings), "Event preset roundtrip");
        }
        foreach (string value in new[] { "2", "50%", "x-1", "x10001", "x0.001", "xNaN", "x1e2", "x2 extra" })
            Check(EventSpawnCommand.Parse("/one events chance " + value, out _) == EventSpawnParseResult.Invalid, "Invalid multiplier " + value);
        foreach (string command in new[] { "/one events reset", "/one events off", "/one events chance x1" })
            Check(EventSpawnCommand.Parse(command, out var settings) == EventSpawnParseResult.Valid && !settings.HasChanges, "Reset native " + command);
        foreach (string row in new[] { "multiplier -1", "multiplier 10001", "multiplier 1.001", "multiplier 02", "multiplier 2.0", "chance 2", "multiplier 2\nevents multiplier 3" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v16\nevents " + row + "\n", out var invalid, out _) && !invalid.HasChanges, "Atomic invalid event preset " + row);
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v15\nevents multiplier 2\n", out _, out _), "Old schema rejects event row");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v15\ncollin starting 1\n", out var old, out _) && old.CollinStartingArtifact && !old.EventSpawns.HasChanges, "Old presets keep native event odds");
        policy.RecordJarSpawns(new JarSpawnSettings(JarSpawnMode.Multiplier, 3));
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.JarSpawns.Value == 3 && combined.EventSpawns.Multiplier == 3.25m, "Jar and event controls independent");
        policy.Clear(); Check(!policy.HasChanges && !policy.EventSpawns.HasChanges, "Scope reset clears event policy");
        return checks;
    }
}
