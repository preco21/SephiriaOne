#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal enum FountainParseResult { NotCommand, Help, Invalid, Valid }
    internal enum FountainOperation { Set, Add, Subtract }

    internal readonly struct FountainCommand
    {
        public FountainOperation Operation { get; }
        public int Amount { get; }

        public const string Usage = "Host only: /fountain 100 (set), /fountain +10 (add), /fountain -5 (subtract). Also: /fountain set|add|sub N.";

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
            updated = Array.Empty<int>();
            updatedLimit = currentLimit;
            error = "";
            if (balances.Count == 0)
            {
                error = "No active players are ready. Enter town or a run first.";
                return false;
            }

            var planned = new int[balances.Count];
            int plannedLimit = currentLimit;
            for (int i = 0; i < balances.Count; i++)
            {
                long value = Operation == FountainOperation.Set ? Amount :
                    (long)balances[i] + (Operation == FountainOperation.Add ? (long)Amount : -(long)Amount);
                if (value < 0 || value > int.MaxValue)
                {
                    error = "A player's resulting Fountain points would be outside 0..2147483647. Nobody was changed.";
                    return false;
                }

                planned[i] = (int)value;
                plannedLimit = Math.Max(plannedLimit, planned[i]);
            }

            updated = planned;
            updatedLimit = plannedLimit;
            return true;
        }
    }
}
