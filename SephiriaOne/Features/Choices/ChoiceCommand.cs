#nullable enable
using System;
using System.Globalization;

namespace SephiriaOne
{
    [Flags]
    internal enum ChoiceTarget { Item = 1, Weapon = 2, Miracle = 4, All = 7 }
    internal enum ChoiceParseResult { NotCommand, Help, Invalid, Valid }
    internal enum ChoiceOperation { Set, Add, Subtract, Reset }

    internal readonly struct ChoiceCommand
    {
        public ChoiceTarget Target { get; }
        public ChoiceOperation Operation { get; }
        public int Amount { get; }
        public bool IsReset => Operation == ChoiceOperation.Reset || (Operation == ChoiceOperation.Set && Amount == 0);
        public const int MaximumExtra = 20;
        internal static readonly string[] Keys = { "EXTRAITEMCHOICES", "EXTRAWEAPONCHOICES", "EXTRAMIRACLECHOICES" };
        public const string Usage = "Host only, current and joining players: /choices all|item|weapon|miracle 5, +2, -1, or set|add|sub N. Extra choices: 0..20. Reset: /choices reset or /choices item|weapon|miracle reset.";

        internal ChoiceCommand(ChoiceTarget target, ChoiceOperation operation, int amount)
        {
            Target = target;
            Operation = operation;
            Amount = amount;
        }

        public static ChoiceParseResult Parse(string? text, out ChoiceCommand command, out string error)
        {
            command = default;
            error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("/choices", StringComparison.OrdinalIgnoreCase))
                return ChoiceParseResult.NotCommand;
            if (parts.Length == 1) return ChoiceParseResult.Help;
            if (parts.Length == 2 && parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                command = new ChoiceCommand(ChoiceTarget.All, ChoiceOperation.Reset, 0);
                return ChoiceParseResult.Valid;
            }

            error = Usage;
            if (parts.Length != 3 && parts.Length != 4) return ChoiceParseResult.Invalid;
            ChoiceTarget target;
            switch (parts[1].ToLowerInvariant())
            {
                case "all": target = ChoiceTarget.All; break;
                case "item": target = ChoiceTarget.Item; break;
                case "weapon": target = ChoiceTarget.Weapon; break;
                case "miracle": target = ChoiceTarget.Miracle; break;
                default: return ChoiceParseResult.Invalid;
            }

            ChoiceOperation operation = ChoiceOperation.Set;
            string amountText = parts[parts.Length - 1];
            if (RelativeMultiplier.HasPrefix(amountText))
            {
                error = "Choices count extra candidates, whose native baseline is normally zero. Baseline multipliers are not supported here; use set/add/sub amounts 0..20.";
                return ChoiceParseResult.Invalid;
            }
            if (parts.Length == 3 && amountText.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                command = new ChoiceCommand(target, ChoiceOperation.Reset, 0);
                error = "";
                return ChoiceParseResult.Valid;
            }
            if (parts.Length == 4)
            {
                switch (parts[2].ToLowerInvariant())
                {
                    case "set": break;
                    case "add": operation = ChoiceOperation.Add; break;
                    case "sub":
                    case "subtract": operation = ChoiceOperation.Subtract; break;
                    default: return ChoiceParseResult.Invalid;
                }
            }
            else if (amountText[0] == '+' || amountText[0] == '-')
            {
                operation = amountText[0] == '+' ? ChoiceOperation.Add : ChoiceOperation.Subtract;
                amountText = amountText.Substring(1);
            }

            if (!int.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out int amount) || amount > MaximumExtra)
                return ChoiceParseResult.Invalid;
            command = new ChoiceCommand(target, operation, amount);
            error = "";
            return ChoiceParseResult.Valid;
        }

        public bool TryPlan(int raw, int applied, int bonus, int amplifier,
            out int updatedRaw, out int updatedApplied, out string error)
        {
            updatedRaw = raw;
            updatedApplied = applied;
            error = "The addon's bonus and each resulting extra-choice stat must stay within 0..20. Nobody was changed.";
            if (applied < 0 || applied > MaximumExtra) return false;
            long nextApplied = IsReset ? 0 : Operation == ChoiceOperation.Set ? Amount :
                (long)applied + (Operation == ChoiceOperation.Add ? Amount : -Amount);
            if (nextApplied < 0 || nextApplied > MaximumExtra) return false;
            long nextRaw = (long)raw - applied + nextApplied;
            if (nextRaw < int.MinValue || nextRaw > int.MaxValue) return false;
            if (IsReset)
            {
                // Removing our contribution must not impose expansion limits on
                // stats supplied by the game or another addon.
                updatedRaw = (int)nextRaw;
                updatedApplied = 0;
                error = "";
                return true;
            }

            // UnitAvatar adds the base and calculated bonuses, multiplies as int,
            // then converts to float and truncates. Check before its int operations.
            long sum = nextRaw + bonus;
            long factor = 100L + amplifier;
            if (sum < int.MinValue || sum > int.MaxValue || factor < int.MinValue || factor > int.MaxValue)
                return false;
            long product = sum * factor;
            if (product < int.MinValue || product > int.MaxValue) return false;
            int effective = (int)((float)product / 100f);
            if (effective < 0 || effective > MaximumExtra) return false;

            updatedRaw = (int)nextRaw;
            updatedApplied = (int)nextApplied;
            error = "";
            return true;
        }
    }
}
