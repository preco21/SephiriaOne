using SephiriaOne;

internal static class PvpCriticalDamageSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        void Parse(SessionPolicy policy, string operation)
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire critical-damage " + operation, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle, "CriticalDamage command keeps the current combat mode: " + operation);
            policy.Record(command);
        }
        Check(FriendlyFireCommand.Parse("/one friendlyfire critical-damage limits 50 100", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Limits apply atomically without touching the combat toggle");
        var policy = new SessionPolicy();
        Check(!policy.HasChanges && !policy.FriendlyFire.CriticalDamage.Enabled && policy.FriendlyFire.CriticalDamage.SoftPercent == 30m &&
            policy.FriendlyFire.CriticalDamage.HardPercent == 70m, "The zero-initialized policy defaults off, soft 30 and hard 70 bonus points");
        Check(default(PvpCriticalDamageSettings).Equals(new PvpCriticalDamageSettings(false)), "Explicit critical-damage defaults equal default(T)");
        foreach (string pair in new[] { "0 0", "0.01 0.01", "0.01 10000", "10.25 22.5", "500 600", "10000 10000" })
        {
            Parse(policy, "limits " + pair);
            Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance && !policy.FriendlyFire.CriticalDamage.Enabled,
                "Changing limits does not enable any combat toggle");
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v57\n") &&
                SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire),
                "Disabled custom limits round trip exactly in v57: " + pair);
        }
        Parse(policy, "limits 30% 70%");
        Check(!policy.HasChanges, "Default limits restore legacy preset eligibility");
        foreach (string bad in new[] { "limits", "limits 10", "limits 101 100", "limits -1 100", "limits +1 100", "limits 0 10000.01",
            "limits 0.001 100", "limits 50 100.000", "limits 1e1 100", "limits 1,5 100", "limits NaN 100", "limits 50 Infinity",
            "limits 50 100 extra", "soft 50", "on extra", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire critical-damage " + bad, out _) == FriendlyFireParseResult.Invalid,
                "Reject invalid critical-damage command: " + bad);
        Parse(policy, "limits 200 300");
        Parse(policy, "on");
        Check(policy.FriendlyFire.CriticalDamage.Enabled && policy.FriendlyFire.CriticalDamage.SoftPercent == 200 &&
            policy.FriendlyFire.CriticalDamage.HardPercent == 300, "On preserves configured limits");
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE CRITICAL-DAMAGE OFF", out command) == FriendlyFireParseResult.Valid &&
            command.IsReset && !command.TouchesMasterToggle, "Off remains a recovery operation and is case insensitive");
        policy.Record(command);
        Check(!policy.FriendlyFire.CriticalDamage.Enabled && policy.FriendlyFire.CriticalDamage.SoftPercent == 200,
            "Off preserves limits");
        Parse(policy, "on");
        var configured = policy.FriendlyFire.CriticalDamage;
        foreach (var sibling in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, percent: 0.1m),
            new FriendlyFireCommand(false, balance: true), new FriendlyFireCommand(false, blockLobbyRegen: true),
            new FriendlyFireCommand(false, refillPercent: 125), new FriendlyFireCommand(false, guardMpPercent: 10),
            new FriendlyFireCommand(false, hitFrequency: true), new FriendlyFireCommand(false, autoPotion: true),
            new FriendlyFireCommand(false, infiniteHpPotions: false), new FriendlyFireCommand(false, dot: new PvpDotChange(PvpDotEffect.Burn, 1.2m)),
            new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(PvpDotEffect.Burn, false)) })
        {
            policy.Record(sibling);
            Check(policy.FriendlyFire.CriticalDamage.Equals(configured) && policy.FriendlyFire.WithEnabled(false).CriticalDamage.Equals(configured),
                "Sibling edits and mode overlays preserve critical-damage limits");
        }
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(),
            "CriticalDamage preset retains all other combat settings");
        Parse(policy, "reset");
        Check(!policy.FriendlyFire.CriticalDamage.HasChanges && policy.FriendlyFire.Enabled && policy.FriendlyFire.Balance &&
            policy.FriendlyFire.DamagePercent == 0.1m, "CriticalDamage reset restores only this option to off/30/70");
        Parse(policy, "limits 20 30");
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges && !policy.FriendlyFire.CriticalDamage.HasChanges, "Full combat reset clears critical-damage too");
        for (int version = 1; version <= 56; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.CriticalDamage.HasChanges,
                "Older preset inherits off/30/70: " + version);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire critical-damage 1\n", out _, out _) &&
                !SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire critical-damage-limits 10 20\n", out _, out _),
                "Older schema rejects critical-damage fields: " + version);
        }
        foreach (string rows in new[] { "critical-damage 2", "critical-damage 01", "critical-damage 1\nfriendlyfire critical-damage 0",
            "critical-damage-limits 101 100", "critical-damage-limits -1 100", "critical-damage-limits 0 10001", "critical-damage-limits 0.001 100",
            "critical-damage-limits 010 100", "critical-damage-limits 10 100.0", "critical-damage-limits 10% 100", "critical-damage-limits 10 100%",
            "critical-damage-limits 50", "critical-damage-limits 10 20\nfriendlyfire critical-damage-limits 30 40" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v57\nfriendlyfire enabled 1\nfriendlyfire " + rows + "\n", out var bad, out _) && !bad.HasChanges,
                "Invalid critical-damage preset never partially applies: " + rows);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v57\nfriendlyfire critical-damage-limits 500 600\nfriendlyfire critical-damage 1\n", out var reversed, out _) &&
            reversed.FriendlyFire.CriticalDamage.Enabled && reversed.FriendlyFire.CriticalDamage.SoftPercent == 500,
            "Preset row order cannot introduce an invalid intermediate limit pair");
        Check(!new PvpCriticalDamageChange(softPercent: 10).IsValid && !new PvpCriticalDamageChange(hardPercent: 100).IsValid &&
            !new PvpCriticalDamageChange(softPercent: 100, hardPercent: 50).IsValid, "Programmatic changes must include a valid complete pair");
        return checks;
    }
}
