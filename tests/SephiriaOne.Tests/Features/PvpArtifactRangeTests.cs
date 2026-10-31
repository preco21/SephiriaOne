using SephiriaOne;

internal static class PvpArtifactRangeTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(policy.FriendlyFire.ArtifactRange.Meters == 5 && policy.FriendlyFire.ArtifactRange.Squared == 25 && !policy.HasChanges,
            "Default range is 5 m without an override");
        foreach (string value in new[] { "0.5", "5", "7.25", "50" })
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire artifact-range " + value, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle, "Range edits are independent settings");
            policy.Record(command);
            string preset = policy.ToPresetText();
            Check(SessionPolicy.TryReadPreset(preset, out var loaded, out _) && loaded.FriendlyFire.Equals(policy.FriendlyFire), "Range presets roundtrip");
            Check(value == "5" ? preset == "SephiriaOne preset v1\n" : preset.StartsWith("SephiriaOne preset v58\n") && preset.Contains("friendlyfire artifact-range " + value + "\n"),
                "Range uses a sparse v58 row");
            foreach (var sibling in new FriendlyFireCommand[] { new(true), new(false), new(false, balance: true), new(false, artifactRate: new(true, 3)) })
                Check(policy.FriendlyFire.Apply(sibling).ArtifactRange.Equals(policy.FriendlyFire.ArtifactRange), "Sibling edits preserve range");
            Check(policy.FriendlyFire.WithEnabled(true).ArtifactRange.Equals(policy.FriendlyFire.ArtifactRange), "Mode overlay preserves range");
        }
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE ARTIFACT-RANGE RESET", out var reset) == FriendlyFireParseResult.Valid && reset.IsReset && !reset.TouchesMasterToggle,
            "Range reset is available as independent recovery");
        policy.Record(new FriendlyFireCommand(false, artifactRate: new(true, 3))); policy.Record(reset);
        Check(policy.FriendlyFire.ArtifactRange.Meters == 5 && policy.FriendlyFire.ArtifactRate.Seconds == 3 && policy.FriendlyFire.ArtifactRate.Enabled,
            "Range reset preserves rate controls");
        foreach (string bad in new[] { "", "0", "0.49", "50.01", "-1", "+5", "x2", "NaN", "1.001", "5.000", "on", "5 extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire artifact-range " + bad, out _) == FriendlyFireParseResult.Invalid, "Malformed range rejected: " + bad);
        for (int version = 1; version <= 57; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && old.FriendlyFire.ArtifactRange.Meters == 5,
                "Older presets inherit range default");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire artifact-range 8\n", out var rejected, out _) && !rejected.HasChanges,
                "Older schemas reject new range atomically");
        }
        foreach (string bad in new[] { "0", "51", "05", "5.00", "1.001", "3\nfriendlyfire artifact-range 4" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v58\nfriendlyfire artifact-range " + bad + "\n", out var rejected, out _) && !rejected.HasChanges,
                "Malformed/duplicate range presets reject atomically");
        policy.Clear(); Check(!policy.HasChanges && policy.FriendlyFire.ArtifactRange.Meters == 5, "Full reset restores 5 m");
        return checks;
    }
}
