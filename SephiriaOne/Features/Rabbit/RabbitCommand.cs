#nullable enable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum RabbitOption { Infinite, Share, ConsumeMp, SuppressSurvival, Reset, MpAmount, LevelUpPotion }
    internal enum RabbitParseResult { NotCommand, Help, Status, Invalid, Valid }

    internal readonly struct RabbitCommand
    {
        public RabbitOption Option { get; }
        public bool Enabled { get; }
        public int Amount { get; }
        public bool IsReset => Option == RabbitOption.Reset || (Option != RabbitOption.MpAmount && !Enabled);
        public static string Usage => L.T("Host only: /one rabbit infinite on|off, /one rabbit share on|off, /one rabbit mp-cost on|off|<0..10000>, /one rabbit suppress-survival on|off, /one rabbit reset, /one rabbit status. A number sets the MP fee and enables it; on/off retain the amount. Applies to Wing-Eared Rabbit HP potions. Save for future sessions: /one save.") + " " +
            L.T("Potion of Regeneration (Sample) is excluded from all Rabbit HP potion options: no MP cost or shared healing; normal consumption and Survival bonuses remain.") + " " +
            L.T("With infinite HP potions enabled, Wing-Eared Rabbit can use regular HP potions during Tension boss combat. Sample, MP and other potions remain restricted.") + " " +
            L.T("/one rabbit level-up-potion on|off: gain one random non-HP/MP potion on each level-up while wearing Wing-Eared Rabbit.");

        public RabbitCommand(RabbitOption option, bool enabled, int amount = RabbitPotionSettings.DefaultMpCostPerDrink)
        { Option = option; Enabled = enabled; Amount = amount; }

        internal static bool TryParseMpCost(string text, out int amount) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) &&
            amount >= 0 && amount <= RabbitPotionSettings.MaximumMpCostPerDrink;

        public static RabbitParseResult Parse(string? text, out RabbitCommand command, out string error)
        {
            command = default; error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("rabbit", StringComparison.OrdinalIgnoreCase)) return RabbitParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && parts[2].Equals("help", StringComparison.OrdinalIgnoreCase)))
                return RabbitParseResult.Help;
            if (parts.Length == 3 && parts[2].Equals("status", StringComparison.OrdinalIgnoreCase)) return RabbitParseResult.Status;
            if (parts.Length == 3 && parts[2].Equals("reset", StringComparison.OrdinalIgnoreCase))
            { command = new RabbitCommand(RabbitOption.Reset, false); return RabbitParseResult.Valid; }
            error = Usage;
            if (parts.Length != 4) return RabbitParseResult.Invalid;
            RabbitOption option;
            if (parts[2].Equals("infinite", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.Infinite;
            else if (parts[2].Equals("share", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.Share;
            else if (parts[2].Equals("mp-cost", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.ConsumeMp;
            else if (parts[2].Equals("suppress-survival", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.SuppressSurvival;
            else if (parts[2].Equals("level-up-potion", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.LevelUpPotion;
            else return RabbitParseResult.Invalid;
            if (option == RabbitOption.ConsumeMp && TryParseMpCost(parts[3], out int amount))
            {
                command = new RabbitCommand(RabbitOption.MpAmount, true, amount); error = "";
                return RabbitParseResult.Valid;
            }
            bool enabled;
            if (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
            else if (parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
            else return RabbitParseResult.Invalid;
            command = new RabbitCommand(option, enabled); error = "";
            return RabbitParseResult.Valid;
        }
    }
}
