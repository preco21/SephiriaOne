using SephiriaOne;

internal static class PvpThornsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.FriendlyFire.SuppressThorns && !policy.FriendlyFire.HasChanges, "Thorns suppression defaults off");
        foreach (string value in new[] { "ON", "off", "on", "reset" })
        {
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE SUPPRESS-THORNS " + value, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle && command.IsReset == (value == "off" || value == "reset"), "Valid isolated Thorns command: " + value);
            policy.Record(command);
            Check(policy.FriendlyFire.SuppressThorns == value.Equals("on", StringComparison.OrdinalIgnoreCase) &&
                !policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance, "Thorns edit does not implicitly activate PvP or balancing");
        }
        foreach (string value in new[] { "", "yes", "1", "on extra", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire suppress-thorns " + value, out _) == FriendlyFireParseResult.Invalid, "Reject invalid Thorns command");
        policy.Record(new FriendlyFireCommand(false, suppressThorns: true));
        Check(policy.FriendlyFire.HasChanges && policy.ToPresetText() == "SephiriaOne preset v48\nfriendlyfire suppress-thorns 1\n", "Inactive Thorns preference persists in v48");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Thorns preset round trip");
        foreach (var edit in new[] { new FriendlyFireCommand(true), new(false, balance: true), new(false, percent: 35),
            new(false, artifactRate: new(true, 3)), new(false, perfectGuard: new(true, 2)), new(false, dotMaxDurationSeconds: 2) })
        {
            policy.Record(edit);
            Check(policy.FriendlyFire.SuppressThorns && policy.FriendlyFire.WithEnabled(false).SuppressThorns &&
                policy.FriendlyFire.WithEnabled(true).SuppressThorns, "Sibling edits and match overlays preserve Thorns preference");
        }
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Mixed-feature preset round trip");
        for (int version = 1; version <= 47; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.SuppressThorns, "Older presets default to off");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire suppress-thorns 1\n", out _, out _), "Old schemas cannot carry new setting");
        }
        foreach (string row in new[] { "2", "-1", "1.0", "01", "on", "1 extra", "1\nfriendlyfire suppress-thorns 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v48\nfriendlyfire enabled 1\nfriendlyfire suppress-thorns " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Malformed or duplicate Thorns rows reject atomically");
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.FriendlyFire.HasChanges && !policy.FriendlyFire.SuppressThorns, "Full reset restores off");
        return checks;
    }
}
