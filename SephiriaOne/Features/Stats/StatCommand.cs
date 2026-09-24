#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal enum StatParseResult { NotCommand, Help, List, Invalid, Valid }
    internal enum StatOperation { Set, Add, Subtract, Reset }

    internal sealed class StatDefinition
    {
        public string Name { get; }
        public string Key { get; }
        public int Scale { get; }
        public int Offset { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public string Unit { get; }
        public string Marker => "SEPHIRIAONE_STAT_" + Key;

        public StatDefinition(string name, string key, string unit, int scale = 1,
            int offset = 0, int minimum = 0, int maximum = 10000)
        {
            Name = name;
            Key = key;
            Unit = unit;
            Scale = scale;
            Offset = offset;
            Minimum = minimum;
            Maximum = maximum;
        }

        public decimal Display(int effective) => (decimal)effective / Scale + Offset;
    }

    internal static class StatCatalog
    {
        public static readonly IReadOnlyList<StatDefinition> All = Array.AsReadOnly(new[]
        {
            new StatDefinition("luck", "LUCK", "points"),
            new StatDefinition("defense", "DAMAGEREDUCTION", "defense points"),
            new StatDefinition("attackspeed", "ATTACKSPEED", "total % (100 = normal)", offset: 100, minimum: 1, maximum: 1000),
            new StatDefinition("critical", "CRITICAL", "chance %", scale: 100, maximum: 100),
            new StatDefinition("criticaldamage", "CRITICALDAMAGEBONUS", "bonus % (50 = default)", offset: 50),
            new StatDefinition("evasion", "EVASION", "rating, not dodge %", scale: 100, maximum: 100),
            new StatDefinition("cooldown", "COOLDOWNRECOVERYSPEED", "recovery points"),
            new StatDefinition("mpregen", "MPREGEN", "regeneration points"),
            new StatDefinition("negotiation", "NEGOTIATION", "points"),
            new StatDefinition("truedamage", "TRUEDAMAGE", "points")
        });

        public static StatDefinition? Find(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "armor": name = "defense"; break;
                case "attack-speed": name = "attackspeed"; break;
                case "crit": case "crit-chance": name = "critical"; break;
                case "critdamage": case "crit-damage": name = "criticaldamage"; break;
                case "cooldownrecovery": name = "cooldown"; break;
                case "mp-regen": name = "mpregen"; break;
            }
            foreach (StatDefinition stat in All)
                if (stat.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return stat;
            return null;
        }
    }

    internal readonly struct StatCommand
    {
        public StatDefinition? Stat { get; }
        public StatOperation Operation { get; }
        public decimal Amount { get; }
        public const string Usage = "Host only, current and joining players: /stats luck 100, +10, -5, or set|add|sub N. Deltas accumulate from each player's native stats; a delta after set starts a new offset. Reset: /stats luck reset or /stats reset. Names/units: /stats list. Active values: /mod status. Save for next launch: /mod save.";

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
                error = "Unknown stat. Use /stats list for supported names and units.";
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

            int decimalPoint = amountText.IndexOf('.');
            int allowedDecimals = stat.Scale == 100 ? 2 : 0;
            if ((decimalPoint >= 0 && (allowedDecimals == 0 || amountText.Length - decimalPoint - 1 > allowedDecimals)) ||
                !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) ||
                amount < 0 || amount > 10000)
            {
                error = $"Use an unsigned amount 0..10000 with at most {allowedDecimals} decimal places. Use +N or -N to add/subtract. /stats list shows resulting limits.";
                return StatParseResult.Invalid;
            }
            command = new StatCommand(stat, operation, amount);
            error = "";
            return StatParseResult.Valid;
        }
    }
}
