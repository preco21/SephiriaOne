using System.Globalization;
using SephiriaOne;

internal static class MultiplierCommandTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }
        StatCommand Stats(string input)
        {
            Check(StatCommand.Parse("/stats " + input, out var result, out _) == StatParseResult.Valid,
                "Parse stat multiplier: " + input);
            return result;
        }
        FountainCommand Fountain(string input)
        {
            Check(FountainCommand.Parse("/fountain " + input, out var result, out _) == FountainParseResult.Valid,
                "Parse Fountain multiplier: " + input);
            return result;
        }
        var luck = StatCatalog.Find("luck")!;
        var policy = new SessionPolicy();
        var command = Stats("luck x3");
        var native = new[] { new StatSnapshot(luck, 10, 0, 0, 0), new StatSnapshot(luck, 20, 0, 0, 0) };
        Check(policy.TryPlanStatCommand(command, native, out var changes, out _) &&
            changes[0].Raw == 30 && changes[1].Raw == 60, "Multiply each character's distinct baseline");
        policy.Record(command);
        var modified = new[] { new StatSnapshot(luck, 30, 20, 0, 0), new StatSnapshot(luck, 60, 40, 0, 0) };
        Check(policy.TryPlanStatCommand(command, modified, out changes, out _) && changes[0].Raw == 30 && changes[1].Raw == 60,
            "Repeated multiplier excludes existing addon contributions");
        Check(policy.TryPlanStatCommand(Stats("luck x2"), modified, out changes, out _) && changes[0].Raw == 20,
            "Another multiplier replaces the factor");
        Check(policy.TryPlanRelativeStat(new StatSnapshot(luck, 32, 20, 3, 100), out var maintained, out _) &&
            (maintained.Raw + 3) * 2 == 90 && maintained.Raw - maintained.Contribution == 12,
            "Multiplier follows native raw, bonus and amplifier changes without absorbing stale adjustment");
        Check(policy.TryPlanStatCommand(Stats("luck +5"), modified, out changes, out _) && changes[0].Raw == 15,
            "Delta after multiplier begins a new native offset");
        policy.Record(Stats("luck +5"));
        Check(policy.TryPlanStatCommand(Stats("luck +2"), new[] { new StatSnapshot(luck, 15, 5, 0, 0) }, out changes, out _) && changes[0].Raw == 17,
            "Later deltas continue accumulating");
        Check(policy.TryPlanStatCommand(Stats("luck set 100"), modified, out changes, out _) && changes[0].Raw == 100,
            "Set switches away from multiplier");
        foreach (var input in new[] { "luck X3", "luck set x3", "luck x1.50", "luck x0", "luck x10000" }) Stats(input);
        foreach (var input in new[] { "x", "x-1", "x+2", "x1.001", "x1e2", "x1,5", "xx3", "xNaN", "x10001", "x99999999999999999999999999999" })
        {
            Check(StatCommand.Parse("/stats luck " + input, out _, out _) == StatParseResult.Invalid, "Reject stat factor " + input);
            Check(FountainCommand.Parse("/fountain " + input, out _, out _) == FountainParseResult.Invalid, "Reject Fountain factor " + input);
        }
        foreach (string op in new[] { "add", "sub" })
        {
            Check(StatCommand.Parse("/stats luck " + op + " x3", out _, out _) == StatParseResult.Invalid, "Reject conflicting stat operation");
            Check(FountainCommand.Parse("/fountain " + op + " x3", out _, out _) == FountainParseResult.Invalid, "Reject conflicting Fountain operation");
        }
        Check(ChoiceCommand.Parse("/choices all x3", out _, out var choiceError) == ChoiceParseResult.Invalid &&
            choiceError.Contains("extra") && choiceError.Contains("multiplier", StringComparison.OrdinalIgnoreCase),
            "Choices explain why extra-candidate counts do not support baseline multiplication");

        foreach (var (name, raw, factor, expected) in new[]
        {
            ("luck", 10, "1.5", 15), ("critical", 1250, "2", 2500),
            ("attackspeed", 0, "2", 100), ("criticaldamage", 0, "3", 100)
        })
        {
            var stat = StatCatalog.Find(name)!;
            Check(policy.TryPlanStatCommand(Stats(name + " x" + factor), new[] { new StatSnapshot(stat, raw, 0, 0, 0) }, out changes, out _) &&
                changes[0].Raw == expected, "Multiply displayed units and offsets: " + name);
        }
        Check(!policy.TryPlanStatCommand(Stats("luck x1.5"), new[] { new StatSnapshot(luck, 3, 0, 0, 0) }, out changes, out _) && changes.Length == 0,
            "Reject fractional integer result rather than round");
        Check(!policy.TryPlanStatCommand(Stats("luck x2"), new[] { native[0], new StatSnapshot(luck, 6000, 0, 0, 0) }, out changes, out _) && changes.Length == 0,
            "One out-of-bounds player rejects whole batch");
        Check(!policy.TryPlanStatCommand(Stats("luck x1.5"), new[] { new StatSnapshot(luck, 1, 0, 0, 100) }, out changes, out _),
            "Reject integer display target unreachable through native amplifier");
        command = Stats("luck x1");
        Check(policy.TryPlanStatCommand(command, new[] { new StatSnapshot(luck, 20010, 10, 0, -100) }, out changes, out _) &&
            changes[0].Raw == 20000 && changes[0].Contribution == 0, "Identity restores exact baseline outside bounds even with invalid native amplifier");
        policy.Record(command);
        Check(!policy.HasChanges, "Identity factor removes retained stat policy");

        var fountain = Fountain("x3");
        Check(fountain.TryPlanTracked(new[] { 14, 22 }, new[] { 10, 20 }, 12, null, null, out var plan, out _) &&
            plan.Points.SequenceEqual(new[] { 12, 6 }) && plan.Contributions.SequenceEqual(new[] { 8, 4 }),
            "Fountain multiplier removes previous contributions before multiplying");
        Check(Fountain("x1.5").TryPlanTracked(new[] { 4 }, new[] { 0 }, 12, null, null, out plan, out _) && plan.Points[0] == 6,
            "Exact fractional Fountain factor supported");
        Check(!Fountain("x1.5").TryPlanTracked(new[] { 3 }, new[] { 0 }, 12, null, null, out plan, out _),
            "Fractional Fountain points rejected");
        Check(!Fountain("x3").TryPlanTracked(new[] { int.MaxValue }, new[] { 0 }, 12, null, null, out plan, out _),
            "Fountain overflow rejected");
        Check(Fountain("x1").TryPlanTracked(new[] { 100 }, new[] { 96 }, 100, 12, 100, out plan, out _) &&
            plan.Points[0] == 4 && plan.Contributions[0] == 0 && plan.Limit == 12 && !plan.AppliedLimit.HasValue,
            "Identity Fountain factor resets contribution and owned carryover limit");

        policy.Record(Stats("luck x3"));
        policy.Record(Fountain("X1.5"));
        string text = policy.ToPresetText();
        Check(text.StartsWith("SephiriaOne preset v2\n") && text.Contains("stats luck multiplier 3") && text.Contains("fountain multiplier 1.5"),
            "New preset version stores factors without flattening them");
        Check(SessionPolicy.TryReadPreset(text, out var loaded, out _) && loaded.ToPresetText() == text, "Multiplier policy round trips");
        var join = new SessionPlayerSnapshot(new Dictionary<string, int> { ["LUCK"] = 7 },
            new Dictionary<string, int>(), new Dictionary<string, int>(), 6, 0, 12, null, null, true);
        Check(loaded.TryPlan(join, out var joined, out _) && joined.Fountain!.Points[0] == 9 && joined.Stats.Single().Raw == 21,
            "Saved multipliers use joining character's baseline");
        var negative = new SessionPlayerSnapshot(new Dictionary<string, int>(), new Dictionary<string, int>(),
            new Dictionary<string, int>(), -1, 0, 12, null, null, true);
        var legacy = new SessionPolicy();
        legacy.Record(Fountain("set 100"));
        Check(!legacy.TryPlan(negative, out _, out _), "Preserve legacy rejection of a negative joining Fountain baseline");
        foreach (string bad in new[]
        {
            "SephiriaOne preset v1\nstats luck multiplier 3", "SephiriaOne preset v2\nfountain multiplier -1",
            "SephiriaOne preset v2\nstats luck multiplier 1.001", "SephiriaOne preset v2\nstats luck multiplier 10001"
        }) Check(!SessionPolicy.TryReadPreset(bad, out loaded, out _) && !loaded.HasChanges, "Reject malformed multiplier preset");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v2\nfountain multiplier 1\nstats luck multiplier 1", out loaded, out _) && !loaded.HasChanges,
            "Saved identity multipliers normalize away");
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(Stats("luck x1.5").Amount == 1.5m && Fountain("set x1.5").Amount == 1.5m,
                "Multiplier input remains invariant across cultures");
        }
        finally { CultureInfo.CurrentCulture = before; }
        return checks;
    }
}
