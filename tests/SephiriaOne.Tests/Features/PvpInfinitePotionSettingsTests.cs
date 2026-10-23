using SephiriaOne;

internal static class PvpInfinitePotionSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(policy.FriendlyFire.InfiniteHpPotions && !policy.HasChanges, "PvP infinite potions default on");
        Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE INFINITE-HP ON", out var command) == FriendlyFireParseResult.Valid &&
            !command.IsReset && !command.TouchesMasterToggle, "Enable is an independent option, not a master toggle");
        policy.Record(command);
        Check(policy.FriendlyFire.InfiniteHpPotions && !policy.FriendlyFire.Enabled &&
            policy.FriendlyFire.AutoPotion == default(FriendlyFireSettings).AutoPotion &&
            !policy.RabbitPotions.HasChanges, "PvP retention leaves combat, supply and Rabbit settings unchanged");
        policy.Record(new FriendlyFireCommand(false, infiniteHpPotions: false));
        Check(policy.ToPresetText() == "SephiriaOne preset v37\nfriendlyfire infinite-hp 0\n" &&
            SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) && saved.FriendlyFire.Equals(policy.FriendlyFire),
            "Explicit opt-out roundtrips with v37");
        Check(!policy.FriendlyFire.WithEnabled(true).InfiniteHpPotions && !policy.FriendlyFire.WithEnabled(false).InfiniteHpPotions,
            "Match overlays preserve an explicit opt-out");
        Check(new FriendlyFireSettings(true, 100).InfiniteHpPotions &&
            !new FriendlyFireSettings(true, 100, infiniteHpPotions: false).InfiniteHpPotions,
            "Explicit construction uses the same default while allowing opt-out");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v37\nfriendlyfire infinite-hp 1\n", out var enabled, out _) &&
            enabled.FriendlyFire.InfiniteHpPotions, "Previous explicit opt-in remains enabled");
        policy.Record(command);
        foreach (var edit in new FriendlyFireCommand[] { new(true), new(false), new(false, balance: true), new(false, percent: 0.05m),
            new(false, autoPotion: true), new(false, dot: new(PvpDotEffect.PlasmaShock, 1.5m)) })
        {
            policy.Record(edit);
            Check(policy.FriendlyFire.InfiniteHpPotions && policy.FriendlyFire.WithEnabled(true).InfiniteHpPotions &&
                policy.FriendlyFire.WithEnabled(false).InfiniteHpPotions, "Mode overlays and sibling settings retain the separate option");
        }
        foreach (string action in new[] { "off", "reset" })
        {
            Check(FriendlyFireCommand.Parse("/one friendlyfire infinite-hp " + action, out command) == FriendlyFireParseResult.Valid &&
                command.IsReset == (action == "off") && !command.TouchesMasterToggle, "Off is recovery; reset restores enabled default");
            var changed = policy.FriendlyFire.Apply(command);
            Check(changed.InfiniteHpPotions == (action == "reset") && changed.AutoPotion && changed.Balance && changed.DamagePercent == 0.05m &&
                changed.DotIntervals.Seconds(PvpDotEffect.PlasmaShock) == 1.5m, "Only the infinite HP option is reset");
        }
        Check(!policy.FriendlyFire.Apply(new(false, reset: true)).HasChanges &&
            policy.FriendlyFire.Apply(new(false, reset: true)).InfiniteHpPotions, "Full reset restores default-on potions");
        foreach (string bad in new[] { "", "1", "true", "on extra", "off off" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire infinite-hp " + bad, out _) == FriendlyFireParseResult.Invalid,
                "Reject malformed infinite potion command");
        for (int version = 1; version <= 36; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && old.FriendlyFire.InfiniteHpPotions,
                "Omitted legacy settings inherit the enabled default");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire infinite-hp 1\n", out var bad, out _) && !bad.HasChanges,
                "Legacy schemas reject the new row atomically");
        }
        foreach (string row in new[] { "infinite-hp 2", "infinite-hp 1.0", "INFINITE-HP 1", "infinite-hp 1\nfriendlyfire infinite-hp 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v37\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Invalid potion presets fail atomically");
        return checks;
    }
}
