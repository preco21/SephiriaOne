using SephiriaOne;

internal static class PvpRapidSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        foreach (string edit in new[] { "window 0.1", "window 10", "rate 0.1", "rate 100", "exponent 0", "exponent 2", "burst 1", "burst 100", "window reset", "rate reset", "exponent reset", "burst reset", "reset" })
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE RAPID " + edit, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle && !command.Reset, "Rapid numeric command preserves mode/toggles: " + edit);
        var defaults = default(PvpRapidSettings);
        Check(defaults.WindowSeconds == 1m && defaults.HitsPerSecond == 4m && defaults.Exponent == 0.5m && defaults.BurstPercent == 75m &&
            defaults.WindowScale == 1d && defaults.RateScale == 4d && defaults.ExponentScale == 0.5d && defaults.BurstScale == 0.75d &&
            !defaults.HasChanges && defaults.Equals(new PvpRapidSettings(1, 4, 0.5m, 75)), "Zero-initialized rapid settings retain every shipped default");
        var policy = new SessionPolicy();
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v44\nfriendlyfire rapid-burst 20\nfriendlyfire refill 300\n", out var explicitLegacy, out _) &&
            explicitLegacy.FriendlyFire.Rapid.BurstPercent == 20 && explicitLegacy.FriendlyFire.RefillPercent == 300 &&
            explicitLegacy.ToPresetText().Contains("friendlyfire rapid-burst 20\n"),
            "Explicit legacy reserve/refill survive the new default and remain serializable");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v44\nfriendlyfire rapid-rate 6\n", out var omittedBurst, out _) &&
            omittedBurst.FriendlyFire.Rapid.BurstPercent == 75 && omittedBurst.FriendlyFire.Rapid.HitsPerSecond == 6,
            "Omitted reserve adopts the current default without overwriting other saved tuning");
        Check(PvpRapidChange.TryParse("burst", "reset", out var resetBurst) &&
            resetBurst.Apply(new PvpRapidSettings(hitsPerSecond: 6, burstPercent: 20)).BurstPercent == 75 &&
            resetBurst.Apply(new PvpRapidSettings(hitsPerSecond: 6, burstPercent: 20)).HitsPerSecond == 6,
            "Single-field burst reset adopts 75 percent while retaining the player's cadence tuning");
        void Parse(string edit)
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire rapid " + edit, out var command) == FriendlyFireParseResult.Valid,
                "Accept rapid edit: " + edit);
            policy.Record(command);
        }
        foreach (string key in new[] { "window", "rate", "exponent", "burst" })
        {
            string[] values = key switch { "window" => new[] { "0.1", "1.25", "10" }, "rate" => new[] { "0.1", "8.75", "100" },
                "exponent" => new[] { "0", "1.25", "2" }, _ => new[] { "1", "35.25", "100" } };
            foreach (string value in values)
            {
                Parse(key + " " + value);
                string saved = policy.ToPresetText();
                Check(saved.StartsWith("SephiriaOne preset v44\n") && saved.Contains("friendlyfire rapid-" + key + " " + value + "\n") &&
                    SessionPolicy.TryReadPreset(saved, out var restored, out _) && restored.FriendlyFire.Equals(policy.FriendlyFire),
                    "Rapid number round trips with exact hundredths in v44: " + key + " " + value);
                Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance && !policy.FriendlyFire.HitFrequency,
                    "Rapid edits do not enable combat switches");
            }
            Parse(key + " reset");
            Check(!policy.HasChanges && !policy.ToPresetText().Contains("rapid-"), "Default numbers omit rapid preset rows");
            foreach (string value in new[] { "-1", "+1", "1e0", "1,5", "NaN", "Infinity", "0.001", "1.000", "1%", "1 extra", "reset extra" })
                Check(FriendlyFireCommand.Parse("/one friendlyfire rapid " + key + " " + value, out _) == FriendlyFireParseResult.Invalid,
                    "Reject malformed rapid number: " + key + " " + value);
        }
        foreach (string edit in new[] { "window 0.09", "window 10.01", "rate 0.09", "rate 100.01", "exponent 2.01", "burst 0.99", "burst 100.01", "reset extra", "window", "timer 2", "on", "resetkda" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire rapid " + edit, out _) == FriendlyFireParseResult.Invalid, "Reject out-of-range or unknown rapid control: " + edit);
        foreach (var invalid in new[] { new PvpRapidChange(windowSeconds: 0), new PvpRapidChange(hitsPerSecond: 101),
            new PvpRapidChange(exponent: 0.001m), new PvpRapidChange(burstPercent: decimal.MaxValue) })
            Check(!invalid.IsValid, "Programmatic rapid edits validate before mutation");
        Parse("window 1.25"); Parse("rate 8.75"); Parse("exponent 0"); Parse("burst 35.25");
        var configured = policy.FriendlyFire.Rapid;
        Check(configured.WindowNumber == "1.25" && configured.RateNumber == "8.75" && configured.ExponentNumber == "0" && configured.BurstNumber == "35.25" &&
            configured.WindowScale == 1.25 && configured.RateScale == 8.75 && configured.ExponentScale == 0 && configured.BurstScale == 0.3525,
            "Runtime scales and invariant strings preserve exact configured units");
        Check(configured.SameFrequency(new PvpRapidSettings(1.25m, 8.75m, 0, 50)) &&
            !configured.SameFrequency(new PvpRapidSettings(2, 8.75m, 0, 35.25m)) &&
            !configured.SameFrequency(new PvpRapidSettings(1.25m, 9, 0, 35.25m)) &&
            !configured.SameFrequency(new PvpRapidSettings(1.25m, 8.75m, 1, 35.25m)), "Only history parameters participate in SameFrequency");
        foreach (var sibling in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, percent: 0.1m),
            new FriendlyFireCommand(false, balance: true), new FriendlyFireCommand(false, blockLobbyRegen: true),
            new FriendlyFireCommand(false, refillPercent: 125), new FriendlyFireCommand(false, guardMpPercent: 10),
            new FriendlyFireCommand(false, hitFrequency: true), new FriendlyFireCommand(false, autoPotion: true),
            new FriendlyFireCommand(false, infiniteHpPotions: false), new FriendlyFireCommand(false, dot: new PvpDotChange(PvpDotEffect.Burn, 1.2m)),
            new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(PvpDotEffect.Burn, false)),
            new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(true, 100, 200)) })
        {
            policy.Record(sibling);
            Check(policy.FriendlyFire.Rapid.Equals(configured) && policy.FriendlyFire.WithEnabled(false).Rapid.Equals(configured) &&
                policy.FriendlyFire.WithEnabled(true).Rapid.Equals(configured), "Sibling edits and mode overlays retain all rapid numbers");
        }
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(),
            "v44 preserves all existing combat options");
        Check(FriendlyFireCommand.Parse("/one friendlyfire hit-frequency reset", out var toggleReset) == FriendlyFireParseResult.Valid,
            "Existing frequency reset command remains valid");
        policy.Record(toggleReset);
        Check(!policy.FriendlyFire.HitFrequency && policy.FriendlyFire.Rapid.Equals(configured), "Frequency reset changes only its toggle and retains rapid numbers");
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Check(configured.WindowNumber == "1.25" && configured.BurstNumber == "35.25" &&
                SessionPolicy.TryReadPreset(policy.ToPresetText(), out var invariant, out _) && invariant.FriendlyFire.Rapid.Equals(configured),
                "Rapid strings and preset round trips are invariant under decimal-comma cultures");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
        var beforeReset = policy.FriendlyFire;
        Parse("window reset");
        Check(policy.FriendlyFire.Rapid.WindowSeconds == 1 && policy.FriendlyFire.Rapid.HitsPerSecond == 8.75m &&
            policy.FriendlyFire.Rapid.Exponent == 0 && policy.FriendlyFire.Rapid.BurstPercent == 35.25m, "Per-field reset retains sibling numbers");
        Parse("reset");
        Check(!policy.FriendlyFire.Rapid.HasChanges && policy.FriendlyFire.Equals(new FriendlyFireSettings(beforeReset.Enabled,
            beforeReset.DamagePercent, beforeReset.Balance, beforeReset.BlockLobbyRegen, beforeReset.RefillPercent, beforeReset.GuardMpPercent,
            beforeReset.DotIntervals, beforeReset.HitFrequency, beforeReset.AutoPotion, beforeReset.InfiniteHpPotions, beforeReset.Amplification)),
            "Rapid reset changes only its four numbers, including when exponent was zero");
        for (int version = 1; version <= 43; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.Rapid.HasChanges,
                "Old presets inherit rapid defaults: " + version);
            foreach (string key in new[] { "window", "rate", "exponent", "burst" })
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire rapid-{key} 1\n", out var rejected, out _) && !rejected.HasChanges,
                    "Old schemas reject new rapid rows atomically");
        }
        foreach (string row in new[] { "rapid-window 0", "rapid-rate 101", "rapid-exponent -1", "rapid-burst 100.01", "rapid-window 01", "rapid-rate 4.0",
            "rapid-exponent 0.500", "rapid-burst 20%", "rapid-window 1\nfriendlyfire rapid-window 2", "rapid unknown 1", "rapid-timer 1", "rapid-window reset" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v44\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var rejected, out _) && !rejected.HasChanges,
                "Malformed v44 preset never partially applies: " + row);
        Parse("burst 99");
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges && !policy.FriendlyFire.Rapid.HasChanges, "Full combat reset restores rapid defaults too");
        return checks;
    }
}
