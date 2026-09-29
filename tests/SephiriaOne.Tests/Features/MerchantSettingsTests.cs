using SephiriaOne;

internal static class MerchantSettingsTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string scenario)
        { if (!value) throw new Exception(scenario); checks++; }
        Check(MerchantCommand.Parse("/one merchant papyrus on", out _, out _) == MerchantParseResult.Valid,
            "A selected merchant type can be enabled independently");
        const string variants = "SephiriaOne preset v8\nmerchant wandering spawns 1\nmerchant papyrus chance 80\nmerchant papyrus from 3\nmerchant papyrus limit 2\n";
        Check(SessionPolicy.TryReadPreset(variants, out var variantPolicy, out _) && variantPolicy.ToPresetText() == variants,
            "Independent merchant variants and conditions round trip in v8");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v7\nmerchant papyrus spawns 1\n", out _, out _),
            "Older merchant schema cannot silently enable a new variant");
        foreach (string command in new[] { "papyrus from 0", "papyrus from 1001", "papyrus limit -1", "papyrus limit 1001", "missing on", "papyrus chance 101" })
            Check(MerchantCommand.Parse("/one merchant " + command, out _, out _) == MerchantParseResult.Invalid,
                "Invalid selected merchant setting is rejected: " + command);
        var independent = new SessionPolicy();
        void Apply(string text)
        {
            Check(MerchantCommand.Parse("/one merchant " + text, out var command, out _) == MerchantParseResult.Valid,
                "Valid merchant command: " + text);
            independent.Record(command);
        }
        Check(MerchantCatalog.All.Select(definition => definition.Id).Distinct().Count() == MerchantCatalog.All.Count &&
            MerchantCatalog.All.Select(definition => definition.SeedSalt).Distinct().Count() == MerchantCatalog.All.Count &&
            MerchantCatalog.Find("WANDERING")!.Id == MerchantCatalog.DefaultId && MerchantCatalog.All.All(definition => definition.HasGuarantee),
            "Catalog ids and random salts are independent, case insensitive, and each type has a guarantee");
        Check(!independent.Merchants.AnyEnabled && !independent.HasChanges && MerchantCatalog.All.All(definition =>
            independent.Merchants.Get(definition.Id).Equals(MerchantSettings.Defaults(definition))),
            "Every new merchant type defaults off with its own default settings");
        foreach (string text in new[] { "on", "chance 50", "papyrus on", "papyrus chance 80", "papyrus from 3", "papyrus limit 2", "taz chance 0" }) Apply(text);
        Check(independent.MerchantSpawns && independent.MerchantSpawnChance == 50 && independent.Merchants.Get("papyrus").Equals(new MerchantSettings(true, 80, 3, 2)) &&
            independent.Merchants.Get("taz").Equals(new MerchantSettings(false, 0)), "Type commands preserve the settings of every other type");
        var retained = independent.Merchants.Snapshot;
        Apply("papyrus off");
        Check(independent.Merchants.Get("papyrus").Equals(new MerchantSettings(false, 80, 3, 2)) &&
            retained["papyrus"].Enabled, "Off retains chance and conditions, and existing policy snapshots cannot change");
        Apply("reset");
        Check(!independent.MerchantSpawns && independent.MerchantSpawnChance == 25 && independent.HasChanges &&
            independent.Merchants.Get("papyrus").FirstFloor == 3, "Legacy reset affects only Wandering");
        Apply("papyrus reset");
        Check(independent.Merchants.Get("papyrus").Equals(MerchantSettings.Defaults(MerchantCatalog.Find("papyrus")!)) &&
            independent.Merchants.Get("taz").Chance == 0, "Selected reset restores only that type's defaults");
        Check(SessionPolicy.TryReadPreset(independent.ToPresetText(), out var copied, out _), "Preset creates an independent policy copy");
        independent.Clear();
        Check(!independent.HasChanges && !independent.Merchants.AnyEnabled && copied.Merchants.Get("taz").Chance == 0 &&
            retained["wandering"].Enabled, "Clear does not mutate older snapshots or independently loaded policies");
        Check(MerchantCommand.Parse(" /ONE MERCHANT PaPyRuS StAtUs ", out var status, out _) == MerchantParseResult.Status &&
            status.TypeId == "papyrus" && !status.AllTypes && MerchantCommand.Parse("/one merchant status", out status, out _) == MerchantParseResult.Status && status.AllTypes,
            "Selected status has a canonical type while bare status covers all types");
        foreach (var definition in MerchantCatalog.All)
        {
            foreach (string text in new[] { "on", "off", "reset", "chance 0", "chance 100", "from 1", "from 1000", "limit 0", "limit 1000" })
                Check(MerchantCommand.Parse("/one merchant " + definition.Id + " " + text, out var selected, out _) == MerchantParseResult.Valid && selected.TypeId == definition.Id,
                    "Every registered type supports the same command bounds: " + definition.Id + " " + text);
        }
        foreach (string row in new[] { "merchant missing spawns 1", "merchant Papyrus chance 10", "merchant papyrus from 0", "merchant papyrus from 1001",
            "merchant papyrus limit -1", "merchant papyrus limit 1001", "merchant papyrus chance 01", "merchant papyrus from 1.0", "merchant papyrus unknown 1",
            "merchant papyrus chance 20\nmerchant papyrus chance 30", "merchant spawns 1\nmerchant wandering spawns 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v8\nrabbit share 1\n" + row + "\n", out var rejected, out _) && !rejected.HasChanges,
                "Malformed or duplicate typed rows reject the entire v8 preset: " + row);
        for (int version = 1; version <= 8; version++)
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out var empty, out _) && !empty.HasChanges &&
                MerchantCatalog.All.All(definition => !empty.Merchants.Get(definition.Id).Enabled), "All existing versions default newly added types off: " + version);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v8\nmerchant papyrus chance 25\nmerchant papyrus from 1\nmerchant papyrus limit 0\nmerchant papyrus spawns 0\n", out var normalized, out _) &&
            !normalized.HasChanges && normalized.ToPresetText() == "SephiriaOne preset v1\n", "Default v8 settings normalize away");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v8\nmerchant wandering chance 75\n", out normalized, out _) &&
            normalized.ToPresetText() == "SephiriaOne preset v7\nmerchant chance 75\n", "Wandering-only settings retain backward-compatible v7 export");
        const string chanceOnly = "SephiriaOne preset v7\nmerchant chance 75\n";
        Check(SessionPolicy.TryReadPreset(chanceOnly, out var chancePolicy, out _) && chancePolicy.HasChanges &&
            chancePolicy.ToPresetText() == chanceOnly, "Custom spawn chance persists while the merchant toggle is off");
        foreach (int chance in new[] { 0, 1, 25, 100 })
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v7\nmerchant chance " + chance + "\n", out chancePolicy, out _) &&
                (chance == 25 ? !chancePolicy.HasChanges : chancePolicy.ToPresetText().Contains("merchant chance " + chance)),
                "Chance boundaries round trip and the default normalizes away: " + chance);
        }
        foreach (string chance in new[] { "-1", "101", "01", "1.0", "+1", "25%", "on", "1e2" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v7\nmerchant spawns 1\nmerchant chance " + chance + "\n", out chancePolicy, out _) && !chancePolicy.HasChanges,
                "Malformed chance rejects the whole preset: " + chance);
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v7\nmerchant chance 40\nmerchant chance 50\n", out chancePolicy, out _) && !chancePolicy.HasChanges,
            "Duplicate chance rows reject the whole preset");
        foreach (bool chanceFirst in new[] { false, true })
        {
            string rows = chanceFirst ? "merchant chance 75\nmerchant spawns 1\n" : "merchant spawns 1\nmerchant chance 75\n";
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v7\n" + rows, out chancePolicy, out _) &&
                chancePolicy.ToPresetText() == "SephiriaOne preset v7\nmerchant spawns 1\nmerchant chance 75\n", "Chance and toggle rows are order independent");
        }
        const string enabled = "SephiriaOne preset v7\nmerchant spawns 1\n";
        Check(SessionPolicy.TryReadPreset(enabled, out var policy, out _), "v7 accepts merchant spawns");
        Check(policy.HasChanges && policy.DescribeSettings().Contains("merchant spawns 1") && policy.ToPresetText() == enabled,
            "Enabled merchant setting round trips canonically");
        policy.Clear();
        Check(!policy.HasChanges && policy.ToPresetText() == "SephiriaOne preset v1\n", "Clearing the session restores merchant default off");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v7\nmerchant spawns 0\n", out policy, out _) &&
            !policy.HasChanges && policy.ToPresetText() == "SephiriaOne preset v1\n", "Explicit off does not force v7 output");
        for (int version = 1; version <= 6; version++)
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out policy, out _) && !policy.HasChanges,
                "Older version remains valid with merchant off: " + version);
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\nmerchant spawns 1\n", out policy, out _) && !policy.HasChanges,
                "Older schema rejects merchant rows: " + version);
        }
        foreach (string row in new[] { "merchant spawns 2", "merchant spawns -1", "merchant spawns 01", "merchant spawns 1.0",
            "merchant spawns +1", "merchant spawns on", "merchant unknown 1", "merchant spawns 1 extra", "merchant 1",
            "merchant spawns 1\nmerchant spawns 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v7\nrabbit share 1\n" + row, out policy, out _) && !policy.HasChanges,
                "Bad merchant rows reject the whole preset: " + row);
        const string combined = "SephiriaOne preset v7\nfountain multiplier 2\nchoices item 2\nstats luck multiplier 3\nresources leaves multiplier 2\nrabbit share 1\nrabbit mp-cost 1\nrabbit mp-amount 25\nmerchant spawns 1\n";
        Check(SessionPolicy.TryReadPreset(combined, out policy, out _) && policy.ToPresetText() == combined &&
            policy.RabbitPotions.Share && policy.RabbitPotions.ConsumeMp && policy.RabbitPotions.MpCostPerDrink == 25,
            "v7 preserves all existing families including Rabbit custom fee");
        return checks;
    }
}
