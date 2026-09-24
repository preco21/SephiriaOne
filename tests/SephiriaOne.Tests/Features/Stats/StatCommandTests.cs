using SephiriaOne;

internal static class StatCommandTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }
        StatCommand Parse(string text)
        {
            Check(StatCommand.Parse(text, out var command, out _) == StatParseResult.Valid, text);
            return command;
        }
        StatSnapshot Snapshot(string name, int raw, int contribution = 0, int bonus = 0, int amplifier = 0)
            => new(StatCatalog.Find(name)!, raw, contribution, bonus, amplifier);

        foreach (string? text in new[] { null, "", "hello", "/statsish luck 1", "say /stats luck 1", "/fountain 1", "/choices all 1" })
            Check(StatCommand.Parse(text, out _, out _) == StatParseResult.NotCommand, "Ordinary chat passes through");
        foreach (string text in new[] { "/stats", " /STATS HELP " })
            Check(StatCommand.Parse(text, out _, out _) == StatParseResult.Help, "Help: " + text);
        Check(StatCommand.Parse("/stats list", out _, out _) == StatParseResult.List, "Discoverable stat list");
        foreach (var test in new (string Text, string? Name, StatOperation Operation, decimal Amount)[] {
            ("/stats luck 100", "luck", StatOperation.Set, 100),
            (" /STATS\tLUCK +10 ", "luck", StatOperation.Add, 10),
            ("/stats luck -5", "luck", StatOperation.Subtract, 5),
            ("/stats crit set 12.34", "critical", StatOperation.Set, 12.34m),
            ("/stats evasion add 1.5", "evasion", StatOperation.Add, 1.5m),
            ("/stats armor sub 10", "defense", StatOperation.Subtract, 10),
            ("/stats attack-speed subtract 50", "attackspeed", StatOperation.Subtract, 50),
            ("/stats luck reset", "luck", StatOperation.Reset, 0),
            ("/stats reset", null, StatOperation.Reset, 0),
            ("/stats all reset", null, StatOperation.Reset, 0) })
        {
            var command = Parse(test.Text);
            Check(command.Stat?.Name == test.Name && command.Operation == test.Operation && command.Amount == test.Amount,
                "Parsed values: " + test.Text);
        }
        foreach (string text in new[] { "/stats luck", "/stats hp 1", "/stats SEPHIRIAONE_STAT_LUCK 1", "/stats all 5",
            "/stats luck 1.5", "/stats critical 0.001", "/stats luck 1e2", "/stats luck 0x10", "/stats luck NaN",
            "/stats luck +", "/stats luck --1", "/stats luck set -1", "/stats luck add +1", "/stats luck add -1",
            "/stats luck 10001", "/stats luck 2147483648", "/stats list extra", "/stats reset 5", "/stats luck reset extra",
            "/stats luck 10 extra", "/stats luck multiply 2", "/stats luck 1,000", "/stats luck 0.00000000000000000000000000001" })
            Check(StatCommand.Parse(text, out _, out var error) == StatParseResult.Invalid && error.Length > 0, "Reject " + text);
        foreach (string name in new[] { "luck", "defense", "attackspeed", "critical", "criticaldamage", "evasion", "cooldown", "mpregen", "negotiation", "truedamage" })
            Check(StatCatalog.Find(name) != null, "Supported stat: " + name);
        foreach (var pair in new[] { ("crit-chance", "critical"), ("crit-damage", "criticaldamage"), ("critdamage", "criticaldamage"),
            ("cooldownrecovery", "cooldown"), ("mp-regen", "mpregen") })
            Check(StatCatalog.Find(pair.Item1) == StatCatalog.Find(pair.Item2), "Alias: " + pair.Item1);

        var set = Parse("/stats luck set 100");
        Check(StatPlanner.TryPlan(set, new[] { Snapshot("luck", 10), Snapshot("luck", 25, bonus: 5) }, out var updates, out _) &&
            updates[0].Raw == 100 && updates[0].Contribution == 90 && updates[1].Raw == 95 && updates[1].Contribution == 70,
            "Set gives each player the same effective luck and tracks distinct adjustments");
        var add = Parse("/stats luck +10");
        Check(StatPlanner.TryPlan(add, new[] { Snapshot("luck", 10), Snapshot("luck", 40) }, out updates, out _) &&
            updates[0].Raw == 20 && updates[1].Raw == 50, "Add preserves different player baselines");
        var subtract = Parse("/stats luck -5");
        Check(StatPlanner.TryPlan(subtract, new[] { Snapshot("luck", 10) }, out updates, out _) &&
            updates[0].Raw == 5 && updates[0].Contribution == -5, "Subtract tracks a negative contribution");
        Check(!StatPlanner.TryPlan(subtract, new[] { Snapshot("luck", 10), Snapshot("luck", 3) }, out updates, out _) &&
            updates.Length == 0, "One player's underflow rejects every write");
        Check(!StatPlanner.TryPlan(set, Array.Empty<StatSnapshot>(), out updates, out _) && updates.Length == 0, "No players rejects");
        Check(!StatPlanner.TryPlan(set, new[] { Snapshot("defense", 10) }, out _, out _), "Mismatched stat fails closed");

        foreach (var test in new (string Command, string Stat, int Raw)[] {
            ("/stats critical 12.34", "critical", 1234), ("/stats evasion +1.5", "evasion", 150),
            ("/stats attackspeed set 150", "attackspeed", 50), ("/stats criticaldamage 75", "criticaldamage", 25) })
        {
            Check(StatPlanner.TryPlan(Parse(test.Command), new[] { Snapshot(test.Stat, 0) }, out updates, out _) &&
                updates[0].Raw == test.Raw, "Display conversion: " + test.Command);
        }
        foreach (string text in new[] { "/stats critical 101", "/stats evasion 101", "/stats attackspeed 0", "/stats attackspeed 1001" })
            Check(!StatPlanner.TryPlan(Parse(text), new[] { Snapshot(StatCommandTarget(text), 0) }, out _, out _), "Reject resulting limit: " + text);

        Check(StatPlanner.TryPlan(set, new[] { Snapshot("luck", 10, bonus: 5, amplifier: 100) }, out updates, out _) &&
            updates[0].Raw == 45 && updates[0].Contribution == 35, "Set compensates for calculated bonus and multiplier");
        Check(!StatPlanner.TryPlan(Parse("/stats luck 101"), new[] { Snapshot("luck", 10, amplifier: 100) }, out updates, out _) &&
            updates.Length == 0, "Do not approximate unreachable odd value under double multiplier");
        Check(StatPlanner.TryPlan(Parse("/stats luck 10"), new[] { Snapshot("luck", 10, amplifier: 50) }, out updates, out _) &&
            updates[0].Raw == 7, "Use game's truncation for non-integral amplified values");
        Check(StatPlanner.TryPlan(Parse("/stats luck +0"), new[] { Snapshot("luck", 3, amplifier: -50) }, out updates, out _) &&
            updates[0].Raw == 3 && updates[0].Contribution == 0, "No-op retains base under rounding ambiguity");
        foreach (int amplifier in new[] { -100, -101, int.MaxValue })
            Check(!StatPlanner.TryPlan(set, new[] { Snapshot("luck", 10, amplifier: amplifier) }, out _, out _), "Reject unsupported multiplier");
        Check(!StatPlanner.TryPlan(add, new[] { Snapshot("luck", int.MaxValue, bonus: 1) }, out _, out _), "Reject native sum overflow");
        Check(!StatPlanner.TryPlan(add, new[] { Snapshot("luck", 30000000) }, out _, out _), "Reject native multiplication overflow");
        Check(!StatPlanner.TryPlan(set, new[] { Snapshot("luck", 10, int.MaxValue) }, out _, out _), "Reject accumulated marker overflow");

        var reset = Parse("/stats luck reset");
        Check(StatPlanner.TryPlan(reset, new[] { Snapshot("luck", 115, 90, bonus: 5, amplifier: 100) }, out updates, out _) &&
            updates[0].Raw == 25 && updates[0].Contribution == 0, "Reset preserves later equipment and current multiplier");
        Check(StatPlanner.TryPlan(reset, new[] { Snapshot("luck", 8, -5) }, out updates, out _) && updates[0].Raw == 13,
            "Reset removes a negative adjustment");
        Check(StatPlanner.TryPlan(reset, new[] { Snapshot("luck", 13) }, out updates, out _) && updates[0].Raw == 13,
            "Repeated reset preserves unmodified value");
        Check(StatPlanner.TryPlan(reset, new[] { Snapshot("luck", -15, 5, bonus: int.MaxValue, amplifier: int.MaxValue) }, out updates, out _) &&
            updates[0].Raw == -20 && updates[0].Contribution == 0, "Reset permits native negatives and bypasses multiplier calculations");
        Check(!StatPlanner.TryPlan(reset, new[] { Snapshot("luck", 50, 5), Snapshot("luck", int.MinValue, 1) }, out updates, out _) &&
            updates.Length == 0, "Reset overflow rejects all changes");
        Check(StatPlanner.TryPlan(Parse("/stats reset"), new[] { Snapshot("luck", 30, 10), Snapshot("critical", 1500, 500) }, out updates, out _) &&
            updates[0].Raw == 20 && updates[1].Raw == 1000, "Reset-all restores each stat's own baseline");
        Check(StatPlanner.TryPlan(Parse("/stats luck 0"), new[] { Snapshot("luck", 30, 10) }, out updates, out _) &&
            updates[0].Raw == 0 && updates[0].Contribution == -20, "Setting zero is not reset");
        Check(StatPlanner.TryPlan(set, new[] { Snapshot("luck", 100, 90) }, out updates, out _) &&
            updates[0].Raw == 100 && updates[0].Contribution == 90, "Repeated set does not stack");
        return checks;
    }

    private static string StatCommandTarget(string text) => text.Split(' ')[1];
}
