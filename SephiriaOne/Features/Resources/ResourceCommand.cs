#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal enum ResourceKind { Dice, Slots, Talents, Fruit, Leaves }
    internal enum ResourceOperation { Set, Add, Subtract, Multiply, Reset }
    internal enum ResourceParseResult { NotCommand, Help, Invalid, Valid }
    internal sealed class ResourceDefinition
    {
        public ResourceKind Kind { get; }
        public string Name { get; }
        public string Label { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public bool StartingOnly => Kind == ResourceKind.Dice || Kind == ResourceKind.Leaves;
        public string Marker { get; }
        public ResourceDefinition(ResourceKind kind, string name, string label, int minimum, int maximum)
        { Kind = kind; Name = name; Label = label; Minimum = minimum; Maximum = maximum; Marker = "SEPHIRIAONE_RESOURCE_" + name.ToUpperInvariant(); }
    }
    internal static class ResourceCatalog
    {
        public static readonly IReadOnlyList<ResourceDefinition> All = Array.AsReadOnly(new[]
        {
            new ResourceDefinition(ResourceKind.Dice, "dice", "Initial reroll dice", 0, 1000),
            new ResourceDefinition(ResourceKind.Slots, "slots", "Inventory slots", 6, 96),
            new ResourceDefinition(ResourceKind.Talents, "talents", "Total talent budget", 0, 1000),
            new ResourceDefinition(ResourceKind.Fruit, "fruit", "Total fruit-skewer budget", 0, 100),
            new ResourceDefinition(ResourceKind.Leaves, "leaves", "Initial leaves", 0, 1000000000)
        });
        public static ResourceDefinition Get(ResourceKind kind)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Kind == kind) return All[i];
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        public static ResourceDefinition? Find(string name)
        {
            foreach (var item in All) if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }
    }
    internal readonly struct ResourceCommand
    {
        public ResourceDefinition? Definition { get; }
        public ResourceOperation Operation { get; }
        public decimal Amount { get; }
        public bool IsReset => Operation == ResourceOperation.Reset || (Operation == ResourceOperation.Multiply && Amount == 1);
        public const string Usage = "Host only: /resources dice|slots|talents|fruit|leaves set|add|sub N, +N, -N, or xN. Set means total; deltas accumulate from native. Dice/leaves affect future starting grants only. Reset: /resources <name> reset or /resources reset. Status/save: /one status, /one save.";
        public ResourceCommand(ResourceDefinition? definition, ResourceOperation operation, decimal amount)
        { Definition = definition; Operation = operation; Amount = amount; }
        public static ResourceParseResult Parse(string? text, out ResourceCommand command, out string error)
        {
            command = default; error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("/resources", StringComparison.OrdinalIgnoreCase)) return ResourceParseResult.NotCommand;
            if (parts.Length == 1 || (parts.Length == 2 && parts[1].Equals("help", StringComparison.OrdinalIgnoreCase))) return ResourceParseResult.Help;
            if (parts.Length == 2 && parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase))
            { command = new ResourceCommand(null, ResourceOperation.Reset, 0); return ResourceParseResult.Valid; }
            error = Usage;
            if (parts.Length != 3 && parts.Length != 4) return ResourceParseResult.Invalid;
            var definition = ResourceCatalog.Find(parts[1]);
            if (definition == null) return ResourceParseResult.Invalid;
            string amount = parts[parts.Length - 1];
            if (parts.Length == 3 && amount.Equals("reset", StringComparison.OrdinalIgnoreCase))
            { command = new ResourceCommand(definition, ResourceOperation.Reset, 0); error = ""; return ResourceParseResult.Valid; }
            ResourceOperation operation = ResourceOperation.Set;
            if (parts.Length == 4)
            {
                switch (parts[2].ToLowerInvariant())
                {
                    case "set": break;
                    case "add": operation = ResourceOperation.Add; break;
                    case "sub": case "subtract": operation = ResourceOperation.Subtract; break;
                    default: return ResourceParseResult.Invalid;
                }
            }
            else if (amount[0] == '+' || amount[0] == '-')
            { operation = amount[0] == '+' ? ResourceOperation.Add : ResourceOperation.Subtract; amount = amount.Substring(1); }
            if (RelativeMultiplier.HasPrefix(amount))
            {
                error = RelativeMultiplier.Usage;
                if (operation != ResourceOperation.Set || !RelativeMultiplier.TryParse(amount, out decimal factor)) return ResourceParseResult.Invalid;
                command = new ResourceCommand(definition, ResourceOperation.Multiply, factor);
            }
            else
            {
                if (!int.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number > definition.Maximum) return ResourceParseResult.Invalid;
                command = new ResourceCommand(definition, operation, number);
            }
            error = ""; return ResourceParseResult.Valid;
        }
    }
}
