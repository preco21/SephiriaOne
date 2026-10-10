using SephiriaOne;

internal static class FriendlyFirePolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.FriendlyFire.Enabled && policy.FriendlyFire.DamagePercent == 100 && !policy.HasChanges, "Combat defaults off and 100%");
        Check(default(FriendlyFireSettings).Equals(new FriendlyFireSettings(false, 100m)), "Explicit defaults equal the zero-initialized setting");
        foreach (decimal invalid in new[] { -0.01m, 300.01m, 0.001m, decimal.MaxValue, decimal.MinValue })
        {
            bool rejected = false;
            try { _ = new FriendlyFireSettings(true, invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Direct construction rejects invalid precision/range without overflow: " + invalid);
        }
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            foreach (string number in new[] { "0.01", "0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.25", "299.99" })
            {
                decimal expected = decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
                foreach (string token in new[] { number, number + "%" })
                {
                    Check(FriendlyFireCommand.Parse("/one friendlyfire damage " + token, out var command) == FriendlyFireParseResult.Valid,
                        "Accept fractional percentage " + token);
                    policy.Record(command);
                    Check(policy.FriendlyFire.DamagePercent == expected && !policy.FriendlyFire.Enabled,
                        "Fractional edit preserves exact value without enabling combat");
                    string preset = policy.ToPresetText();
                    Check(preset.StartsWith("SephiriaOne preset v19\n") && preset.Contains("friendlyfire damage " + number + "\n"),
                        "Fractional percentage emits canonical culture-independent v19 row");
                    Check(SessionPolicy.TryReadPreset(preset, out var restored, out _) && restored.FriendlyFire.DamagePercent == expected,
                        "Fractional percentage round-trips exactly");
                    policy.Record(new FriendlyFireCommand(true)); policy.Record(new FriendlyFireCommand(false));
                    Check(policy.FriendlyFire.DamagePercent == expected, "Toggling retains exact fractional value");
                }
            }
            foreach (string bad in new[] { "-0.1", "300.01", "NaN", "Infinity", "1e-1", "0,1", "0.001", "1.000", "0.1%%", "+0.1", "x2", "9999999999999999999999999999999999999" })
                Check(FriendlyFireCommand.Parse("/one friendlyfire damage " + bad, out _) == FriendlyFireParseResult.Invalid,
                    "Reject malformed/out-of-range fractional command " + bad);
            foreach (string row in new[] { "damage 0.001", "damage 300.01", "damage 0.10", "enabled 0.1", "damage NaN", "damage 0.1%", "damage 0.1\nfriendlyfire damage 0.2" })
                Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v19\nfriendlyfire " + row + "\n", out var invalid, out _) && !invalid.HasChanges,
                    "Invalid fractional preset remains atomic: " + row);
            policy.RecordDeathmatchDuration(45);
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.DeathmatchDuration == 45 &&
                combined.FriendlyFire.DamagePercent == policy.FriendlyFire.DamagePercent, "v19 preserves deathmatch alongside fractional damage");
            policy.Clear();
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        foreach (int percent in new[] { 0, 25, 100, 200, 300 })
        {
            policy.Record(new FriendlyFireCommand(true)); policy.Record(new FriendlyFireCommand(false,percent));
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v12\n"), "Combat changes require v12");
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(),out var restored,out _) && restored.FriendlyFire.Enabled &&
                restored.FriendlyFire.DamagePercent == percent, "Combat preset round trip: " + percent);
            policy.Record(new FriendlyFireCommand(false));
            Check(!policy.FriendlyFire.Enabled && policy.FriendlyFire.DamagePercent == percent, "Off retains chosen damage percentage");
        }
        foreach (string bad in new[] { "enabled 2", "damage -1", "damage 301", "damage 1.5", "damage NaN", "damage +5", "damage 025", "damage 25\nfriendlyfire damage 50", "enabled 1\nfriendlyfire enabled 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v12\nfriendlyfire " + bad + "\n", out var invalid,out _) && !invalid.HasChanges,
                "Invalid combat preset is atomic: " + bad);
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v11\nfriendlyfire enabled 1\n",out _,out _), "Old schema rejects new syntax");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v11\nitems unlock 1\n",out var old,out _) && !old.FriendlyFire.Enabled && old.FriendlyFire.DamagePercent == 100,
            "Old presets migrate with safe combat defaults");
        policy.Record(new FriendlyFireCommand(false,reset:true));
        Check(!policy.HasChanges && policy.FriendlyFire.DamagePercent == 100, "Combat reset restores defaults and old minimal schema");
        policy.Record(new FriendlyFireCommand(true)); policy.Clear();
        Check(!policy.FriendlyFire.Enabled, "Shared scope clear removes combat policy");
        return checks;
    }
}
