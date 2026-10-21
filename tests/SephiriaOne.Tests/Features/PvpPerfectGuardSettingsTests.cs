using SephiriaOne;

internal static class PvpPerfectGuardSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var defaults = default(PvpPerfectGuardSettings);
        Check(!defaults.Enabled && defaults.Multiplier == 2m && defaults.Scale == 2d && defaults.Number == "2" && !defaults.HasChanges &&
            defaults.Equals(new PvpPerfectGuardSettings()) && defaults.Equals(new PvpPerfectGuardSettings(false, 2)),
            "Default struct and explicit constructors share off/x2 exact policy");
        foreach (decimal value in new[] { 0m, 0.99m, 100.01m, 1.001m, decimal.MinValue, decimal.MaxValue })
        {
            Check(!PvpPerfectGuardSettings.ValidMultiplier(value) && !new PvpPerfectGuardChange(multiplier: value).IsValid,
                "Programmatic multiplier rejects invalid bounds or precision");
            bool rejected = false;
            try { _ = new PvpPerfectGuardSettings(true, value); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Constructor cannot create an invalid policy");
        }
        var policy = new SessionPolicy();
        void Parse(string edit)
        {
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE PERFECT-GUARD " + edit, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle && !command.Reset, "Perfect Guard edits preserve master/mode: " + edit);
            policy.Record(command);
        }
        Parse("multiplier x3.25");
        Check(policy.HasChanges && !policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance &&
            policy.ToPresetText() == "SephiriaOne preset v45\nfriendlyfire perfect-guard-multiplier 3.25\n",
            "Inactive Perfect Guard custom multiplier persists without enabling any switch");
        Parse("on");
        string configured = policy.ToPresetText();
        Check(configured.Contains("friendlyfire perfect-guard 1\n") && SessionPolicy.TryReadPreset(configured, out var restored, out _) &&
            restored.FriendlyFire.Equals(policy.FriendlyFire), "Enabled Perfect Guard settings round trip exactly");
        Parse("off");
        Check(!policy.ToPresetText().Contains("perfect-guard 1") && policy.ToPresetText().Contains("perfect-guard-multiplier 3.25"),
            "Disabling retains custom multiplier");
        Parse("on"); Parse("multiplier reset");
        Check(policy.ToPresetText() == "SephiriaOne preset v45\nfriendlyfire perfect-guard 1\n", "Value reset retains enabled toggle");
        Parse("reset");
        Check(!policy.HasChanges, "Family reset restores off/x2 only");
        foreach (string edit in new[] { "on extra", "off extra", "reset extra", "multiplier", "multiplier 0.99", "multiplier 100.01",
            "multiplier 1.001", "multiplier 1.000", "multiplier -1", "multiplier +2", "multiplier 2%", "multiplier 2e0",
            "multiplier 2,5", "multiplier NaN", "multiplier Infinity", "multiplier xx2", "multiplier x", "multiplier reset extra", "2" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire perfect-guard " + edit, out _) == FriendlyFireParseResult.Invalid,
                "Reject malformed or out-of-range Perfect Guard edit: " + edit);
        foreach (string value in new[] { "1", "100", "x1", "X100", "2.25", "x99.99" }) Parse("multiplier " + value);
        var retained = policy.FriendlyFire.PerfectGuard;
        Check(!retained.Enabled && retained.Multiplier == 99.99m && retained.Scale == 99.99d && retained.Number == "99.99", "Policy exposes exact multiplier units");
        foreach (var sibling in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, percent: 0.1m),
            new FriendlyFireCommand(false, balance: true), new FriendlyFireCommand(false, blockLobbyRegen: true),
            new FriendlyFireCommand(false, refillPercent: 125), new FriendlyFireCommand(false, guardMpPercent: 10),
            new FriendlyFireCommand(false, hitFrequency: true), new FriendlyFireCommand(false, autoPotion: true),
            new FriendlyFireCommand(false, infiniteHpPotions: false), new FriendlyFireCommand(false, dot: new PvpDotChange(PvpDotEffect.Burn, 1.2m)),
            new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(PvpDotEffect.Burn, false)),
            new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(true, 100, 200)),
            new FriendlyFireCommand(false, rapid: new PvpRapidChange(windowSeconds: 2)) })
        {
            policy.Record(sibling);
            Check(policy.FriendlyFire.PerfectGuard.Equals(retained) && policy.FriendlyFire.WithEnabled(false).PerfectGuard.Equals(retained) &&
                policy.FriendlyFire.WithEnabled(true).PerfectGuard.Equals(retained), "Sibling edits and mode overlays preserve inactive custom multiplier");
        }
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(),
            "v45 round trips all combined combat controls");
        var before = policy.FriendlyFire;
        Parse("reset");
        Check(policy.FriendlyFire.Equals(new FriendlyFireSettings(before.Enabled, before.DamagePercent, before.Balance,
            before.BlockLobbyRegen, before.RefillPercent, before.GuardMpPercent, before.DotIntervals, before.HitFrequency, before.AutoPotion,
            before.InfiniteHpPotions, before.Amplification, before.Rapid)), "Perfect Guard family reset preserves every sibling setting");
        Parse("multiplier 3.25");
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Check(policy.FriendlyFire.PerfectGuard.Number == "3.25" && SessionPolicy.TryReadPreset(policy.ToPresetText(), out var invariant, out _) &&
                invariant.FriendlyFire.Equals(policy.FriendlyFire), "Perfect Guard strings and presets stay invariant under decimal-comma cultures");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        for (int version = 1; version <= 44; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.HasChanges,
                "Old presets inherit Perfect Guard defaults: " + version);
            foreach (string row in new[] { "perfect-guard 1", "perfect-guard-multiplier 3" })
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire {row}\n", out var rejected, out _) && !rejected.HasChanges,
                    "Old schemas reject Perfect Guard rows atomically");
        }
        foreach (string row in new[] { "perfect-guard 2", "perfect-guard on", "perfect-guard 1.0", "perfect-guard 1\nfriendlyfire perfect-guard 0",
            "perfect-guard-multiplier 0", "perfect-guard-multiplier 100.01", "perfect-guard-multiplier 1.001", "perfect-guard-multiplier x2",
            "perfect-guard-multiplier 02", "perfect-guard-multiplier 2.0", "perfect-guard-multiplier 2\nfriendlyfire perfect-guard-multiplier 3" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v45\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var rejected, out _) && !rejected.HasChanges,
                "Malformed v45 preset rejects atomically: " + row);
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges, "Full combat reset restores Perfect Guard defaults");
        return checks;
    }
}
