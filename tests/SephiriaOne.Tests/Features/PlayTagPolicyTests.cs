using SephiriaOne;

internal static class PlayTagPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; }
        const string preset = "SephiriaOne preset v25\ntag duration 45\n";
        Check(SessionPolicy.TryReadPreset(preset, out var policy, out _), "Play Tag duration accepts preset v25");
        Check(policy.HasChanges && policy.ToPresetText() == preset && policy.DescribeSettings().SequenceEqual(new[] { "tag duration 45" }),
            "Nondefault Play Tag fuse is the only persisted Tag state");
        foreach (string value in new[] { "3", "3600" })
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v25\ntag duration " + value + "\n", out _, out _),
                "Play Tag preset accepts duration bound " + value);
        foreach (string body in new[] { "tag duration 2", "tag duration 3601", "tag duration 30.0", "tag duration +30",
            "tag duration 030", "tag seconds 30", "tag duration 3\ntag duration 4", "tag carrier 1", "tag deadline 10",
            "tag allowance 2", "tag duration reset" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v25\n" + body + "\n", out _, out _),
                "Play Tag preset rejects invalid or transient state: " + body);
        for (int version = 1; version <= 24; version++)
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out var old, out _) &&
                !old.DescribeSettings().Any(x => x.StartsWith("tag ")), "Legacy preset preserves default Tag fuse: v" + version);
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\ntag duration 45\n", out _, out _),
                "Legacy preset rejects unsupported Tag duration: v" + version);
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v25\ntag duration 30\n", out var defaults, out _) &&
            !defaults.HasChanges && defaults.ToPresetText() == "SephiriaOne preset v1\n", "Default Tag fuse uses the existing empty preset");
        policy.Clear();
        Check(!policy.HasChanges && !policy.ToPresetText().Contains("tag"), "Clear restores default Tag fuse");
        const string combined = "SephiriaOne preset v25\ndeathmatch duration 45\ntournament duration 50\ntag duration 60\nfriendlyfire refill 25.5\n";
        Check(SessionPolicy.TryReadPreset(combined, out policy, out _) && policy.ToPresetText() == combined,
            "Tag v25 keeps existing match and PvP refill configuration");
        return checks;
    }
}
