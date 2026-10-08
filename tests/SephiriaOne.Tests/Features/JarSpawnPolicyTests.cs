using SephiriaOne;

internal static class JarSpawnPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value,string name) { if (!value) throw new Exception(name); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.JarSpawns.HasChanges && policy.JarSpawns.Probability(.18f) == .18f, "Default Jar chance is native");
        foreach (string text in new[] { "0", "0.25", "18", "50", "100", "x0", "x0.5", "x2", "x10000" })
        {
            Check(JarSpawnCommand.Parse("/one jars chance " + text,out var settings) == JarSpawnParseResult.Valid, "Valid Jar rate " + text);
            policy.RecordJarSpawns(settings);
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v13\n"), "Jar override uses v13");
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(),out var restored,out _) && restored.JarSpawns.Equals(settings), "Jar preset roundtrip " + text);
        }
        foreach (string invalid in new[] { "-1", "100.01", "NaN", "Infinity", "1e2", "1,5", "0.001", "x-1", "x10001", "x1.001", "50 extra" })
            Check(JarSpawnCommand.Parse("/one jars chance " + invalid,out _) == JarSpawnParseResult.Invalid, "Invalid Jar rate " + invalid);
        foreach (string text in new[] { "/one jars reset", "/one jars off", "/one jars chance x1" })
            Check(JarSpawnCommand.Parse(text,out var settings) == JarSpawnParseResult.Valid && !settings.HasChanges, "Native reset: " + text);
        var multiplier = new JarSpawnSettings(JarSpawnMode.Multiplier,2);
        Check(multiplier.Probability(.18f) == .36f && multiplier.Probability(.75f) == 1f, "Per-location multiplication and saturation");
        foreach (string row in new[] { "chance 101", "chance -1", "chance 1.001", "chance 050", "chance 1.0", "multiplier 10001", "multiplier -1", "chance 50\njars multiplier 2", "enabled 1" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v13\njars " + row + "\n",out var invalid,out _) && !invalid.HasChanges, "Malformed Jar preset is atomic: " + row);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v12\nfriendlyfire enabled 1\n",out var old,out _) && !old.JarSpawns.HasChanges && old.FriendlyFire.Enabled, "Old preset preserves combat and defaults Jars to native");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v12\njars chance 50\n",out _,out _), "Old schema cannot contain Jar override");
        policy.Clear(); Check(!policy.JarSpawns.HasChanges && !policy.HasChanges, "Shared scope reset clears Jar override");
        return checks;
    }
}
