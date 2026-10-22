using SephiriaOne;

internal static class PvpLowerBoundTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire lower-bound on", out _) == FriendlyFireParseResult.Valid,
            "Lower-bound enable command is accepted");
        var policy = new SessionPolicy();
        var defaults = default(PvpLowerBoundSettings);
        Check(!defaults.Enabled && !defaults.HasChanges && defaults.ReferencePercent == 4 && defaults.RefillPercent == 8 &&
            defaults.ReferenceScale == .04 && defaults.RefillScale == .08, "Default value has disabled normal tuning");
        foreach (string value in new[] { "on", "off", "ON", "reset", "reference 0.1", "reference 25", "reference 4.25", "reference reset", "refill 0.1", "refill 100", "refill 8.75", "refill reset" })
        {
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE LOWER-BOUND " + value, out var cmd) == FriendlyFireParseResult.Valid &&
                !cmd.TouchesMasterToggle && cmd.LowerBound.HasValue, "Isolated valid command " + value);
            policy.Record(cmd);
            Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance, "Command preserves master and balancing toggles");
        }
        foreach (string value in new[] { "", "yes", "1", "on extra", "reset extra", "reference", "reference 0", "reference 25.01", "reference 1.001", "reference 1.000", "reference -1", "reference +1", "reference 1,2", "reference NaN", "reference 1e1", "refill 0.09", "refill 100.01", "refill reset extra", "unknown 1" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire lower-bound " + value, out _) == FriendlyFireParseResult.Invalid, "Reject malformed command " + value);
        Check(!PvpLowerBoundSettings.ValidReferencePercent(.099m) && !PvpLowerBoundSettings.ValidRefillPercent(100.01m) &&
            !new PvpLowerBoundChange(referencePercent: 1.001m).IsValid, "Reject invalid programmatic values");
        policy.Record(new FriendlyFireCommand(false, lowerBound: new(true, 3.25m, 7.75m)));
        foreach (var edit in new[] { new FriendlyFireCommand(true), new(false, balance: true), new(false, percent: .3m),
            new(false, hitFrequency: true), new(false, artifactRate: new(true, 3)), new(false, perfectGuard: new(true, 2)),
            new(false, amplification: new(true, 50, 200)), new(false, suppressThorns: true) })
        {
            var before = policy.FriendlyFire.LowerBound;
            policy.Record(edit);
            Check(policy.FriendlyFire.LowerBound.Equals(before) && policy.FriendlyFire.WithEnabled(false).LowerBound.Equals(before) &&
                policy.FriendlyFire.WithEnabled(true).LowerBound.Equals(before), "Sibling edits and match overlay preserve assistance");
        }
        Check(policy.ToPresetText().StartsWith("SephiriaOne preset v49\n") && SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) &&
            saved.FriendlyFire.Equals(policy.FriendlyFire), "Mixed feature v49 preset round trip");
        policy.Record(new FriendlyFireCommand(false, lowerBound: new(enabled: false)));
        Check(policy.FriendlyFire.LowerBound.ReferencePercent == 3.25m && policy.FriendlyFire.LowerBound.RefillPercent == 7.75m,
            "Off preserves tuning");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Disabled preferences persist");
        FriendlyFireCommand.Parse("/one friendlyfire lower-bound reference reset", out var resetReference); policy.Record(resetReference);
        Check(policy.FriendlyFire.LowerBound.ReferencePercent == 4 && policy.FriendlyFire.LowerBound.RefillPercent == 7.75m && !policy.FriendlyFire.LowerBound.Enabled,
            "Numeric reset changes only reference");
        FriendlyFireCommand.Parse("/one friendlyfire lower-bound reset", out var reset); policy.Record(reset);
        Check(!policy.FriendlyFire.LowerBound.HasChanges && policy.FriendlyFire.Enabled && policy.FriendlyFire.Balance, "Family reset leaves siblings intact");
        for (int version = 1; version <= 48; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.LowerBound.HasChanges,
                "Older schemas default assistance off");
            foreach (string row in new[] { "lower-bound 1", "lower-reference 4", "lower-refill 8" })
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire {row}\n", out _, out _), "Older schemas reject new row");
        }
        foreach (string row in new[] { "lower-bound 2", "lower-bound 1.0", "lower-bound 01", "lower-bound on", "lower-reference 0", "lower-reference 25.01",
            "lower-reference 4.000", "lower-refill 100.01", "lower-refill +8", "lower-bound 1 extra", "lower-bound 1\nfriendlyfire lower-bound 0",
            "lower-reference 4\nfriendlyfire lower-reference 5", "lower-refill 8\nfriendlyfire lower-refill 9", "lower-credit 99" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v49\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Malformed or duplicate rows reject atomically: " + row);
        policy = new SessionPolicy(); policy.Record(new FriendlyFireCommand(false, lowerBound: new(true)));
        Check(policy.ToPresetText() == "SephiriaOne preset v49\nfriendlyfire lower-bound 1\n", "Save only changed preferences");
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.FriendlyFire.HasChanges && !policy.FriendlyFire.LowerBound.HasChanges, "Full reset restores lower-bound defaults");
        return checks;
    }
}
