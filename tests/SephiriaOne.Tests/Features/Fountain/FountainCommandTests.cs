using SephiriaOne;

internal static class FountainCommandTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }

        foreach (string text in new[] { "hello", "hello /fountain 10", "/fountainish 10", "/fountain100", "" })
        {
            Check(FountainCommand.Parse(text, out _, out _) == FountainParseResult.NotCommand,
                $"Ordinary chat must pass through: {text}");
        }

        Check(FountainCommand.Parse(" /FOUNTAIN ", out _, out _) == FountainParseResult.Help, "Bare command shows help");

        foreach (var test in new (string Text, FountainOperation Operation, int Amount)[]
        {
            ("/fountain 100", FountainOperation.Set, 100),
            (" /FoUnTaIn\tSeT 25 ", FountainOperation.Set, 25),
            ("/fountain +10", FountainOperation.Add, 10),
            ("/fountain -5", FountainOperation.Subtract, 5),
            ("/fountain ADD 3", FountainOperation.Add, 3),
            ("/fountain sub 2", FountainOperation.Subtract, 2),
            ("/fountain subtract 2", FountainOperation.Subtract, 2),
            ("/fountain 0", FountainOperation.Set, 0),
            ("/fountain 2147483647", FountainOperation.Set, int.MaxValue)
        })
        {
            var parsed = FountainCommand.Parse(test.Text, out var command, out var error);
            Check(parsed == FountainParseResult.Valid && command.Operation == test.Operation &&
                command.Amount == test.Amount && error.Length == 0, $"Parse: {test.Text}");
        }

        foreach (string text in new[]
        {
            "/fountain +", "/fountain -", "/fountain set -1", "/fountain add -1",
            "/fountain 2147483648", "/fountain -2147483648", "/fountain 1.5",
            "/fountain 1e3", "/fountain 1,000", "/fountain 0x10", "/fountain set",
            "/fountain random 10", "/fountain set 10 extra", "/fountain --5"
        })
        {
            Check(FountainCommand.Parse(text, out _, out var error) == FountainParseResult.Invalid &&
                error.Length > 0, $"Reject malformed command: {text}");
        }

        FountainCommand.Parse("/fountain 100", out var set, out _);
        int[] original = { 4, 8, 12 };
        Check(set.TryPlan(original, 12, out var results, out var limit, out _) &&
            results.SequenceEqual(new[] { 100, 100, 100 }) && limit == 100, "Set all and raise usable carryover cap");
        Check(original.SequenceEqual(new[] { 4, 8, 12 }), "Planning leaves original balances unchanged");
        Check(set.TryPlan(original, 150, out _, out limit, out _) && limit == 150, "Never lower an existing higher cap");

        FountainCommand.Parse("/fountain +10", out var add, out _);
        Check(add.TryPlan(original, 12, out results, out limit, out _) &&
            results.SequenceEqual(new[] { 14, 18, 22 }) && limit == 22, "Addition is relative to each player's balance");
        FountainCommand.Parse("/fountain -5", out var subtract, out _);
        Check(subtract.TryPlan(new[] { 5, 8, 12 }, 12, out results, out limit, out _) &&
            results.SequenceEqual(new[] { 0, 3, 7 }) && limit == 12, "Subtraction supports zero and retains cap");
        Check(!subtract.TryPlan(original, 12, out results, out limit, out var planError) &&
            results.Length == 0 && limit == 12 && planError.Length > 0, "Any underflow rejects the whole batch");
        Check(!add.TryPlan(new[] { 4, int.MaxValue }, 12, out results, out limit, out _) &&
            results.Length == 0 && limit == 12, "Overflow in a later player rejects the whole batch");
        Check(!set.TryPlan(Array.Empty<int>(), 12, out results, out _, out _), "No players is not a successful change");
        FountainCommand.Parse("/fountain 0", out var zero, out _);
        Check(zero.TryPlan(original, 12, out results, out _, out _) && results.All(x => x == 0), "Zero disables points for all players");
        FountainCommand.Parse("/fountain 2147483647", out var maximum, out _);
        Check(maximum.TryPlan(new[] { 1 }, 12, out results, out limit, out _) &&
            results[0] == int.MaxValue && limit == int.MaxValue, "Maximum integer is supported without planner overflow");
        Check(original.SequenceEqual(new[] { 4, 8, 12 }), "Failed batch does not mutate input balances");
        Check(FountainCommand.Parse(" /FOUNTAIN ReSeT ", out var reset, out var resetError) == FountainParseResult.Valid &&
            reset.Operation.ToString() == "Reset" && resetError.Length == 0, "Recognize explicit Fountain reset");
        foreach (string text in new[] { "/fountain reset 5", "/fountain reset extra", "/fountain reset +1 extra" })
            Check(FountainCommand.Parse(text, out _, out _) == FountainParseResult.Invalid, "Reset rejects arguments: " + text);
        return checks;
    }
}
