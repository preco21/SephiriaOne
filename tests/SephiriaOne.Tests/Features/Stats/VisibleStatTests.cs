using SephiriaOne;

internal static class VisibleStatTests
{
    // Audit of current C-menu rows, separate from the implementation catalog.
    internal static readonly (string Name, string Key, int Offset, int Max)[] Added = {
        ("toughness", "TOUGHNESS", 0, 10000), ("dashrecovery", "DASHRECOVERY", 100, 1000),
        ("expdrop", "EXPDROP", 100, 10000), ("leafdrop", "MONEYDROP", 100, 10000),
        ("thorns", "THORNS", 0, 10000), ("normaldamage", "BASICATTACKDAMAGEBONUS", 100, 10000),
        ("dashdamage", "DASHATTACKDAMAGEBONUS", 100, 10000), ("specialdamage", "SPECIALATTACKDAMAGEBONUS", 100, 10000),
        ("weapondamage", "FINALWEAPONDAMAGE", 100, 10000), ("grimoiredamage", "MAGICDAMAGEBONUS", 100, 10000),
        ("alldamage", "ALLDAMAGEBONUS", 100, 10000), ("hpsteal", "HPSTEAL", 0, 10000),
        ("mpsteal", "MPSTEAL", 0, 10000), ("ignoredefense", "IGNOREDEFENSE", 0, 100),
        ("debuffduration", "DEBUFFDURATION", 0, 10000), ("debuffdamage", "DEBUFFDAMAGE", 0, 10000),
        ("crossbowreload", "CROSSBOWRELOADSPEED", 100, 1000)
    };

    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        foreach (var (name, key, offset, maximum) in Added)
        {
            var stat = StatCatalog.Find(name);
            Check(stat != null, "Missing visible stat " + name);
            Check(stat!.Key == key && stat.Offset == offset && stat.Scale == 1 && stat.Maximum == maximum, "Menu units/key " + name);
            foreach (string syntax in new[] { "40", "set 40", "+10", "-5", "add 10", "sub 5", "x2", "reset" })
                Check(StatCommand.Parse("/stats " + name + " " + syntax, out var parsed, out _) == StatParseResult.Valid && parsed.Stat == stat, "Shared syntax " + name + ": " + syntax);
            Check(StatCommand.Parse("/stats " + name + " 0.1", out _, out _) == StatParseResult.Invalid, "Whole menu units " + name);
            var policy = new SessionPolicy();
            var twice = new StatCommand(stat, StatOperation.Multiply, 2);
            var native = new StatSnapshot(stat, 20, 0, 4, 25);
            Check(policy.TryPlanStatCommand(twice, new[] { native }, out var writes, out _), "Multiplier plans " + name);
            // Ignore defense's bounded chance cannot reach 2 * 30? It can (60%).
            Check(!writes[0].UsesNativeFallback && stat.Display((int)((writes[0].Raw + 4) * 1.25f)) == (30 + offset) * 2,
                "Multiplier targets this character's native displayed value " + name);
            policy.Record(twice);
            var applied = new StatSnapshot(stat, writes[0].Raw, writes[0].Contribution, 4, 25);
            Check(policy.TryPlanRelativeStat(applied, out var repeated, out _) && repeated.Raw == writes[0].Raw, "Repeated sync does not compound " + name);
            var penalty = new StatSnapshot(stat, 10 - offset, 20, 0, 0);
            Check(policy.TryPlanRelativeStat(penalty, out var fallback, out _) && fallback.UsesNativeFallback && fallback.Raw == -10 - offset && fallback.Contribution == 0,
                "Penalty fallback preserves exact native stat " + name);
            string preset = policy.ToPresetText();
            Check(preset.StartsWith("SephiriaOne preset v17\n") && SessionPolicy.TryReadPreset(preset, out var saved, out _) && saved.ToPresetText() == preset, "Expanded stat preset roundtrip " + name);
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v16\nstats " + name + " multiplier 2\n", out _, out _), "Older schema rejects expanded stat " + name);
            Check(StatPlanner.TryPlan(new StatCommand(stat, StatOperation.Reset, 0), new[] { applied }, out var reset, out _) && reset[0].Raw == 20 && reset[0].Contribution == 0, "Reset preserves original raw " + name);
        }
        var original = new[] { "luck", "defense", "attackspeed", "critical", "criticaldamage", "evasion", "cooldown", "mpregen", "negotiation", "truedamage" };
        Check(StatCatalog.All.Select(s => s.Name).ToHashSet().SetEquals(original.Concat(Added.Select(a => a.Name))), "Only audited menu stats are public");
        Check(StatCatalog.All.Select(s => s.Key).Distinct().Count() == StatCatalog.All.Count, "No duplicate ownership keys");
        foreach (string name in new[] { "physicaldamage", "firedamage", "icedamage", "lightningdamage", "hpregen", "magiccritical", "hppotionbonus", "mppotionbonus", "dashcount", "weaponrange", "darkcloud", "flameground", "infinitymp", "CROSSBOWAMMO" })
            Check(StatCommand.Parse("/stats " + name + " 10", out _, out _) == StatParseResult.Invalid, "Unexposed/internal/composite stat rejected " + name);
        foreach (var (alias, name) in new[] { ("lifesteal", "hpsteal"), ("mp-steal", "mpsteal"), ("magicdamage", "grimoiredamage"),
            ("penetration", "ignoredefense"), ("basicdamage", "normaldamage"), ("crossbowreloadspeed", "crossbowreload") })
            Check(StatCatalog.Find(alias.ToUpperInvariant()) == StatCatalog.Find(name), "Alias resolves only audited key " + alias);
        var mixed = new SessionPolicy();
        mixed.Record(new StatCommand(StatCatalog.Find("hpsteal"), StatOperation.Add, 3));
        mixed.RecordCollin(true); mixed.RecordEventSpawns(new EventSpawnSettings(2));
        mixed.Record(new StatCommand(StatCatalog.Find("luck"), StatOperation.Multiply, 2));
        Check(SessionPolicy.TryReadPreset(mixed.ToPresetText(), out var combined, out _) && combined.CollinStartingArtifact && combined.EventSpawns.Multiplier == 2 &&
            combined.ToPresetText() == mixed.ToPresetText(), "v17 retains existing feature settings atomically");
        mixed.Record(new StatCommand(StatCatalog.Find("hpsteal"), StatOperation.Reset, 0));
        Check(mixed.ToPresetText().StartsWith("SephiriaOne preset v16\n"), "Reset returns preset to lowest needed schema");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v1\nstats luck set 15\n", out var old, out _) && old.ToPresetText().StartsWith("SephiriaOne preset v1\n"), "Original presets remain unchanged");
        return checks;
    }
}
