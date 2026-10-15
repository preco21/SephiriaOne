using SephiriaOne;

internal static class FriendlyFireRefillTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(policy.FriendlyFire.RefillPercent == 70 && policy.FriendlyFire.RefillScale == 0.7 && !policy.HasChanges,
            "Zero-initialized refill is 70% HP/s without a policy override");
        foreach (string value in new[] { "1", "40", "70", "99.99", "100", "100.01", "300" })
            foreach (string suffix in new[] { "", "%" })
            {
                Check(FriendlyFireCommand.Parse("/one friendlyfire refill " + value + suffix, out var command) == FriendlyFireParseResult.Valid &&
                    !command.TouchesMasterToggle && !command.IsReset, "Refill is an ordinary settings edit: " + value + suffix);
                policy.Record(command);
                Check(policy.FriendlyFire.RefillNumber == value && !policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance,
                    "Refill edit preserves disabled combat and balancing");
                Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire), "Exact refill round trip");
                Check(value == "70" || policy.ToPresetText().StartsWith("SephiriaOne preset v24\n"), "Nondefault refill requires v24");
            }
        foreach (string bad in new[] { "", "0", "0.99", "300.01", "100.001", "100.000", "+100", "-1", "1e2", "100,5", "NaN", "Infinity", "x2", "100 extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire refill " + bad, out _) == FriendlyFireParseResult.Invalid, "Reject invalid refill: " + bad);
        foreach (decimal bad in new[] { 0m, -1m, 0.99m, 300.01m, 100.001m, decimal.MaxValue, decimal.MinValue })
        {
            bool rejected = false;
            try { _ = new FriendlyFireSettings(true, 100, refillPercent: bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Constructor validates refill without overflow: " + bad);
        }
        foreach (string row in new[] { "0", "301", "1.001", "NaN", "1%", "010", "10.0", "10\nfriendlyfire refill 20" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v24\nfriendlyfire enabled 1\nfriendlyfire refill " + row + "\n", out var invalid, out _) && !invalid.HasChanges,
                "Invalid refill preset remains atomic: " + row);
        for (int version = 1; version <= 23; version++)
        {
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire refill 40\n", out _, out _), "Older schema rejects refill: " + version);
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && old.FriendlyFire.RefillPercent == 70,
                "Older preset inherits new default: " + version);
        }
        policy.Record(new FriendlyFireCommand(false, refillPercent: 125.5m));
        policy.Record(new FriendlyFireCommand(true));
        policy.Record(new FriendlyFireCommand(false, percent: 0.1m));
        policy.Record(new FriendlyFireCommand(false, balance: true));
        policy.Record(new FriendlyFireCommand(false, blockLobbyRegen: true));
        Check(policy.FriendlyFire.RefillPercent == 125.5m && policy.FriendlyFire.WithEnabled(false).RefillScale == 1.255,
            "Other combat edits and match overlays retain the rate");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var combined, out _) && combined.ToPresetText() == policy.ToPresetText(), "v24 retains prior fields");
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE REFILL RESET", out var reset) == FriendlyFireParseResult.Valid && !reset.TouchesMasterToggle, "Refill-only reset keeps master");
        policy.Record(reset);
        Check(policy.FriendlyFire.RefillPercent == 70 && policy.FriendlyFire.Enabled && policy.FriendlyFire.Balance &&
            policy.FriendlyFire.BlockLobbyRegen && policy.FriendlyFire.DamagePercent == 0.1m, "Refill reset preserves all other preferences");
        policy.Record(new FriendlyFireCommand(false, refillPercent: 40));
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges && policy.FriendlyFire.RefillPercent == 70, "Full reset restores refill default");
        return checks;
    }
}
