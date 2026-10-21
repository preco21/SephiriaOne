using SephiriaOne;

internal static class PvpAmplificationSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        void Parse(SessionPolicy policy, string operation)
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire amplification " + operation, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle, "Amplification command keeps the current combat mode: " + operation);
            policy.Record(command);
        }
        Check(FriendlyFireCommand.Parse("/one friendlyfire amplification limits 50 100", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Limits apply atomically without touching the combat toggle");
        var policy = new SessionPolicy();
        Check(!policy.HasChanges && !policy.FriendlyFire.Amplification.Enabled && policy.FriendlyFire.Amplification.SoftPercent == 50m &&
            policy.FriendlyFire.Amplification.HardPercent == 100m, "The zero-initialized policy defaults off, soft 50 and hard 100 bonus points");
        Check(default(PvpAmplificationSettings).Equals(new PvpAmplificationSettings(false)), "Explicit amplification defaults equal default(T)");
        foreach (string pair in new[] { "0 0", "0.01 0.01", "0.01 10000", "10.25 22.5", "500 600", "10000 10000" })
        {
            Parse(policy, "limits " + pair);
            Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance && !policy.FriendlyFire.Amplification.Enabled,
                "Changing limits does not enable any combat toggle");
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v43\n") &&
                SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire),
                "Disabled custom limits round trip exactly in v43: " + pair);
        }
        Parse(policy, "limits 50% 100%");
        Check(!policy.HasChanges, "Default limits restore legacy preset eligibility");
        foreach (string bad in new[] { "limits", "limits 10", "limits 101 100", "limits -1 100", "limits +1 100", "limits 0 10000.01",
            "limits 0.001 100", "limits 50 100.000", "limits 1e1 100", "limits 1,5 100", "limits NaN 100", "limits 50 Infinity",
            "limits 50 100 extra", "soft 50", "on extra", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire amplification " + bad, out _) == FriendlyFireParseResult.Invalid,
                "Reject invalid amplification command: " + bad);
        Parse(policy, "limits 200 300");
        Parse(policy, "on");
        Check(policy.FriendlyFire.Amplification.Enabled && policy.FriendlyFire.Amplification.SoftPercent == 200 &&
            policy.FriendlyFire.Amplification.HardPercent == 300, "On preserves configured limits");
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE AMPLIFICATION OFF", out command) == FriendlyFireParseResult.Valid &&
            command.IsReset && !command.TouchesMasterToggle, "Off remains a recovery operation and is case insensitive");
        policy.Record(command);
        Check(!policy.FriendlyFire.Amplification.Enabled && policy.FriendlyFire.Amplification.SoftPercent == 200,
            "Off preserves limits");
        Parse(policy, "on");
        var configured = policy.FriendlyFire.Amplification;
        foreach (var sibling in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, percent: 0.1m),
            new FriendlyFireCommand(false, balance: true), new FriendlyFireCommand(false, blockLobbyRegen: true),
            new FriendlyFireCommand(false, refillPercent: 125), new FriendlyFireCommand(false, guardMpPercent: 10),
            new FriendlyFireCommand(false, hitFrequency: true), new FriendlyFireCommand(false, autoPotion: true),
            new FriendlyFireCommand(false, infiniteHpPotions: false), new FriendlyFireCommand(false, dot: new PvpDotChange(PvpDotEffect.Burn, 1.2m)),
            new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(PvpDotEffect.Burn, false)) })
        {
            policy.Record(sibling);
            Check(policy.FriendlyFire.Amplification.Equals(configured) && policy.FriendlyFire.WithEnabled(false).Amplification.Equals(configured),
                "Sibling edits and mode overlays preserve amplification limits");
        }
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(),
            "Amplification preset retains all other combat settings");
        Parse(policy, "reset");
        Check(!policy.FriendlyFire.Amplification.HasChanges && policy.FriendlyFire.Enabled && policy.FriendlyFire.Balance &&
            policy.FriendlyFire.DamagePercent == 0.1m, "Amplification reset restores only this option to off/50/100");
        Parse(policy, "limits 20 30");
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges && !policy.FriendlyFire.Amplification.HasChanges, "Full combat reset clears amplification too");
        for (int version = 1; version <= 42; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.Amplification.HasChanges,
                "Older preset inherits off/50/100: " + version);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire amplification 1\n", out _, out _) &&
                !SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire amplification-limits 10 20\n", out _, out _),
                "Older schema rejects amplification fields: " + version);
        }
        foreach (string rows in new[] { "amplification 2", "amplification 01", "amplification 1\nfriendlyfire amplification 0",
            "amplification-limits 101 100", "amplification-limits -1 100", "amplification-limits 0 10001", "amplification-limits 0.001 100",
            "amplification-limits 010 100", "amplification-limits 10 100.0", "amplification-limits 10% 100", "amplification-limits 10 100%",
            "amplification-limits 50", "amplification-limits 10 20\nfriendlyfire amplification-limits 30 40" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v43\nfriendlyfire enabled 1\nfriendlyfire " + rows + "\n", out var bad, out _) && !bad.HasChanges,
                "Invalid amplification preset never partially applies: " + rows);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v43\nfriendlyfire amplification-limits 500 600\nfriendlyfire amplification 1\n", out var reversed, out _) &&
            reversed.FriendlyFire.Amplification.Enabled && reversed.FriendlyFire.Amplification.SoftPercent == 500,
            "Preset row order cannot introduce an invalid intermediate limit pair");
        Check(!new PvpAmplificationChange(softPercent: 10).IsValid && !new PvpAmplificationChange(hardPercent: 100).IsValid &&
            !new PvpAmplificationChange(softPercent: 100, hardPercent: 50).IsValid, "Programmatic changes must include a valid complete pair");
        return checks;
    }
}
