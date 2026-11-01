using SephiriaOne;

internal static class PvpEvasionSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        void Apply(string operation)
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire evasion " + operation, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle, "Evasion command preserves match state: " + operation);
            policy.Record(command);
        }
        Check(!policy.HasChanges && policy.FriendlyFire.Evasion.Enabled && policy.FriendlyFire.Evasion.SoftPercent == 30 &&
            policy.FriendlyFire.Evasion.HardPercent == 50, "Zero-init defaults on/30/50 without marking policy dirty");
        Check(default(PvpEvasionSettings).Equals(new PvpEvasionSettings(true)), "Explicit defaults equal default(T)");
        foreach (var pair in new[] { (0f, 0f), (10f, 10f), (30f, 30f), (50f, 45f), (70f, 50f), (100f, 50f) })
            Check(Math.Abs(default(PvpEvasionSettings).Limit(pair.Item1) - pair.Item2) < 0.0001f, "Shared chance curve " + pair);
        float previous = -1;
        for (int i = 0; i <= 10000; i++)
        {
            float input = i / 100f, actual = default(PvpEvasionSettings).Limit(input);
            Check(actual >= previous && actual <= input + 0.00001f && actual <= 50, "Monotonic bounded chance: " + input);
            previous = actual;
        }
        foreach (string limits in new[] { "0 0", "0.01 0.01", "0 100", "12.34 56.78", "100 100" })
        {
            Apply("limits " + limits);
            Check(!policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance, "Limit edit does not activate PvP");
            Check(policy.ToPresetText().StartsWith("SephiriaOne preset v60\n") &&
                SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire),
                "Custom limits round trip exactly");
        }
        Apply("limits 30% 50%"); Check(!policy.HasChanges, "Defaults restore sparse preset eligibility");
        Apply("limits 20 40"); Apply("off");
        Check(!policy.FriendlyFire.Evasion.Enabled && policy.FriendlyFire.Evasion.Limit(90) == 90 &&
            policy.FriendlyFire.Evasion.SoftPercent == 20, "Off restores native chance without discarding limits");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var disabled, out _) && !disabled.FriendlyFire.Evasion.Enabled,
            "Disabled limiter persists independently of master switch");
        var configured = policy.FriendlyFire.Evasion;
        foreach (var command in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, balance: true),
            new FriendlyFireCommand(false, percent: .1m), new FriendlyFireCommand(false, refillPercent: 125),
            new FriendlyFireCommand(false, maxHp: new PvpMaxHpChange(enabled: true)) })
        {
            policy.Record(command);
            Check(policy.FriendlyFire.Evasion.Equals(configured) && policy.FriendlyFire.WithEnabled(false).Evasion.Equals(configured),
                "Sibling edits and mode overlay preserve Evasion policy");
        }
        Apply("reset"); Check(!policy.FriendlyFire.Evasion.HasChanges && policy.FriendlyFire.Evasion.Enabled && policy.FriendlyFire.Balance,
            "Reset restores on/30/50 without resetting other combat controls");
        foreach (string invalid in new[] { "limits", "limits 10", "limits 50 30", "limits -1 50", "limits +1 50", "limits 0 100.01",
            "limits 0.001 50", "limits 30 50.000", "limits NaN 50", "limits 30 Infinity", "limits 1e1 50", "limits 1,5 50",
            "limits 30 50 extra", "on extra", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire evasion " + invalid, out _) == FriendlyFireParseResult.Invalid,
                "Invalid input rejected: " + invalid);
        for (int version = 1; version < 60; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.Evasion.HasChanges,
                "Old preset receives approved defaults: " + version);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire evasion 0\n", out _, out _), "Old schema rejects new fields");
        }
        foreach (string row in new[] { "evasion 2", "evasion 01", "evasion 0\nfriendlyfire evasion 1", "evasion-limits 50 30",
            "evasion-limits 0 100.01", "evasion-limits -1 50", "evasion-limits 10% 50", "evasion-limits 10 50.0",
            "evasion-limits 10 20\nfriendlyfire evasion-limits 20 30" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v60\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Malformed preset does not partially apply: " + row);
        Check(!new PvpEvasionChange(softPercent: 20).IsValid && !new PvpEvasionChange(hardPercent: 50).IsValid &&
            !new PvpEvasionChange(softPercent: 0, hardPercent: 101).IsValid, "Programmatic changes validate complete probability pairs");
        return checks;
    }
}
