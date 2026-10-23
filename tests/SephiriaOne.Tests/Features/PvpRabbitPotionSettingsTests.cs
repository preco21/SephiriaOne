using SephiriaOne;

internal static class PvpRabbitPotionSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        var policy = new SessionPolicy();
        Check(policy.FriendlyFire.BlockRabbitInfinite && !policy.HasChanges, "Default restriction is on and sparse");
        foreach (string action in new[] { "off", "on", "reset", "OFF" })
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire block-rabbit-infinite " + action, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle && command.IsReset == !action.Equals("off", StringComparison.OrdinalIgnoreCase), "Independent command and recovery semantics");
            policy.Record(command);
            bool blocked = !action.Equals("off", StringComparison.OrdinalIgnoreCase);
            Check(policy.FriendlyFire.BlockRabbitInfinite == blocked && !policy.FriendlyFire.Enabled && policy.FriendlyFire.InfiniteHpPotions,
                "Only restriction changes; default general option preserved");
            Check(policy.FriendlyFire.WithEnabled(true).BlockRabbitInfinite == blocked && policy.FriendlyFire.WithEnabled(false).BlockRabbitInfinite == blocked,
                "Match overlays preserve restriction");
            string text = policy.ToPresetText();
            Check(SessionPolicy.TryReadPreset(text, out var roundTrip, out _) && roundTrip.FriendlyFire.BlockRabbitInfinite == blocked,
                "Preset roundtrip");
            Check(blocked || text.StartsWith("SephiriaOne preset v53\nfriendlyfire block-rabbit-infinite 0\n"), "Opt out requires new schema");
        }
        foreach (string sibling in new[] { "on", "damage 0.1", "balance on", "infinite-hp off", "auto-potion on", "rapid rate 6" })
        {
            FriendlyFireCommand.Parse("/one friendlyfire " + sibling, out var command); policy.Record(command);
            Check(!policy.FriendlyFire.BlockRabbitInfinite, "Sibling command preserves opt out");
        }
        Check(policy.FriendlyFire.Apply(new(false, reset: true)).BlockRabbitInfinite, "Full reset restores default");
        for (int version = 1; version <= 52; version++)
        {
            string header = $"SephiriaOne preset v{version}\n";
            Check(SessionPolicy.TryReadPreset(header, out var old, out _) && old.FriendlyFire.BlockRabbitInfinite, "Legacy presets inherit protection");
            Check(!SessionPolicy.TryReadPreset(header + "friendlyfire block-rabbit-infinite 0\n", out var bad, out _) && !bad.HasChanges, "Legacy schemas reject new rows");
        }
        foreach (string row in new[] { "2", "1.0", "true", "0\nfriendlyfire block-rabbit-infinite 1" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v53\nfriendlyfire block-rabbit-infinite " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Invalid/duplicate preset rejected atomically");
        foreach (string value in new[] { "", "true", "1", "on extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire block-rabbit-infinite " + value, out _) == FriendlyFireParseResult.Invalid, "Reject malformed command");
        return checks;
    }
}
