#nullable enable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum MerchantParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal enum MerchantOption { Toggle, Chance, Reset }

    internal readonly struct MerchantCommand
    {
        public const int DefaultChance = 25;
        public MerchantOption Option { get; }
        public bool Enabled { get; }
        public int Chance { get; }
        public bool IsReset => Option == MerchantOption.Reset || (Option == MerchantOption.Toggle && !Enabled);
        public static string Usage => L.T("Host only: /one merchant on|off|status|reset, /one merchant chance <0..100>. While enabled, each run guarantees one extra hostile Wandering Merchant on a randomly selected eligible floor, including the first. Other eligible floors use the chosen whole-number percent (default 25), before or after the guaranteed encounter. At most one per normal dungeon floor; excludes boss-only floors, lobby, towns, and training. Added merchants have 1x normal HP, cannot talk, and have no negotiation/crime penalty. Chance changes keep the toggle unchanged. Off stops future spawns; reset also restores 25%. Existing added merchants stay exempt until floor teardown. Save: /one save.");

        public MerchantCommand(bool enabled) { Option = MerchantOption.Toggle; Enabled = enabled; Chance = DefaultChance; }
        public MerchantCommand(int chance) { Option = MerchantOption.Chance; Enabled = false; Chance = chance; }
        private MerchantCommand(MerchantOption option) { Option = option; Enabled = false; Chance = DefaultChance; }

        internal static bool TryParseChance(string text, out int chance) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out chance) && chance >= 0 && chance <= 100;

        public static MerchantParseResult Parse(string? text, out MerchantCommand command, out string error)
        {
            command = default; error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("merchant", StringComparison.OrdinalIgnoreCase)) return MerchantParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && parts[2].Equals("help", StringComparison.OrdinalIgnoreCase)))
                return MerchantParseResult.Help;
            if (parts.Length == 3)
            {
                if (parts[2].Equals("status", StringComparison.OrdinalIgnoreCase)) return MerchantParseResult.Status;
                if (parts[2].Equals("on", StringComparison.OrdinalIgnoreCase))
                { command = new MerchantCommand(true); return MerchantParseResult.Valid; }
                if (parts[2].Equals("off", StringComparison.OrdinalIgnoreCase)) return MerchantParseResult.Valid;
                if (parts[2].Equals("reset", StringComparison.OrdinalIgnoreCase))
                { command = new MerchantCommand(MerchantOption.Reset); return MerchantParseResult.Valid; }
            }
            if (parts.Length == 4 && parts[2].Equals("chance", StringComparison.OrdinalIgnoreCase) && TryParseChance(parts[3], out int chance))
            { command = new MerchantCommand(chance); return MerchantParseResult.Valid; }
            error = Usage;
            return MerchantParseResult.Invalid;
        }
    }
}
