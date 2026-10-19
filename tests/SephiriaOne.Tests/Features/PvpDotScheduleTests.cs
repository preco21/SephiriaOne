using SephiriaOne;

internal static class PvpDotScheduleTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        for (int i = 0; i < PvpDotSettings.Count; i++)
        {
            var effect = (PvpDotEffect)i;
            string key = PvpDotSettings.Key(effect), prefix = "/one friendlyfire dot " + key + " preserve ";
            if (effect == PvpDotEffect.DarkCloud)
            {
                foreach (string action in new[] { "on", "off", "reset" })
                    Check(FriendlyFireCommand.Parse(prefix + action, out _) == FriendlyFireParseResult.Invalid,
                        "Dark Cloud's independent strike clock cannot be disabled by a debuff option");
                continue;
            }
            var policy = new SessionPolicy();
            Check(policy.FriendlyFire.DotIntervals.PreserveSchedule(effect) && !policy.HasChanges, "Default-on protection has no saved override");
            Check(FriendlyFireCommand.Parse(prefix + "OFF", out var command) == FriendlyFireParseResult.Valid && command.IsReset &&
                !command.TouchesMasterToggle, "Disabling protection is a recovery action, not a combat toggle");
            policy.Record(command);
            Check(!policy.FriendlyFire.DotIntervals.PreserveSchedule(effect) && policy.HasChanges && !policy.FriendlyFire.Enabled,
                "One protection can be disabled without enabling combat");
            string text = policy.ToPresetText();
            Check(text == "SephiriaOne preset v35\nfriendlyfire dot-preserve-" + key + " 0\n", "New schema saves only nondefault protection");
            Check(SessionPolicy.TryReadPreset(text, out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Protection roundtrip");
            for (int j = 0; j < (int)PvpDotEffect.DarkCloud; j++)
                if (j != i) Check(policy.FriendlyFire.DotIntervals.PreserveSchedule((PvpDotEffect)j), "Sibling protection remains enabled");
            policy.Record(new FriendlyFireCommand(false, dot: new(effect, 1.25m)));
            Check(!policy.FriendlyFire.DotIntervals.PreserveSchedule(effect), "Interval edits preserve the independent toggle");
            policy.Record(new FriendlyFireCommand(true)); policy.Record(new FriendlyFireCommand(false, balance: true)); policy.Record(new FriendlyFireCommand(false, percent: 0.05m));
            Check(!policy.FriendlyFire.DotIntervals.PreserveSchedule(effect) &&
                !policy.FriendlyFire.WithEnabled(false).DotIntervals.PreserveSchedule(effect), "Modes and sibling settings preserve protection");
            foreach (string action in new[] { "on", "reset" })
            {
                Check(FriendlyFireCommand.Parse(prefix + action, out command) == FriendlyFireParseResult.Valid && !command.IsReset &&
                    !command.TouchesMasterToggle, "Enabling/defaulting protection requires available hooks without stopping a match");
                policy.Record(command);
                Check(policy.FriendlyFire.DotIntervals.PreserveSchedule(effect) && policy.FriendlyFire.DotIntervals.Seconds(effect) == 1.25m,
                    "Protection reset preserves configured interval");
            }
            foreach (string bad in new[] { "", "1", "true", "on extra", "off off" })
                Check(FriendlyFireCommand.Parse(prefix + bad, out _) == FriendlyFireParseResult.Invalid, "Reject malformed protection command");
            for (int version = 1; version <= 34; version++)
            {
                Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) &&
                    old.FriendlyFire.DotIntervals.PreserveSchedule(effect), "Legacy preset inherits protection");
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-preserve-{key} 0\n", out var bad, out _) &&
                    !bad.HasChanges, "Old schema rejects new rows atomically");
            }
        }
        foreach (string row in new[] { "dot-preserve-burn -1", "dot-preserve-poison 2", "dot-preserve-wound 0.0", "dot-preserve-shock 0",
            "dot-preserve-BURN 0", "dot-preserve-dark-cloud 0", "dot-preserve-missing 0", "dot-preserve-burn 0\nfriendlyfire dot-preserve-burn 1" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v35\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) &&
                !bad.HasChanges, "Invalid protection preset is atomic: " + row);
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE DOT SHOCK PRESERVE OFF", out var alias) == FriendlyFireParseResult.Valid &&
            alias.DotSchedule.GetValueOrDefault().Effect == PvpDotEffect.Lightning, "Shock alias resolves shared Lightning toggle");
        var combined = new SessionPolicy();
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
        {
            combined.Record(new FriendlyFireCommand(false, dot: new(effect, (int)effect + 1)));
            if (PvpDotSettings.SupportsSchedule(effect)) combined.Record(new FriendlyFireCommand(false, dotSchedule: new(effect, false)));
        }
        combined.Record(new FriendlyFireCommand(true, percent: 0.05m, balance: true, hitFrequency: true, refillPercent: 125, guardMpPercent: 15));
        Check(SessionPolicy.TryReadPreset(combined.ToPresetText(), out var roundtrip, out _) &&
            roundtrip.FriendlyFire.Equals(combined.FriendlyFire), "v35 carries all intervals, toggles and sibling combat settings");
        combined.Record(new FriendlyFireCommand(false, reset: true));
        Check(!combined.HasChanges && !combined.FriendlyFire.DotIntervals.HasChanges, "Full reset restores all default-on protections");
        return checks;
    }
}
