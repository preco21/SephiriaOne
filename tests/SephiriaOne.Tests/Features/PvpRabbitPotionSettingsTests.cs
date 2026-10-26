using SephiriaOne;

internal static class PvpRabbitPotionSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        foreach (bool infinite in new[] { false, true })
        foreach (bool auto in new[] { false, true })
        foreach (string action in new[] { "off", "on", "reset", "OFF" })
        {
            var policy = new SessionPolicy();
            policy.Record(new FriendlyFireCommand(false, infiniteHpPotions: infinite));
            policy.Record(new FriendlyFireCommand(false, autoPotion: auto));
            string before = policy.ToPresetText();
            Check(FriendlyFireCommand.Parse("/one friendlyfire block-rabbit-infinite " + action, out var command) == FriendlyFireParseResult.Valid &&
                !command.TouchesMasterToggle, "Legacy command remains recognized without master-toggle semantics");
            policy.Record(command);
            Check(policy.ToPresetText() == before && policy.FriendlyFire.InfiniteHpPotions == infinite && policy.FriendlyFire.AutoPotion == auto,
                "Retired command cannot change consumption, automatic grants or unrelated policy");
            foreach (int legacy in new[] { 0, 1 })
            {
                string text = $"SephiriaOne preset v53\nfriendlyfire block-rabbit-infinite {legacy}\nfriendlyfire infinite-hp {(infinite ? 1 : 0)}\nfriendlyfire auto-potion {(auto ? 1 : 0)}\n";
                Check(SessionPolicy.TryReadPreset(text, out var migrated, out _) &&
                    migrated.FriendlyFire.InfiniteHpPotions == infinite && migrated.FriendlyFire.AutoPotion == auto,
                    "v53 legacy override is ignored while independent potion settings survive");
                Check(!migrated.ToPresetText().Contains("block-rabbit-infinite") &&
                    SessionPolicy.TryReadPreset(migrated.ToPresetText(), out var roundTrip, out _) &&
                    roundTrip.FriendlyFire.InfiniteHpPotions == infinite && roundTrip.FriendlyFire.AutoPotion == auto,
                    "Saving migrated presets removes obsolete override and roundtrips live settings");
            }
        }
        for (int version = 1; version <= 52; version++)
        {
            string header = $"SephiriaOne preset v{version}\n";
            Check(SessionPolicy.TryReadPreset(header, out var old, out _) && !old.HasChanges, "Legacy defaults remain sparse");
            Check(!SessionPolicy.TryReadPreset(header + "friendlyfire block-rabbit-infinite 0\n", out var bad, out _) && !bad.HasChanges, "Pre-v53 schemas still reject newer rows");
        }
        foreach (string row in new[] { "2", "1.0", "true", "0\nfriendlyfire block-rabbit-infinite 1" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v53\nfriendlyfire block-rabbit-infinite " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Invalid/duplicate legacy rows still reject the entire preset");
        foreach (string value in new[] { "", "true", "1", "on extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire block-rabbit-infinite " + value, out _) == FriendlyFireParseResult.Invalid, "Reject malformed legacy command");
        return checks;
    }
}
