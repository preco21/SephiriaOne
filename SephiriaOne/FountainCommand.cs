#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal enum FountainParseResult { NotCommand, Help, Invalid, Valid }
    internal enum FountainOperation { Set, Add, Subtract, Reset }

    internal sealed class FountainPlan
    {
        public int[] Points { get; }
        public int[] Contributions { get; }
        public int Limit { get; }
        public int? OriginalLimit { get; }
        public int? AppliedLimit { get; }

        public FountainPlan(int[] points, int[] contributions, int limit, int? originalLimit, int? appliedLimit)
        {
            Points = points; Contributions = contributions; Limit = limit;
            OriginalLimit = originalLimit; AppliedLimit = appliedLimit;
        }
    }

    internal readonly struct FountainCommand
    {
        public FountainOperation Operation { get; }
        public int Amount { get; }

        public const string Usage = "Host only: /fountain 100 (set), /fountain +10 (add), /fountain -5 (subtract). Also: /fountain set|add|sub N. /fountain reset restores points without our adjustments.";

        private FountainCommand(FountainOperation operation, int amount)
        {
            Operation = operation;
            Amount = amount;
        }

        public static FountainParseResult Parse(string? text, out FountainCommand command, out string error)
        {
            command = default;
            error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("/fountain", StringComparison.OrdinalIgnoreCase))
            {
                return FountainParseResult.NotCommand;
            }

            if (parts.Length == 1)
            {
                return FountainParseResult.Help;
            }
            if (parts.Length == 2 && parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                command = new FountainCommand(FountainOperation.Reset, 0);
                return FountainParseResult.Valid;
            }

            FountainOperation operation = FountainOperation.Set;
            string amountText;
            if (parts.Length == 2)
            {
                amountText = parts[1];
                if (amountText[0] == '+' || amountText[0] == '-')
                {
                    operation = amountText[0] == '+' ? FountainOperation.Add : FountainOperation.Subtract;
                    amountText = amountText.Substring(1);
                }
            }
            else if (parts.Length == 3)
            {
                switch (parts[1].ToLowerInvariant())
                {
                    case "set": operation = FountainOperation.Set; break;
                    case "add": operation = FountainOperation.Add; break;
                    case "sub":
                    case "subtract": operation = FountainOperation.Subtract; break;
                    default:
                        error = "Unknown Fountain operation. " + Usage;
                        return FountainParseResult.Invalid;
                }

                amountText = parts[2];
            }
            else
            {
                error = "Too many arguments. " + Usage;
                return FountainParseResult.Invalid;
            }

            if (!int.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out int amount))
            {
                error = "Use a whole-number amount from 0 to 2147483647. " + Usage;
                return FountainParseResult.Invalid;
            }

            command = new FountainCommand(operation, amount);
            return FountainParseResult.Valid;
        }

        public bool TryPlan(IReadOnlyList<int> balances, int currentLimit, out int[] updated, out int updatedLimit, out string error)
        {
            bool valid = TryPlanTracked(balances, new int[balances.Count], currentLimit, null, null, out FountainPlan plan, out error);
            updated = plan.Points;
            updatedLimit = plan.Limit;
            return valid;
        }

        public bool TryPlanTracked(IReadOnlyList<int> balances, IReadOnlyList<int> contributions,
            int currentLimit, int? originalLimit, int? appliedLimit, out FountainPlan plan, out string error)
        {
            plan = new FountainPlan(Array.Empty<int>(), Array.Empty<int>(), currentLimit, originalLimit, appliedLimit);
            error = "No active players are ready. Enter town or a run first.";
            if (balances.Count == 0) return false;
            error = "Fountain reset tracking is inconsistent. Nobody was changed.";
            if (balances.Count != contributions.Count || currentLimit < 0 || originalLimit.HasValue != appliedLimit.HasValue ||
                (originalLimit.HasValue && (originalLimit.Value < 0 || appliedLimit.GetValueOrDefault() < originalLimit.Value))) return false;

            var points = new int[balances.Count];
            var offsets = new int[balances.Count];
            int nextLimit = currentLimit;
            bool reset = Operation == FountainOperation.Reset;
            for (int i = 0; i < balances.Count; i++)
            {
                long value = reset ? (long)balances[i] - contributions[i] :
                    Operation == FountainOperation.Set ? Amount :
                    (long)balances[i] + (Operation == FountainOperation.Add ? (long)Amount : -(long)Amount);
                long offset = reset ? 0 : (long)contributions[i] + value - balances[i];
                if (value < 0 || value > int.MaxValue || offset < int.MinValue || offset > int.MaxValue)
                {
                    error = "A player's Fountain points or reset adjustment would exceed its supported range. Nobody was changed.";
                    return false;
                }
                points[i] = (int)value;
                offsets[i] = (int)offset;
                if (!reset) nextLimit = Math.Max(nextLimit, points[i]);
            }

            int? nextOriginal = originalLimit;
            int? nextApplied = appliedLimit;
            if (reset)
            {
                // A different current value belongs to the game or another mod.
                if (appliedLimit.HasValue && currentLimit == appliedLimit.Value) nextLimit = originalLimit.GetValueOrDefault();
                nextOriginal = null;
                nextApplied = null;
            }
            else if (nextLimit != currentLimit)
            {
                if (!appliedLimit.HasValue || currentLimit != appliedLimit.Value) nextOriginal = currentLimit;
                nextApplied = nextLimit;
            }
            plan = new FountainPlan(points, offsets, nextLimit, nextOriginal, nextApplied);
            error = "";
            return true;
        }
    }
}
