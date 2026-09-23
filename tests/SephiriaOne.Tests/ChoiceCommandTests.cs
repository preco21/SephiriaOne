using SephiriaOne;

internal static class ChoiceCommandTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }

        foreach (string? text in new[] { null, "", "hello", "/choicesish all 5", "say /choices all 5", "/fountain 5" })
            Check(ChoiceCommand.Parse(text, out _, out _) == ChoiceParseResult.NotCommand, "Ordinary chat passes through");
        Check(ChoiceCommand.Parse(" /CHOICES ", out _, out _) == ChoiceParseResult.Help, "Bare choice command shows help");
        foreach (var test in new (string Text, ChoiceTarget Target, ChoiceOperation Operation, int Amount)[]
        {
            ("/choices all 5", ChoiceTarget.All, ChoiceOperation.Set, 5),
            (" /choices\tITEM +2 ", ChoiceTarget.Item, ChoiceOperation.Add, 2),
            ("/choices weapon -1", ChoiceTarget.Weapon, ChoiceOperation.Subtract, 1),
            ("/choices miracle set 5", ChoiceTarget.Miracle, ChoiceOperation.Set, 5),
            ("/choices all ADD 10", ChoiceTarget.All, ChoiceOperation.Add, 10),
            ("/choices item sub 3", ChoiceTarget.Item, ChoiceOperation.Subtract, 3),
            ("/choices weapon subtract 3", ChoiceTarget.Weapon, ChoiceOperation.Subtract, 3),
            ("/choices all 0", ChoiceTarget.All, ChoiceOperation.Set, 0),
            ("/choices all 20", ChoiceTarget.All, ChoiceOperation.Set, 20)
        })
        {
            Check(ChoiceCommand.Parse(test.Text, out var command, out _) == ChoiceParseResult.Valid &&
                command.Target == test.Target && command.Operation == test.Operation && command.Amount == test.Amount, test.Text);
        }
        foreach (string text in new[] { "/choices all", "/choices 5", "/choices other 5", "/choices all +", "/choices all -",
            "/choices all 21", "/choices all 2147483648", "/choices all 1.5", "/choices all 1e1", "/choices all 0x10",
            "/choices all set -5", "/choices all add -5", "/choices all add +5", "/choices all set 5 extra", "/choices all random 5" })
            Check(ChoiceCommand.Parse(text, out _, out var error) == ChoiceParseResult.Invalid && error.Length > 0, "Reject " + text);

        ChoiceCommand.Parse("/choices all 5", out var set, out _);
        Check(set.TryPlan(2, 0, 0, 0, out var raw, out var applied, out _) && raw == 7 && applied == 5, "Preserve equipment bonus");
        Check(set.TryPlan(raw, applied, 0, 0, out raw, out applied, out _) && raw == 7 && applied == 5, "Setting twice does not stack");
        Check(set.TryPlan(9, 5, 0, 0, out raw, out applied, out _) && raw == 9, "Preserve stat changes after first command");
        ChoiceCommand.Parse("/choices all +2", out var add, out _);
        Check(add.TryPlan(7, 5, 0, 0, out raw, out applied, out _) && raw == 9 && applied == 7, "Addition adjusts mod bonus");
        ChoiceCommand.Parse("/choices all -2", out var sub, out _);
        Check(sub.TryPlan(7, 5, 0, 0, out raw, out applied, out _) && raw == 5 && applied == 3, "Subtraction adjusts mod bonus");
        ChoiceCommand.Parse("/choices all 0", out var reset, out _);
        Check(reset.TryPlan(9, 5, 0, 0, out raw, out applied, out _) && raw == 4 && applied == 0, "Reset preserves non-mod changes");
        Check(!sub.TryPlan(1, 1, 0, 0, out raw, out applied, out _) && raw == 1 && applied == 1, "Underflow leaves outputs unchanged");
        Check(!add.TryPlan(20, 20, 0, 0, out _, out _, out _), "Mod bonus cannot exceed 20");
        Check(!set.TryPlan(int.MaxValue, 0, 0, 0, out _, out _, out _), "Reject raw overflow");
        Check(!set.TryPlan(1, -1, 0, 0, out _, out _, out _), "Reject corrupt contribution marker");
        Check(!set.TryPlan(0, 0, 16, 0, out _, out _, out _), "Account for calculated bonuses");
        Check(!set.TryPlan(0, 0, 0, 400, out _, out _, out _), "Account for stat amplification");
        Check(set.TryPlan(0, 0, 0, 100, out raw, out _, out _) && raw == 5, "Valid amplified value preserves amplifier");
        Check(!set.TryPlan(0, 0, 0, int.MaxValue, out _, out _, out _), "Reject native factor overflow");
        Check(!set.TryPlan(0, 0, int.MaxValue, 0, out _, out _, out _), "Reject native sum overflow");
        Check(!set.TryPlan(0, 0, 100000000, 0, out _, out _, out _), "Reject native multiplication overflow");
        Check(!set.TryPlan(-20, 0, 0, 0, out _, out _, out _), "Reject negative effective candidates");
        foreach (var test in new (string Text, ChoiceTarget Target)[] {
            ("/choices reset", ChoiceTarget.All), (" /CHOICES ALL RESET ", ChoiceTarget.All),
            ("/choices item reset", ChoiceTarget.Item), ("/choices weapon reset", ChoiceTarget.Weapon),
            ("/choices miracle reset", ChoiceTarget.Miracle) })
            Check(ChoiceCommand.Parse(test.Text, out var parsed, out _) == ChoiceParseResult.Valid &&
                parsed.Target == test.Target && parsed.Operation.ToString() == "Reset", "Reset syntax: " + test.Text);
        foreach (string text in new[] { "/choices reset 5", "/choices all reset 5", "/choices item reset extra", "/choices reset all", "/choices other reset" })
            Check(ChoiceCommand.Parse(text, out _, out _) == ChoiceParseResult.Invalid, "Reject extra reset arguments: " + text);
        ChoiceCommand.Parse("/choices reset", out var explicitReset, out _);
        Check(explicitReset.TryPlan(9, 5, 0, 0, out raw, out applied, out _) && raw == 4 && applied == 0, "Explicit reset retains equipment changes");
        Check(explicitReset.TryPlan(30, 5, 0, 0, out raw, out applied, out _) && raw == 25 && applied == 0, "Reset permits the original native stat above the expansion limit");
        Check(explicitReset.TryPlan(4, 0, int.MaxValue, int.MaxValue, out raw, out applied, out _) && raw == 4 && applied == 0, "Unmodified reset does not depend on unrelated modifier arithmetic");
        Check(reset.TryPlan(30, 5, 0, 0, out raw, out applied, out _) && raw == 25 && applied == 0, "Set zero has the same restoration behavior");
        Check(!explicitReset.TryPlan(int.MinValue, 5, 0, 0, out raw, out applied, out _) && raw == int.MinValue && applied == 5, "Unsafe restoration leaves state unchanged");
        return checks;
    }
}
