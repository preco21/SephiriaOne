using SephiriaOne;

internal static class PvpDotSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire dot burn 0.75", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Independent DoT interval command is accepted without toggling combat");
        var policy = new SessionPolicy();
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
        {
            string key = PvpDotSettings.Key(effect);
            Check(policy.FriendlyFire.DotIntervals.Seconds(effect) == 0.75m && !policy.HasChanges, "Default interval: " + key);
            foreach (string value in new[] { "0.05", "0.1", "0.25", "0.75", "1", "2.25", "10" })
            {
                Check(FriendlyFireCommand.Parse("/one friendlyfire dot " + key + " " + value, out command) == FriendlyFireParseResult.Valid &&
                    !command.TouchesMasterToggle && !command.IsReset, "Accept independent interval: " + key + " " + value);
                policy.Record(command);
                Check(policy.FriendlyFire.DotIntervals.Number(effect) == value && !policy.FriendlyFire.Enabled, "Interval applies without enabling combat");
                foreach (var other in Enum.GetValues<PvpDotEffect>())
                    if (other != effect) Check(policy.FriendlyFire.DotIntervals.Seconds(other) == 0.75m, "Other intervals unchanged");
                Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Interval roundtrip: " + key);
                Check(value == "0.75" || policy.ToPresetText().StartsWith("SephiriaOne preset v28\n"), "Nondefault interval uses v28");
            }
            foreach (string invalid in new[] { "0", "0.04", "10.01", "0.751", "0.750", "-1", "+1", "1e1", "NaN", "Infinity", "1%", "1,5", "x2", "1 extra" })
                Check(FriendlyFireCommand.Parse("/one friendlyfire dot " + key + " " + invalid, out _) == FriendlyFireParseResult.Invalid, "Reject interval: " + invalid);
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE DOT " + key.ToUpperInvariant() + " RESET", out command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle && !command.IsReset, "Per-effect reset preserves match and other settings");
            policy.Record(command); Check(!policy.HasChanges, "Default interval needs no saved row");
        }
        foreach (string invalid in new[] { "/one friendlyfire dot", "/one friendlyfire dot shock 1", "/one friendlyfire dot burn", "/one friendlyfire dot burn 1 2" })
            Check(FriendlyFireCommand.Parse(invalid, out _) == FriendlyFireParseResult.Invalid, "Reject invalid DoT command");
        for (int version = 1; version <= 27; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.DotIntervals.HasChanges,
                "Old presets inherit four 0.75 defaults: " + version);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-burn 1\n", out _, out _), "Old schemas reject interval rows");
        }
        foreach (string row in new[] { "dot-burn 0", "dot-burn 0.751", "dot-burn 1.00", "dot-BURN 1", "dot-shock 1", "dot-burn 1\nfriendlyfire dot-burn 2" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v28\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) && !bad.HasChanges, "Invalid preset is atomic: " + row);
        var settings = new FriendlyFireSettings(true, 0.5m, balance: true, blockLobbyRegen: true, refillPercent: 125, guardMpPercent: 15);
        foreach (var effect in Enum.GetValues<PvpDotEffect>()) settings = settings.Apply(new(false, dot: new(effect, (int)effect + 1)));
        var intervals = settings.DotIntervals;
        foreach (var edit in new FriendlyFireCommand[] { new(false), new(true), new(false, percent: 2), new(false, balance: false),
            new(false, blockLobbyRegen: false), new(false, refillPercent: 50), new(false, guardMpPercent: 20) })
        {
            settings = settings.Apply(edit);
            Check(settings.DotIntervals.Equals(intervals) && settings.WithEnabled(false).DotIntervals.Equals(intervals), "Sibling edit and mode overlay preserve every interval");
        }
        settings = settings.Apply(new(false, dot: new(PvpDotEffect.Burn, 0.75m)));
        Check(settings.DotIntervals.Seconds(PvpDotEffect.PlasmaBurn) == 2 && settings.DotIntervals.Seconds(PvpDotEffect.Poison) == 4, "Single reset preserves other edited intervals");
        settings = settings.Apply(new(false, reset: true));
        Check(!settings.HasChanges && !settings.DotIntervals.HasChanges, "Full reset restores all intervals");
        foreach (decimal bad in new[] { 0m, 0.001m, 10.01m, decimal.MaxValue, decimal.MinValue })
        {
            bool rejected = false;
            try { _ = new PvpDotChange(PvpDotEffect.Burn, bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Range validation cannot overflow: " + bad);
        }
        return checks;
    }
}
