using SephiriaOne;

internal static class RabbitPotionSettingsTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string scenario)
        { if (!value) throw new Exception(scenario); checks++; }
        RabbitCommand Parse(string text)
        {
            Check(RabbitCommand.Parse(text, out var command, out _) == RabbitParseResult.Valid, text);
            return command;
        }
        var policy = new SessionPolicy();
        Check(!policy.HasChanges && !policy.RabbitPotions.HasChanges && !policy.RabbitPotions.LevelUpPotion, "Defaults are native");
        var levelUpOn = Parse(" /ONE RABBIT LEVEL-UP-POTION ON ");
        var levelUpOff = Parse("/one rabbit level-up-potion off");
        Check(levelUpOn.Option == RabbitOption.LevelUpPotion && levelUpOn.Enabled && !levelUpOn.IsReset &&
            levelUpOff.Option == levelUpOn.Option && !levelUpOff.Enabled && levelUpOff.IsReset,
            "Level-up potion toggle parses case-insensitively and off remains a recovery action");
        policy.Record(levelUpOn);
        const string levelUpOnly = "SephiriaOne preset v10\nrabbit level-up-potion 1\n";
        Check(policy.HasChanges && policy.RabbitPotions.HasChanges && policy.RabbitPotions.LevelUpPotion && policy.ToPresetText() == levelUpOnly,
            "Level-up potion alone is a saved v10 setting");
        Check(!policy.RabbitPotions.Infinite && !policy.RabbitPotions.Share && !policy.RabbitPotions.ConsumeMp &&
            !policy.RabbitPotions.SuppressSurvival && policy.RabbitPotions.MpCostPerDrink == 10,
            "Level-up potion does not change existing rabbit settings");
        Check(SessionPolicy.TryReadPreset(levelUpOnly, out var levelUpLoad, out _) && levelUpLoad.ToPresetText() == levelUpOnly,
            "Level-up potion round trips in v10");
        foreach (string text in new[] { "infinite on", "infinite off", "share on", "share off", "mp-cost on", "mp-cost off",
            "mp-cost 25", "suppress-survival on", "suppress-survival off" })
        {
            policy.Record(Parse("/one rabbit " + text));
            Check(policy.RabbitPotions.LevelUpPotion && policy.DescribeSettings().Contains("rabbit level-up-potion 1"), "Rabbit edit retains level-up potion: " + text);
        }
        policy.Record(Parse("/one rabbit infinite on"));
        policy.Record(Parse("/one rabbit share on"));
        policy.Record(Parse("/one rabbit suppress-survival on"));
        policy.Record(levelUpOff);
        Check(policy.ToPresetText() == "SephiriaOne preset v6\nrabbit infinite 1\nrabbit share 1\nrabbit mp-cost 1\nrabbit suppress-survival 1\nrabbit mp-amount 25\n",
            "Disabling level-up potion preserves every existing setting and restores the earlier schema");
        policy.Record(levelUpOn);
        policy.Record(Parse("/one rabbit reset"));
        Check(!policy.HasChanges && policy.ToPresetText() == "SephiriaOne preset v1\n", "Rabbit reset clears level-up potion");
        policy.Record(levelUpOn); policy.Clear();
        Check(!policy.HasChanges && policy.ToPresetText() == "SephiriaOne preset v1\n", "New session clears level-up potion");
        for (int version = 1; version <= 9; version++)
        {
            string header = "SephiriaOne preset v" + version + "\n";
            Check(SessionPolicy.TryReadPreset(header, out levelUpLoad, out _) && !levelUpLoad.HasChanges,
                "Older versions leave level-up potion off: " + version);
            Check(!SessionPolicy.TryReadPreset(header + "rabbit level-up-potion 1\n", out levelUpLoad, out _) && !levelUpLoad.HasChanges,
                "Older versions reject level-up potion rows atomically: " + version);
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v10\nrabbit level-up-potion 0\n", out levelUpLoad, out _) &&
            !levelUpLoad.HasChanges && levelUpLoad.ToPresetText() == "SephiriaOne preset v1\n", "Explicit level-up potion off normalizes away");
        const string allFamilies = "SephiriaOne preset v10\nfountain multiplier 2\nchoices item 2\nstats luck multiplier 3\nresources leaves multiplier 2\nrabbit infinite 1\nrabbit share 1\nrabbit mp-cost 1\nrabbit suppress-survival 1\nrabbit mp-amount 25\nrabbit level-up-potion 1\nmerchant wandering spawns 1\nmerchant papyrus chance 80\nmerchant papyrus from 3\nmerchant papyrus limit 2\nmerchant papyrus guarantee 0\n";
        Check(SessionPolicy.TryReadPreset(allFamilies, out levelUpLoad, out _) && levelUpLoad.ToPresetText() == allFamilies &&
            levelUpLoad.Merchants.Get("papyrus").Equals(new MerchantSettings(false, 80, 3, 2, false)),
            "v10 preserves all previous setting families including typed merchant caps and guarantee");
        const string guaranteeOnly = "SephiriaOne preset v9\nmerchant wandering guarantee 0\n";
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v10\nrabbit level-up-potion 0\nmerchant wandering guarantee 0\n", out levelUpLoad, out _) &&
            levelUpLoad.ToPresetText() == guaranteeOnly, "Level-up potion off retains existing v9 merchant serialization");
        foreach (bool levelUpFirst in new[] { false, true })
        {
            string levelUpRow = "rabbit level-up-potion 1\n", costRows = "rabbit mp-amount 25\nrabbit mp-cost 0\n";
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v10\n" + (levelUpFirst ? levelUpRow + costRows : costRows + levelUpRow), out levelUpLoad, out _) &&
                levelUpLoad.ToPresetText() == "SephiriaOne preset v10\nrabbit mp-amount 25\nrabbit level-up-potion 1\n",
                "v10 preserves level-up potion and disabled custom MP cost regardless of row order");
        }
        foreach (string row in new[] { "rabbit level-up-potion 2", "rabbit level-up-potion -1", "rabbit level-up-potion 01",
            "rabbit level-up-potion 1.0", "rabbit level-up-potion +1", "rabbit level-up-potion on", "rabbit level-up-potion 1 extra",
            "rabbit level-up-potion 1\nrabbit level-up-potion 0", "rabbit level-up-potion 0\nrabbit level-up-potion 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v10\nrabbit share 1\n" + row + "\n", out levelUpLoad, out _) && !levelUpLoad.HasChanges,
                "Malformed or duplicate level-up potion rows reject v10 atomically: " + row);
        foreach (string suffix in new[] { "", "0", "1", "yes", "on extra", "reset" })
            Check(RabbitCommand.Parse("/one rabbit level-up-potion " + suffix, out _, out _) == RabbitParseResult.Invalid,
                "Reject malformed level-up potion command: " + suffix);
        Check(RabbitCommand.Parse("/one rabbit mp-cost 25", out _, out _) == RabbitParseResult.Valid,
            "Host can set a numeric MP fee in game");
        Check(policy.RabbitPotions.MpCostPerDrink == 10, "Default struct MP cost is ten");
        policy.Record(Parse("/one rabbit mp-cost on"));
        Check(policy.RabbitPotions.ConsumeMp && !policy.RabbitPotions.Infinite && !policy.RabbitPotions.Share && !policy.RabbitPotions.SuppressSurvival,
            "MP cost is independent");
        policy.Record(Parse("/one rabbit suppress-survival on"));
        Check(policy.RabbitPotions.ConsumeMp && policy.RabbitPotions.SuppressSurvival, "Survival suppression is independent");
        string balanced = policy.ToPresetText();
        Check(balanced == "SephiriaOne preset v5\nrabbit mp-cost 1\nrabbit suppress-survival 1\n", "New flags write v5");
        Check(SessionPolicy.TryReadPreset(balanced, out var balancedLoad, out _) && balancedLoad.ToPresetText() == balanced,
            "v5 round trips both new flags");
        policy.Record(Parse("/one rabbit reset"));
        Check(!policy.RabbitPotions.HasChanges, "Reset clears new flags");
        policy.Record(Parse("/one rabbit infinite on"));
        Check(policy.RabbitPotions.Infinite && !policy.RabbitPotions.Share && policy.HasChanges, "Infinite is independent");
        policy.Record(Parse(" /ONE RABBIT SHARE ON "));
        Check(policy.RabbitPotions.Infinite && policy.RabbitPotions.Share, "Both can be enabled");
        string saved = policy.ToPresetText();
        Check(saved == "SephiriaOne preset v4\nrabbit infinite 1\nrabbit share 1\n", "Canonical v4 serialization");
        Check(SessionPolicy.TryReadPreset(saved, out var loaded, out _) && loaded.ToPresetText() == saved, "Both flags round trip");
        policy.Record(Parse("/one rabbit infinite off"));
        Check(!policy.RabbitPotions.Infinite && policy.RabbitPotions.Share, "Turning off one preserves the other");
        policy.Record(Parse("/one rabbit reset"));
        Check(!policy.HasChanges && policy.ToPresetText() == "SephiriaOne preset v1\n", "Reset returns to native defaults");
        policy.Record(Parse("/one rabbit share on")); policy.Clear();
        Check(!policy.RabbitPotions.HasChanges, "New session clears rabbit intent");
        foreach (string text in new[] { "", "/mod rabbit infinite on", "/onefoo rabbit infinite on", "/one save", "/one rabbits share on" })
            Check(RabbitCommand.Parse(text, out _, out _) == RabbitParseResult.NotCommand, "Unrelated namespace: " + text);
        foreach (string text in new[] { "/one rabbit", "/one rabbit help" })
            Check(RabbitCommand.Parse(text, out _, out _) == RabbitParseResult.Help, "Rabbit help: " + text);
        Check(RabbitCommand.Parse("/one rabbit status", out _, out _) == RabbitParseResult.Status, "Status is read-only");
        foreach (string text in new[] { "/one rabbit infinite", "/one rabbit infinite 1", "/one rabbit share yes", "/one rabbit share x2", "/one rabbit reset on", "/one rabbit share on extra", "/one rabbit unknown on" })
            Check(RabbitCommand.Parse(text, out _, out _) == RabbitParseResult.Invalid, "Reject malformed command: " + text);
        foreach (string row in new[] { "rabbit unknown 1", "rabbit infinite 2", "rabbit infinite -1", "rabbit share 0.5", "rabbit share on", "rabbit infinite 1\nrabbit infinite 0", "rabbit share 1 extra" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v4\nrabbit share 1\n" + row, out loaded, out _) && !loaded.HasChanges, "Reject preset atomically: " + row);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v4\nrabbit infinite 0\nrabbit share 0\n", out loaded, out _) && !loaded.HasChanges, "Explicit off rows restore defaults");
        foreach (int version in new[] { 1, 2, 3 })
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out loaded, out _) && !loaded.HasChanges, "Old preset still accepted");
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\nrabbit infinite 1\n", out _, out _), "Old schema rejects new rows");
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v4\nstats luck multiplier 3\nresources leaves multiplier 2\nchoices item 2\nrabbit infinite 1\n", out loaded, out _) && loaded.RabbitPotions.Infinite && loaded.DescribeSettings().Count == 4, "v4 preserves all existing families");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v4\nrabbit mp-cost 1\n", out loaded, out _) && !loaded.HasChanges,
            "v4 rejects new options atomically");
        foreach (string row in new[] { "rabbit mp-cost 2", "rabbit mp-cost 01", "rabbit suppress-survival -1", "rabbit suppress-survival 1.0", "rabbit mp-cost 1\nrabbit mp-cost 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v5\nrabbit infinite 1\n" + row, out loaded, out _) && !loaded.HasChanges,
                "v5 rejects malformed row atomically: " + row);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v5\nresources leaves multiplier 2\nrabbit share 1\nrabbit mp-cost 1\n", out loaded, out _) &&
            loaded.RabbitPotions.Share && loaded.RabbitPotions.ConsumeMp && loaded.Resources.HasChanges,
            "v5 accepts previous families and new flags");
        policy.Clear();
        policy.Record(Parse("/one rabbit mp-cost 25"));
        Check(policy.RabbitPotions.ConsumeMp && policy.RabbitPotions.MpCostPerDrink == 25 &&
            !policy.RabbitPotions.Infinite && !policy.RabbitPotions.SuppressSurvival, "Amount enables only MP charging");
        Check(policy.ToPresetText() == "SephiriaOne preset v6\nrabbit mp-cost 1\nrabbit mp-amount 25\n", "Custom cost uses v6");
        policy.Record(Parse("/one rabbit mp-cost off"));
        Check(!policy.RabbitPotions.ConsumeMp && policy.HasChanges && policy.RabbitPotions.MpCostPerDrink == 25,
            "Off retains custom amount and save eligibility");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out loaded, out _) && !loaded.RabbitPotions.ConsumeMp &&
            loaded.RabbitPotions.MpCostPerDrink == 25, "Disabled custom cost round trips");
        policy.Record(Parse("/one rabbit mp-cost on"));
        policy.Record(Parse("/one rabbit share on"));
        policy.Record(Parse("/one rabbit suppress-survival on"));
        Check(policy.RabbitPotions.ConsumeMp && policy.RabbitPotions.MpCostPerDrink == 25, "Toggle changes preserve custom cost");
        foreach (int amount in new[] { 0, 1, 10000 })
        {
            var command = Parse("/one rabbit mp-cost " + amount);
            Check(!command.IsReset, "Numeric cost is a mutation even for zero");
            policy.Record(command);
            Check(policy.RabbitPotions.ConsumeMp && policy.RabbitPotions.MpCostPerDrink == amount, "Exact integer cost " + amount);
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out loaded, out _) && loaded.RabbitPotions.MpCostPerDrink == amount,
                "Cost boundary round trips " + amount);
        }
        foreach (string amount in new[] { "-1", "10001", "2147483648", "1.5", "1e2", "x2", "+5", "NaN", "1,000" })
            Check(RabbitCommand.Parse("/one rabbit mp-cost " + amount, out _, out _) == RabbitParseResult.Invalid, "Reject invalid fee " + amount);
        foreach (int version in new[] { 1, 2, 3, 4, 5 })
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out loaded, out _) &&
                loaded.RabbitPotions.MpCostPerDrink == 10, "Old presets default to ten");
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\nrabbit mp-amount 25\n", out loaded, out _) &&
                !loaded.HasChanges, "Old versions reject amount row");
        }
        foreach (string toggle in new[] { "0", "1" })
            foreach (bool amountFirst in new[] { false, true })
            {
                string amountRow = "rabbit mp-amount 25\n", toggleRow = "rabbit mp-cost " + toggle + "\n";
                Check(SessionPolicy.TryReadPreset("SephiriaOne preset v6\n" + (amountFirst ? amountRow + toggleRow : toggleRow + amountRow), out loaded, out _) &&
                    loaded.RabbitPotions.MpCostPerDrink == 25 && loaded.RabbitPotions.ConsumeMp == (toggle == "1"), "Preset row order preserves fee and toggle");
            }
        foreach (string row in new[] { "rabbit mp-amount -1", "rabbit mp-amount 10001", "rabbit mp-amount 01", "rabbit mp-amount 1.0",
            "rabbit mp-amount +1", "rabbit mp-amount 25\nrabbit mp-amount 20", "rabbit mp-cost 25", "rabbit mp-amount 25 extra" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v6\nrabbit share 1\n" + row, out loaded, out _) && !loaded.HasChanges,
                "Reject bad cost preset atomically " + row);
        policy.Record(Parse("/one rabbit reset"));
        Check(!policy.HasChanges && policy.RabbitPotions.MpCostPerDrink == 10, "Reset clears custom fee to ten");
        return checks;
    }
}
