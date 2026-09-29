#nullable enable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum MerchantParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal enum MerchantOption { Toggle, Chance, FirstFloor, MaxPerRun, Reset, Guarantee }

    internal readonly struct MerchantCommand
    {
        public const int DefaultChance = 25;
        private readonly string? typeId;
        public string TypeId => typeId ?? MerchantCatalog.DefaultId;
        public bool AllTypes { get; }
        public MerchantOption Option { get; }
        public bool Enabled { get; }
        public int Value { get; }
        public int Chance => Option == MerchantOption.Chance ? Value : DefaultChance;
        public bool IsReset => Option == MerchantOption.Reset || ((Option == MerchantOption.Toggle || Option == MerchantOption.Guarantee) && !Enabled);
        public static string Usage => L.T("Host only: /one merchant <id> on|off|status|reset, /one merchant <id> guarantee on|off, /one merchant <id> chance <0..100>, /one merchant <id> from <1..1000>, /one merchant <id> limit <0..1000> (0 = unlimited). Omit <id> for Wandering Merchant; /one merchant status lists all types. Guarantee defaults on: one encounter per enabled type on a random eligible floor. Guarantee off leaves chance rolls active with no reserved run-limit slot. Conditions apply per type; different types can share a floor. Off keeps settings; reset restores spawns off, guarantee on, 25%, first floor 1, unlimited. Save: /one save.") + " " + L.T("Merchant types: ") + TypeIds();

        public MerchantCommand(bool enabled) : this(MerchantCatalog.DefaultId, MerchantOption.Toggle, enabled ? 1 : 0) { }
        public MerchantCommand(int chance) : this(MerchantCatalog.DefaultId, MerchantOption.Chance, chance) { }
        public MerchantCommand(string id, MerchantOption option, int value = 0, bool allTypes = false)
        {
            typeId = (MerchantCatalog.Find(id) ?? throw new ArgumentException("Unknown merchant type.", nameof(id))).Id;
            Option = option; Enabled = (option == MerchantOption.Toggle || option == MerchantOption.Guarantee) && value != 0;
            Value = value; AllTypes = allTypes;
        }

        private static string TypeIds()
        {
            var ids = new string[MerchantCatalog.All.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = MerchantCatalog.All[i].Id;
            return string.Join(", ", ids);
        }

        internal static bool TryParseChance(string text, out int chance) =>
            TryParseNumber(text, 0, 100, out chance);

        internal static bool TryParseNumber(string text, int minimum, int maximum, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;

        public static MerchantParseResult Parse(string? text, out MerchantCommand command, out string error)
        {
            command = default; error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("merchant", StringComparison.OrdinalIgnoreCase)) return MerchantParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && parts[2].Equals("help", StringComparison.OrdinalIgnoreCase)))
                return MerchantParseResult.Help;
            string id = MerchantCatalog.DefaultId;
            int operation = 2;
            var definition = MerchantCatalog.Find(parts[2]);
            if (definition != null) { id = definition.Id; operation++; }
            int count = parts.Length - operation;
            if (count == 0 || (count == 1 && parts[operation].Equals("help", StringComparison.OrdinalIgnoreCase)))
                return MerchantParseResult.Help;
            if (count == 1)
            {
                if (parts[operation].Equals("status", StringComparison.OrdinalIgnoreCase))
                { command = new MerchantCommand(id, MerchantOption.Toggle, allTypes: definition == null); return MerchantParseResult.Status; }
                if (parts[operation].Equals("on", StringComparison.OrdinalIgnoreCase))
                { command = new MerchantCommand(id, MerchantOption.Toggle, 1); return MerchantParseResult.Valid; }
                if (parts[operation].Equals("off", StringComparison.OrdinalIgnoreCase))
                { command = new MerchantCommand(id, MerchantOption.Toggle); return MerchantParseResult.Valid; }
                if (parts[operation].Equals("reset", StringComparison.OrdinalIgnoreCase))
                { command = new MerchantCommand(id, MerchantOption.Reset); return MerchantParseResult.Valid; }
            }
            if (count == 2)
            {
                string option = parts[operation].ToLowerInvariant();
                if (option == "guarantee" &&
                    (parts[operation + 1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                     parts[operation + 1].Equals("off", StringComparison.OrdinalIgnoreCase)))
                {
                    command = new MerchantCommand(id, MerchantOption.Guarantee,
                        parts[operation + 1].Equals("on", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                    return MerchantParseResult.Valid;
                }
                int maximum = option == "chance" ? 100 : 1000;
                int minimum = option == "from" ? 1 : 0;
                if ((option == "chance" || option == "from" || option == "limit") &&
                    TryParseNumber(parts[operation + 1], minimum, maximum, out int value))
                {
                    command = new MerchantCommand(id, option == "chance" ? MerchantOption.Chance :
                        option == "from" ? MerchantOption.FirstFloor : MerchantOption.MaxPerRun, value);
                    return MerchantParseResult.Valid;
                }
            }
            error = Usage;
            return MerchantParseResult.Invalid;
        }
    }
}
