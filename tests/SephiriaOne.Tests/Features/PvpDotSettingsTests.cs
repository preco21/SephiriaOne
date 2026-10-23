using SephiriaOne;

internal static class PvpDotSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire dot burn preserve off", out var schedule) == FriendlyFireParseResult.Valid &&
            !schedule.TouchesMasterToggle, "Per-effect schedule control must not toggle friendly fire");
        foreach (string key in new[] { "wound", "lightning", "plasma-shock", "freeze", "chim", "dark-cloud" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire dot " + key + " 1.5", out _) == FriendlyFireParseResult.Valid,
                "Missing effect control is exposed: " + key);
        decimal ExpectedDefault(PvpDotEffect effect) => effect switch { PvpDotEffect.Burn => 0.9m, PvpDotEffect.PlasmaBurn or PvpDotEffect.PlasmaShock => 3m, PvpDotEffect.Wound => 1.33m, PvpDotEffect.Lightning => 0.5m, PvpDotEffect.Freeze or PvpDotEffect.Chim => 0.25m, PvpDotEffect.DarkCloud => 2m, _ => 0.75m };
        Check(FriendlyFireCommand.Parse("/one friendlyfire dot burn 0.75", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Independent DoT interval command is accepted without toggling combat");
        var policy = new SessionPolicy();
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
        {
            string key = PvpDotSettings.Key(effect);
            Check(policy.FriendlyFire.DotIntervals.Seconds(effect) == ExpectedDefault(effect) && !policy.HasChanges, "Default interval: " + key);
            foreach (string value in new[] { "0.05", "0.1", "0.25", "0.75", "0.9", "1", "2.25", "10" })
            {
                Check(FriendlyFireCommand.Parse("/one friendlyfire dot " + key + " " + value, out command) == FriendlyFireParseResult.Valid &&
                    !command.TouchesMasterToggle && !command.IsReset, "Accept independent interval: " + key + " " + value);
                policy.Record(command);
                Check(policy.FriendlyFire.DotIntervals.Number(effect) == value && !policy.FriendlyFire.Enabled, "Interval applies without enabling combat");
                foreach (var other in Enum.GetValues<PvpDotEffect>())
                    if (other != effect) Check(policy.FriendlyFire.DotIntervals.Seconds(other) == ExpectedDefault(other), "Other intervals unchanged");
                Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Interval roundtrip: " + key);
                Check(policy.FriendlyFire.DotIntervals.Seconds(effect) == ExpectedDefault(effect)
                    ? policy.ToPresetText() == "SephiriaOne preset v1\n"
                    : policy.ToPresetText() == "SephiriaOne preset v" + (effect is PvpDotEffect.Lightning or PvpDotEffect.PlasmaShock ? "37" : (int)effect < 4 ? "28" : "34") + "\nfriendlyfire dot-" + key + " " + value + "\n",
                    "Only each effect's nondefault interval is serialized: " + key);
            }
            foreach (string invalid in new[] { "0", "0.04", "10.01", "0.751", "0.750", "-1", "+1", "1e1", "NaN", "Infinity", "1%", "1,5", "x2", "1 extra" })
                Check(FriendlyFireCommand.Parse("/one friendlyfire dot " + key + " " + invalid, out _) == FriendlyFireParseResult.Invalid, "Reject interval: " + invalid);
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE DOT " + key.ToUpperInvariant() + " RESET", out command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle && !command.IsReset, "Per-effect reset preserves match and other settings");
            policy.Record(command);
            Check(policy.FriendlyFire.DotIntervals.Seconds(effect) == ExpectedDefault(effect) && !policy.HasChanges,
                "Per-effect reset restores the current default and needs no saved row");
        }
        foreach (string invalid in new[] { "/one friendlyfire dot", "/one friendlyfire dot missing 1", "/one friendlyfire dot burn", "/one friendlyfire dot burn 1 2" })
            Check(FriendlyFireCommand.Parse(invalid, out _) == FriendlyFireParseResult.Invalid, "Reject invalid DoT command");
        Check(FriendlyFireCommand.Parse("/one friendlyfire dot SHOCK 1.5", out command) == FriendlyFireParseResult.Valid,
            "Shock alias resolves case-insensitively to the Lightning setting");
        policy.Record(command);
        Check(policy.ToPresetText() == "SephiriaOne preset v37\nfriendlyfire dot-lightning 1.5\n", "Alias serializes as canonical Lightning");
        policy = new SessionPolicy();
        for (int version = 34; version <= 37; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-lightning 1.25\nfriendlyfire dot-dark-cloud 0.5\n", out var migrated, out _) &&
                migrated.FriendlyFire.DotIntervals.Seconds(PvpDotEffect.Lightning) == 1.25m &&
                migrated.FriendlyFire.DotIntervals.Seconds(PvpDotEffect.PlasmaShock) == (version < 37 ? 1.25m : 3m) &&
                migrated.FriendlyFire.DotIntervals.Seconds(PvpDotEffect.DarkCloud) == 0.5m,
                "Legacy explicit shared shock override migrates to both clocks; v37 separates them");
            Check(SessionPolicy.TryReadPreset(migrated.ToPresetText(), out var reloaded, out _) && reloaded.FriendlyFire.Equals(migrated.FriendlyFire),
                "Migrated shock settings roundtrip without changing a saved custom Dark Cloud interval");
            if (version >= 35)
                Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-preserve-lightning 0\n", out var oldSchedule, out _) &&
                    !oldSchedule.FriendlyFire.DotIntervals.PreserveSchedule(PvpDotEffect.Lightning) &&
                    oldSchedule.FriendlyFire.DotIntervals.PreserveSchedule(PvpDotEffect.PlasmaShock) == (version == 37),
                    "Legacy explicit shared schedule toggle migrates to both effects");
            if (version < 37)
                foreach (string row in new[] { "dot-plasma-shock 0.9", "dot-preserve-plasma-shock 0" })
                    Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire {row}\n", out var bad, out _) && !bad.HasChanges,
                        "Legacy schemas reject separate Plasma shock controls atomically");
        }
        for (int version = 1; version <= 33; version++)
            foreach (var effect in Enum.GetValues<PvpDotEffect>().Where(e => (int)e >= 4))
            {
                Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var omitted, out _) &&
                    omitted.FriendlyFire.DotIntervals.Seconds(effect) == ExpectedDefault(effect), "Legacy schema inherits new default: " + effect);
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-{PvpDotSettings.Key(effect)} 1\n", out var bad, out _) &&
                    !bad.HasChanges, "Legacy schema rejects new effect rows atomically: " + effect);
            }
        foreach (string row in new[] { "dot-wound 0", "dot-lightning 0.001", "dot-freeze NaN", "dot-chim 11", "dot-dark-cloud 1.00",
            "dot-shock 1", "dot-DARK-CLOUD 1", "dot-dark-cloud 1\nfriendlyfire dot-dark-cloud 2" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v34\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) &&
                !bad.HasChanges, "Invalid extended preset is atomic: " + row);
        for (int version = 1; version <= 27; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.DotIntervals.HasChanges,
                "Old presets inherit current per-effect defaults: " + version);
            foreach (var effect in Enum.GetValues<PvpDotEffect>())
                Check(old.FriendlyFire.DotIntervals.Seconds(effect) == ExpectedDefault(effect), "Omitted old interval uses current default: " + effect);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-burn 1\n", out _, out _), "Old schemas reject interval rows");
        }
        for (int version = 28; version <= 34; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var omitted, out _) &&
                !omitted.HasChanges && omitted.FriendlyFire.DotIntervals.Seconds(PvpDotEffect.Burn) == 0.9m,
                "Omitted Burn interval inherits 0.9 seconds: " + version);
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-burn 0.75\n", out var explicitOld, out _) &&
                explicitOld.FriendlyFire.DotIntervals.Seconds(PvpDotEffect.Burn) == 0.75m && explicitOld.HasChanges &&
                explicitOld.ToPresetText().Contains("friendlyfire dot-burn 0.75\n"),
                "Explicit prior Burn default remains a saved override: " + version);
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
        FriendlyFireCommand.Parse("/one friendlyfire dot burn reset", out command);
        settings = settings.Apply(command);
        Check(settings.DotIntervals.Seconds(PvpDotEffect.Burn) == 0.9m && settings.DotIntervals.Seconds(PvpDotEffect.PlasmaBurn) == 2 &&
            settings.DotIntervals.Seconds(PvpDotEffect.Poison) == 4, "Single reset preserves other edited intervals");
        settings = settings.Apply(new(false, reset: true));
        Check(!settings.HasChanges && !settings.DotIntervals.HasChanges, "Full reset restores all intervals");
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
            Check(settings.DotIntervals.Seconds(effect) == ExpectedDefault(effect), "Full reset restores the effect's current default: " + effect);
        foreach (decimal bad in new[] { 0m, 0.001m, 10.01m, decimal.MaxValue, decimal.MinValue })
        {
            bool rejected = false;
            try { _ = new PvpDotChange(PvpDotEffect.Burn, bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Range validation cannot overflow: " + bad);
        }
        return checks;
    }
}
