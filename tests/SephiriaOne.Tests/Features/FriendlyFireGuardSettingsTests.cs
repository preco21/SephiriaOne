using SephiriaOne;

internal static class FriendlyFireGuardSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire guard-mp 10", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Guard MP edit is accepted without touching the master toggle");
        var policy = new SessionPolicy();
        Check(policy.FriendlyFire.GuardMpPercent == 5 && !policy.HasChanges,
            "Default-zero policy inherits 5 percent maximum MP");
        foreach (string value in new[] { "0", "0.01", "0.1", "5", "8", "10", "12.5", "99.99", "100" })
            foreach (string suffix in new[] { "", "%" })
            {
                Check(FriendlyFireCommand.Parse("/one friendlyfire guard-mp " + value + suffix, out command) == FriendlyFireParseResult.Valid &&
                    !command.TouchesMasterToggle && !command.IsReset, "Valid guard percent: " + value + suffix);
                policy.Record(command);
                Check(policy.FriendlyFire.GuardMpNumber == value && !policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance,
                    "Guard cost edit preserves disabled combat and balancing");
                Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire),
                    "Guard setting round trips exactly");
                Check(value == "5" || policy.ToPresetText().StartsWith("SephiriaOne preset v27\n"), "Nondefault guard cost requires v27");
            }
        foreach (string bad in new[] { "", "-1", "+1", "100.01", "100.001", "10.000", "1e1", "1,5", "NaN", "Infinity", "x2", "10 extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire guard-mp " + bad, out _) == FriendlyFireParseResult.Invalid, "Reject bad guard input: " + bad);
        foreach (decimal bad in new[] { -1m, 100.01m, 0.001m, decimal.MaxValue, decimal.MinValue })
        {
            bool rejected = false;
            try { _ = new FriendlyFireSettings(true, 100, guardMpPercent: bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Validate guard percent without overflow: " + bad);
        }
        foreach (string row in new[] { "-1", "101", "10.001", "NaN", "10%", "010", "10.0", "10\nfriendlyfire guard-mp 20" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v27\nfriendlyfire enabled 1\nfriendlyfire guard-mp " + row + "\n", out var invalid, out _) && !invalid.HasChanges,
                "Invalid guard preset is atomic: " + row);
        for (int version = 1; version <= 26; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && old.FriendlyFire.GuardMpPercent == 5,
                "Old preset inherits guard default: " + version);
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire guard-mp 15\n", out _, out _),
                "Older schema rejects new guard field: " + version);
        }
        policy.Record(new FriendlyFireCommand(false, guardMpPercent: 12.5m));
        policy.Record(new FriendlyFireCommand(true));
        policy.Record(new FriendlyFireCommand(false, percent: 0.1m));
        policy.Record(new FriendlyFireCommand(false, refillPercent: 99.99m));
        policy.Record(new FriendlyFireCommand(false, balance: true));
        policy.Record(new FriendlyFireCommand(false, blockLobbyRegen: true));
        Check(policy.FriendlyFire.GuardMpPercent == 12.5m && policy.FriendlyFire.WithEnabled(false).GuardMpPercent == 12.5m,
            "All combat edits and match overlays preserve guard percent");
        policy.RecordPlayTagChaser(new CombatRoleSettings(true, 3, true, 50));
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(),
            "v27 preserves other combat and chaser fields");
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE GUARD-MP RESET", out var reset) == FriendlyFireParseResult.Valid && !reset.TouchesMasterToggle,
            "Guard-only reset is case insensitive and does not stop combat");
        policy.Record(reset);
        Check(policy.FriendlyFire.GuardMpPercent == 5 && policy.FriendlyFire.Enabled && policy.FriendlyFire.Balance &&
            policy.FriendlyFire.BlockLobbyRegen && policy.FriendlyFire.RefillPercent == 99.99m && policy.FriendlyFire.DamagePercent == 0.1m,
            "Guard reset preserves all sibling settings");
        policy.Record(new FriendlyFireCommand(false, guardMpPercent: 20));
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.FriendlyFire.HasChanges && policy.FriendlyFire.GuardMpPercent == 5, "Full combat reset restores native policy defaults");
        return checks;
    }
}
