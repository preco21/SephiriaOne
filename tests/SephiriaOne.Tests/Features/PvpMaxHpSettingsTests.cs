using SephiriaOne;

internal static class PvpMaxHpSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string why) { if (!condition) throw new Exception(why); checks++; }
        void Parse(SessionPolicy policy, string operation)
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire max-hp " + operation, out var parsed) == FriendlyFireParseResult.Valid &&
                !parsed.TouchesMasterToggle, "Max HP command preserves the master toggle: " + operation);
            policy.Record(parsed);
        }
        Check(FriendlyFireCommand.Parse("/one friendlyfire max-hp limits 100 50 100", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Max HP limits parse atomically without touching the master toggle");
        var policy = new SessionPolicy();
        var defaults = default(PvpMaxHpSettings);
        Check(!policy.HasChanges && !defaults.Enabled && defaults.Baseline == 100m && defaults.Soft == 50m && defaults.Hard == 100m,
            "The zero-initialized policy defaults off, baseline 100 and bonus limits 50/100 HP");
        Check(defaults.Equals(new PvpMaxHpSettings(false)) && !defaults.HasChanges, "Explicit defaults equal default(T)");
        var enabled = new PvpMaxHpSettings(true);
        foreach (var sample in new[] { (25f, 25f), (99f, 99f), (100f, 100f), (150f, 150f), (200f, 187.5f), (250f, 200f), (10000f, 200f) })
        {
            Check(enabled.Limit(sample.Item1) == sample.Item2, "Shared bonus curve preserves baseline: " + sample.Item1);
            Check(defaults.Limit(sample.Item1) == sample.Item1, "Disabled policy retains native max HP");
        }
        Check(new PvpMaxHpSettings(true, 120, 0, 0).Limit(110) == 110 && new PvpMaxHpSettings(true, 120, 0, 0).Limit(121) == 120,
            "A zero-width bonus cap never increases below-baseline HP");
        foreach (string limits in new[] { "1 0 0", "1.01 0.01 0.01", "10000 0 10000", "12.25 10.25 22.5", "500 500 600", "10000 10000 10000" })
        {
            Parse(policy, "limits " + limits);
            Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance && !policy.FriendlyFire.MaxHp.Enabled,
                "Limit edits do not enable combat or normalization");
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v59\n") &&
                SessionPolicy.TryReadPreset(policy.ToPresetText(), out var loaded, out _) && loaded.FriendlyFire.Equals(policy.FriendlyFire),
                "Disabled custom HP limits round trip exactly: " + limits);
        }
        Parse(policy, "limits 100 50 100");
        Check(!policy.HasChanges, "Default limits restore sparse legacy preset eligibility");
        foreach (string bad in new[] { "limits", "limits 100 50", "limits 0 50 100", "limits 10001 50 100", "limits 100 101 100",
            "limits 100 -1 100", "limits +100 50 100", "limits 100 0 10000.01", "limits 100.001 50 100", "limits 100 50.001 100",
            "limits 100 50 100.000", "limits 1e2 50 100", "limits 1,5 50 100", "limits NaN 50 100", "limits 100 50 Infinity",
            "limits 100% 50 100", "limits 100 50% 100", "limits 100 50 100%", "limits 100 50 100 extra", "on extra", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire max-hp " + bad, out _) == FriendlyFireParseResult.Invalid,
                "Reject invalid Max HP command: " + bad);
        Parse(policy, "limits 120.25 60.5 90.75"); Parse(policy, "on");
        Check(policy.FriendlyFire.MaxHp.Enabled && policy.FriendlyFire.MaxHp.BaselineNumber == "120.25" &&
            policy.FriendlyFire.MaxHp.SoftNumber == "60.5" && policy.FriendlyFire.MaxHp.HardNumber == "90.75", "On retains configured HP limits");
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE MAX-HP OFF", out command) == FriendlyFireParseResult.Valid &&
            command.IsReset && !command.TouchesMasterToggle, "Off is case insensitive and remains a recovery operation");
        policy.Record(command);
        Check(!policy.FriendlyFire.MaxHp.Enabled && policy.FriendlyFire.MaxHp.Baseline == 120.25m, "Off preserves custom values");
        Parse(policy, "on"); var configured = policy.FriendlyFire.MaxHp;
        foreach (var sibling in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, percent: 0.1m),
            new FriendlyFireCommand(false, balance: true), new FriendlyFireCommand(false, blockLobbyRegen: true),
            new FriendlyFireCommand(false, refillPercent: 125), new FriendlyFireCommand(false, guardMpPercent: 10),
            new FriendlyFireCommand(false, hitFrequency: true), new FriendlyFireCommand(false, autoPotion: false),
            new FriendlyFireCommand(false, infiniteHpPotions: true), new FriendlyFireCommand(false, artifactRangeMeters: 7.25m),
            new FriendlyFireCommand(false, criticalDamage: new PvpCriticalDamageChange(enabled: true)),
            new FriendlyFireCommand(false, dot: new PvpDotChange(PvpDotEffect.Burn, 1.2m)),
            new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(PvpDotEffect.Burn, false)) })
        {
            policy.Record(sibling);
            Check(policy.FriendlyFire.MaxHp.Equals(configured) && policy.FriendlyFire.WithEnabled(false).MaxHp.Equals(configured),
                "Sibling edits and match mode overlays retain Max HP preferences");
        }
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(),
            "v59 preserves all existing combat settings");
        Parse(policy, "reset");
        Check(!policy.FriendlyFire.MaxHp.HasChanges && policy.FriendlyFire.Enabled && policy.FriendlyFire.Balance &&
            policy.FriendlyFire.DamagePercent == 0.1m && policy.FriendlyFire.ArtifactRange.Meters == 7.25m,
            "Max HP reset restores only off/100/50/100");
        Parse(policy, "limits 200 40 80"); policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges && !policy.FriendlyFire.MaxHp.HasChanges, "Full combat reset clears Max HP policy");
        for (int version = 1; version <= 58; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.MaxHp.HasChanges,
                "Old preset defaults to off/100/50/100: " + version);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire max-hp 1\n", out _, out _) &&
                !SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire max-hp-limits 100 50 100\n", out _, out _),
                "Old schema rejects Max HP fields: " + version);
        }
        foreach (string rows in new[] { "max-hp 2", "max-hp 01", "max-hp 1.0", "max-hp 1\nfriendlyfire max-hp 0",
            "max-hp-limits 0 50 100", "max-hp-limits 10001 50 100", "max-hp-limits 100 101 100", "max-hp-limits 100 -1 100",
            "max-hp-limits 100 0 10001", "max-hp-limits 100.001 50 100", "max-hp-limits 0100 50 100", "max-hp-limits 100 50 100.0",
            "max-hp-limits 100% 50 100", "max-hp-limits 100 50 100%", "max-hp-limits 100 50", "max-hp-limits 100 50 100 200",
            "max-hp-limits 100 50 100\nfriendlyfire max-hp-limits 120 60 80" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v59\nfriendlyfire enabled 1\nfriendlyfire " + rows + "\n", out var bad, out _) && !bad.HasChanges,
                "Corrupt Max HP presets never partially apply: " + rows);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v59\nfriendlyfire max-hp-limits 500 600 700\nfriendlyfire max-hp 1\n", out var reversed, out _) &&
            reversed.FriendlyFire.MaxHp.Enabled && reversed.FriendlyFire.MaxHp.Baseline == 500 && reversed.FriendlyFire.MaxHp.Soft == 600,
            "Preset row order retains the complete atomic limits tuple");
        foreach (var invalid in new[] { new PvpMaxHpChange(baseline: 100), new PvpMaxHpChange(soft: 50), new PvpMaxHpChange(hard: 100),
            new PvpMaxHpChange(baseline: 0, soft: 50, hard: 100), new PvpMaxHpChange(baseline: 100, soft: 101, hard: 100) })
        {
            Check(!invalid.IsValid, "Programmatic edits require a complete valid limits tuple");
            bool rejected = false;
            try { invalid.Apply(default); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid programmatic changes throw before applying");
        }
        return checks;
    }
}
