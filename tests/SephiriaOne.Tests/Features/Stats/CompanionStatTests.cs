using SephiriaOne;

internal static class CompanionStatTests
{
    internal static readonly (string Name, string Key, int Scale, int Offset, int Min, int Max)[] Expected = {
        ("companionattackspeed", "FOLLOWERATTACKSPEED", 1, 100, 1, 1000),
        ("companiondamage", "FOLLOWERDAMAGE", 1, 100, 0, 10000),
        ("companiondefense", "FOLLOWERDEFENSE", 1, 0, 0, 10000),
        ("companioncritical", "FOLLOWERCRITICAL", 100, 0, 0, 100),
        ("companioncriticalshare", "FOLLOWERCRITICALCONTRIBUTE", 1, 0, 0, 100),
        ("companionrevive", "FOLLOWERREVIVE", 1, 100, 1, 1000)
    };

    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        foreach (var (name, key, scale, offset, min, max) in Expected)
        {
            var stat = StatCatalog.Find(name);
            Check(stat != null, "Missing companion modifier " + name);
            Check(stat!.Key == key && stat.Scale == scale && stat.Offset == offset && stat.Minimum == min && stat.Maximum == max,
                "Audited companion units and bounds " + name);
            Check(stat.IsCompanion && stat.MenuId.Length == 0, "Companion bonuses do not claim native C-menu rows " + name);
            string alias = "follower" + (name == "companioncriticalshare" ? "criticalcontribute" : name.Substring("companion".Length));
            Check(StatCatalog.Find(alias.ToUpperInvariant()) == stat, "Native follower alias " + name);
            foreach (string syntax in new[] { "40", "set 40", "+10", "-5", "add 10", "sub 5", "x2", "reset" })
                Check(StatCommand.Parse("/stats " + name + " " + syntax, out var command, out _) == StatParseResult.Valid && command.Stat == stat,
                    "Shared command syntax " + name + " " + syntax);
            Check(StatCommand.Parse("/stats " + name + " 12.25", out _, out _) ==
                (scale == 100 ? StatParseResult.Valid : StatParseResult.Invalid), "Only critical chance uses fractional points " + name);
            var policy = new SessionPolicy();
            var multiply = new StatCommand(stat, StatOperation.Multiply, 2);
            var native = new StatSnapshot(stat, 20 * scale, 0, 4 * scale, 25);
            Check(policy.TryPlanStatCommand(multiply, new[] { native }, out var updates, out _), "Plan companion multiplier " + name);
            Check(!updates[0].UsesNativeFallback && stat.Display((int)((updates[0].Raw + 4 * scale) * 1.25f)) == (30 + offset) * 2,
                "Owner-specific native baseline with amplification " + name);
            policy.Record(multiply);
            var applied = new StatSnapshot(stat, updates[0].Raw, updates[0].Contribution, 4 * scale, 25);
            Check(policy.TryPlanRelativeStat(applied, out var repeat, out _) && repeat.Raw == updates[0].Raw,
                "Repeated reconciliation cannot compound " + name);
            var penalty = new StatSnapshot(stat, (10 - offset) * scale, 20 * scale, 0, 0);
            Check(policy.TryPlanRelativeStat(penalty, out var fallback, out _) && fallback.UsesNativeFallback &&
                fallback.Raw == (-10 - offset) * scale && fallback.Contribution == 0, "Invalid native multiplier target falls back exactly " + name);
            Check(StatPlanner.TryPlan(new StatCommand(stat, StatOperation.Reset, 0), new[] { applied }, out var reset, out _) &&
                reset[0].Raw == 20 * scale && reset[0].Contribution == 0, "Reset removes only owned contribution " + name);
            Check(!StatPlanner.TryPlan(new StatCommand(stat, StatOperation.Set, max + 1), new[] { native }, out _, out _),
                "Unsafe absolute result rejected " + name);
            string text = policy.ToPresetText();
            Check(text.StartsWith("SephiriaOne preset v21\n") && SessionPolicy.TryReadPreset(text, out var saved, out _) && saved.ToPresetText() == text,
                "Companion preset roundtrip " + name);
            for (int version = 1; version <= 20; version++)
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nstats {name} multiplier 2\n", out _, out _),
                    "Older schema rejects companion setting " + name);
            policy.Record(new StatCommand(stat, StatOperation.Reset, 0));
            Check(policy.ToPresetText() == "SephiriaOne preset v1\n", "Reset returns minimal schema " + name);
        }
        var mixed = new SessionPolicy();
        Check(StatCatalog.All.Where(s => s.IsCompanion).Select(s => s.Name).ToHashSet().SetEquals(Expected.Select(s => s.Name)),
            "Only the six audited companion bonuses extend the menu allowlist");
        mixed.Record(new StatCommand(StatCatalog.Find("companioncritical"), StatOperation.Set, 12.25m));
        mixed.Record(new StatCommand(StatCatalog.Find("luck"), StatOperation.Add, 10));
        mixed.RecordTournamentDuration(45);
        Check(SessionPolicy.TryReadPreset(mixed.ToPresetText(), out var restored, out _) && restored.ToPresetText() == mixed.ToPresetText() &&
            restored.TournamentDuration == 45, "v21 preserves older settings and decimal companion critical chance");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v21\nstats followercritical set 10\n", out _, out _),
            "Presets only store canonical companion names");
        var chance = StatCatalog.Find("companioncritical")!;
        Check(StatPlanner.TryPlan(new StatCommand(chance, StatOperation.Set, 12.25m),
            new[] { new StatSnapshot(chance, 0, 0, 0, 0) }, out var fraction, out _) && fraction[0].Raw == 1225,
            "Fractional critical chance converts to native hundredths");
        return checks;
    }
}
