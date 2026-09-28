using SephiriaOne;

internal static class MerchantSettingsTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string scenario)
        { if (!value) throw new Exception(scenario); checks++; }
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
