using SephiriaOne;

internal static class PvpArtifactRateTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire artifact-rate on", out var command) == FriendlyFireParseResult.Valid,
            "Autonomous artifact rate command is supported");
        var defaults = default(PvpArtifactRateSettings);
        Check(!defaults.Enabled && defaults.Seconds == 2 && defaults.Interval == 2f && !defaults.HasChanges &&
            defaults.Equals(new PvpArtifactRateSettings(false, 2)), "Default policy is off / 2s");
        var policy = new SessionPolicy();
        void Edit(string tail)
        {
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE ARTIFACT-RATE " + tail, out var edit) == FriendlyFireParseResult.Valid &&
                !edit.TouchesMasterToggle, "Artifact edit never touches match master: " + tail);
            policy.Record(edit);
        }
        Edit("interval 3.25");
        Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance && !policy.FriendlyFire.ArtifactRate.Enabled &&
            policy.ToPresetText() == "SephiriaOne preset v47\nfriendlyfire artifact-interval 3.25\n", "Inactive interval persists");
        Edit("on");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Policy round trip");
        Edit("off"); Check(policy.FriendlyFire.ArtifactRate.Seconds == 3.25m, "Off preserves custom interval");
        foreach (string text in new[] { "0.05", "2", "10", "60", "59.99" }) Edit("interval " + text);
        foreach (string text in new[] { "", "0", "-2", "+2", "60.01", "0.049", "2.000", "NaN", "Infinity", "2,5", "2e0", "x2", "2%", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire artifact-rate interval " + text, out _) == FriendlyFireParseResult.Invalid, "Reject invalid interval: " + text);
        foreach (string text in new[] { "on extra", "off extra", "reset extra", "interval", "2" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire artifact-rate " + text, out _) == FriendlyFireParseResult.Invalid, "Reject invalid action");
        foreach (decimal bad in new[] { 0m, 60.01m, 2.001m, decimal.MaxValue })
        {
            bool throws = false; try { _ = new PvpArtifactRateSettings(true, bad); } catch (ArgumentOutOfRangeException) { throws = true; }
            Check(throws && !new PvpArtifactRateChange(seconds: bad).IsValid, "Typed commands reject invalid policy");
        }
        Edit("on"); Edit("interval reset");
        Check(policy.FriendlyFire.ArtifactRate.Enabled && policy.FriendlyFire.ArtifactRate.Seconds == 2, "Interval reset retains toggle");
        policy.Record(new FriendlyFireCommand(true, balance: true, perfectGuard: new(true, 3), dotMaxDurationSeconds: 1.5m));
        var before = policy.FriendlyFire;
        Edit("reset");
        Check(!policy.FriendlyFire.ArtifactRate.HasChanges && policy.FriendlyFire.PerfectGuard.Equals(before.PerfectGuard) &&
            policy.FriendlyFire.Balance && policy.FriendlyFire.DotMaxDurationSeconds == 1.5m, "Family reset preserves other controls");
        Edit("interval 3.25"); Edit("on");
        Check(policy.FriendlyFire.WithEnabled(true).ArtifactRate.Equals(policy.FriendlyFire.ArtifactRate), "Mode overlay preserves policy");
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Check(policy.FriendlyFire.ArtifactRate.Number == "3.25" && SessionPolicy.TryReadPreset(policy.ToPresetText(), out saved, out _) &&
                saved.FriendlyFire.Equals(policy.FriendlyFire), "Invariant storage and display");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        for (int version = 1; version <= 47; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.ArtifactRate.HasChanges, "Backward defaults");
            if (version < 47) Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire artifact-interval 2\n", out _, out _), "Old schema rejects new row");
        }
        foreach (string row in new[] { "artifact-rate 2", "artifact-rate 1.0", "artifact-interval 0", "artifact-interval 61", "artifact-interval 2.0",
            "artifact-interval x2", "artifact-interval 2\nfriendlyfire artifact-interval 3", "artifact-rate 1\nfriendlyfire artifact-rate 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v47\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) && !bad.HasChanges, "Malformed rows reject atomically");
        policy.Record(new FriendlyFireCommand(false, reset: true)); Check(!policy.HasChanges, "Full reset includes artifact controls");
        return checks;
    }
}
