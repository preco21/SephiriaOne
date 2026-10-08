#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal enum StatParseResult { NotCommand, Help, List, Invalid, Valid }
    internal enum StatOperation { Set, Add, Subtract, Reset, Multiply }

    internal readonly struct StatCommand
    {
        public StatDefinition? Stat { get; }
        public StatOperation Operation { get; }
        public decimal Amount { get; }
        public static string Usage => L.F("Host only, current and joining players: /stats luck 100, +10, -5, x3, or set|add|sub N. xN targets each player's native displayed stat times N; repeated xN replaces the factor. Deltas accumulate; a delta after Set or xN starts a new native offset. {0} Reset: /stats luck reset or /stats reset. Names/units: /stats list. Active values: /one status. Save: /one save.", RelativeMultiplier.Usage);

        internal StatCommand(StatDefinition? stat, StatOperation operation, decimal amount)
        {
            Stat = stat;
            Operation = operation;
            Amount = amount;
        }

        public static StatParseResult Parse(string? text, out StatCommand command, out string error)
        {
            command = default;
            error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("/stats", StringComparison.OrdinalIgnoreCase))
                return StatParseResult.NotCommand;
            if (parts.Length == 1 || (parts.Length == 2 && parts[1].Equals("help", StringComparison.OrdinalIgnoreCase)))
                return StatParseResult.Help;
            if (parts.Length == 2 && parts[1].Equals("list", StringComparison.OrdinalIgnoreCase)) return StatParseResult.List;
            if ((parts.Length == 2 && parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase)) ||
                (parts.Length == 3 && parts[1].Equals("all", StringComparison.OrdinalIgnoreCase) && parts[2].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            {
                command = new StatCommand(null, StatOperation.Reset, 0);
                return StatParseResult.Valid;
            }

            error = Usage;
            if (parts.Length != 3 && parts.Length != 4) return StatParseResult.Invalid;
            StatDefinition? stat = StatCatalog.Find(parts[1]);
            if (stat == null)
            {
                error = L.T("Unknown stat. Use /stats list for supported names and units.");
                return StatParseResult.Invalid;
            }
            string amountText = parts[parts.Length - 1];
            if (parts.Length == 3 && amountText.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                command = new StatCommand(stat, StatOperation.Reset, 0);
                error = "";
                return StatParseResult.Valid;
            }
            StatOperation operation = StatOperation.Set;
            if (parts.Length == 4)
            {
                switch (parts[2].ToLowerInvariant())
                {
                    case "set": break;
                    case "add": operation = StatOperation.Add; break;
                    case "sub": case "subtract": operation = StatOperation.Subtract; break;
                    default: return StatParseResult.Invalid;
                }
            }
            else if (amountText[0] == '+' || amountText[0] == '-')
            {
                operation = amountText[0] == '+' ? StatOperation.Add : StatOperation.Subtract;
                amountText = amountText.Substring(1);
            }

            if (RelativeMultiplier.HasPrefix(amountText))
            {
                error = RelativeMultiplier.Usage;
                if (operation != StatOperation.Set || !RelativeMultiplier.TryParse(amountText, out decimal factor)) return StatParseResult.Invalid;
                command = new StatCommand(stat, StatOperation.Multiply, factor);
                error = "";
                return StatParseResult.Valid;
            }

            int decimalPoint = amountText.IndexOf('.');
            int allowedDecimals = stat.Scale == 100 ? 2 : 0;
            if ((decimalPoint >= 0 && (allowedDecimals == 0 || amountText.Length - decimalPoint - 1 > allowedDecimals)) ||
                !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) ||
                amount < 0 || amount > 10000)
            {
                error = L.F("Use an unsigned amount 0..10000 with at most {0} decimal places. Use +N or -N to add/subtract. /stats list shows resulting limits.", allowedDecimals);
                return StatParseResult.Invalid;
            }
            command = new StatCommand(stat, operation, amount);
            error = "";
            return StatParseResult.Valid;
        }
    }
}
