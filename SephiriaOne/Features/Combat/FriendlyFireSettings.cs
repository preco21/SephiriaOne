#nullable disable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct FriendlyFireSettings
    {
        // Hundredths of a percent keep equality/presets exact and default(T)
        // at the native damage scale and configured default refill.
        private readonly int damageOffset;
        private readonly int refillOffset;
        public const decimal DefaultRefillPercent = 70m;
        private const int DefaultRefillHundredths = (int)(DefaultRefillPercent * 100);
        public const double DefaultRefillScale = DefaultRefillHundredths / 10000d;
        public bool Enabled { get; }
        public bool Balance { get; }
        public bool BlockLobbyRegen { get; }
        public decimal DamagePercent => (damageOffset + 10000) / 100m;
        public double DamageScale => (damageOffset + 10000) / 10000d;
        public bool HasDamage => damageOffset > -10000;
        public bool HasFractionalPercent => damageOffset % 100 != 0;
        public string Number => DamagePercent.ToString("0.##", CultureInfo.InvariantCulture);
        public decimal RefillPercent => (refillOffset + DefaultRefillHundredths) / 100m;
        public double RefillScale => (refillOffset + DefaultRefillHundredths) / 10000d;
        public string RefillNumber => RefillPercent.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => Enabled || Balance || BlockLobbyRegen || damageOffset != 0 || refillOffset != 0;
        public FriendlyFireSettings(bool enabled, decimal damagePercent, bool balance = false, bool blockLobbyRegen = false,
            decimal refillPercent = DefaultRefillPercent)
        {
            if (!ValidPercent(damagePercent)) throw new ArgumentOutOfRangeException(nameof(damagePercent));
            if (!ValidRefillPercent(refillPercent)) throw new ArgumentOutOfRangeException(nameof(refillPercent));
            Enabled = enabled; Balance = balance; BlockLobbyRegen = blockLobbyRegen; damageOffset = (int)(damagePercent * 100) - 10000;
            refillOffset = (int)(refillPercent * 100) - DefaultRefillHundredths;
        }
        public static bool ValidPercent(decimal percent) => percent >= 0 && percent <= 300 && percent * 100 == decimal.Truncate(percent * 100);
        public static bool ValidRefillPercent(decimal percent) => percent >= 1 && ValidPercent(percent);
        private FriendlyFireSettings(FriendlyFireSettings source, bool enabled)
        { Enabled = enabled; Balance = source.Balance; BlockLobbyRegen = source.BlockLobbyRegen; damageOffset = source.damageOffset; refillOffset = source.refillOffset; }
        // Deathmatch reads this at hit time; keep toggle overlays in integer space.
        public FriendlyFireSettings WithEnabled(bool enabled) => new FriendlyFireSettings(this, enabled);
        public FriendlyFireSettings Apply(FriendlyFireCommand command) => command.Reset ? default :
            command.RefillPercent.HasValue ? new FriendlyFireSettings(Enabled, DamagePercent, Balance, BlockLobbyRegen, command.RefillPercent.Value) :
            command.BlockLobbyRegen.HasValue ? new FriendlyFireSettings(Enabled, DamagePercent, Balance, command.BlockLobbyRegen.Value, RefillPercent) :
            command.Balance.HasValue ? new FriendlyFireSettings(Enabled, DamagePercent, command.Balance.Value, BlockLobbyRegen, RefillPercent) :
            command.Percent.HasValue ? new FriendlyFireSettings(Enabled, command.Percent.Value, Balance, BlockLobbyRegen, RefillPercent) : WithEnabled(command.Enabled);
    }

    internal enum FriendlyFireParseResult { NotCommand, Help, Status, ResetScores, Invalid, Valid }
    internal readonly struct FriendlyFireCommand
    {
        public bool Reset { get; }
        public bool Enabled { get; }
        public decimal? Percent { get; }
        public decimal? RefillPercent { get; }
        public bool? Balance { get; }
        public bool? BlockLobbyRegen { get; }
        public FriendlyFireCommand(bool enabled, decimal? percent = null, bool reset = false, bool? balance = null, bool? blockLobbyRegen = null,
            decimal? refillPercent = null)
        { Enabled = enabled; Percent = percent; Reset = reset; Balance = balance; BlockLobbyRegen = blockLobbyRegen; RefillPercent = refillPercent; }
        public bool IsReset => Reset || !RefillPercent.HasValue && (BlockLobbyRegen.HasValue ? !BlockLobbyRegen.Value : Balance.HasValue ? !Balance.Value : !Percent.HasValue && !Enabled);
        public bool TouchesMasterToggle => Reset || !Percent.HasValue && !Balance.HasValue && !BlockLobbyRegen.HasValue && !RefillPercent.HasValue;
        public static string Usage => L.T("Friendly fire: /one friendlyfire on|off|status|reset|resetkda; /one friendlyfire damage 0..300 (percent, up to 2 decimals; e.g. 0.1). Default 100. /one save persists settings.") + " " +
            L.T("PvP balancing: /one friendlyfire balance on|off. Default off; softens hits, limits rapid damage (20% HP burst, configurable refill per opponent) and Dark Cloud repeats; slows debuff ticks by 25% and limits Shock/Freeze damage to once per 0.25s. Freeze stun is preserved.") + " " +
            L.T("PvP refill: /one friendlyfire refill 1..300|reset (percent of max HP per second, up to 2 decimals). Default 70; applies only with PvP balancing on.") + " " +
            L.T("Lobby recovery: /one friendlyfire block-lobby-regen on|off. Default off; blocks automatic HP/MP recovery for active lobby participants. Potions and skill healing remain available.");
        internal static bool TryPercent(string text, out decimal percent)
        {
            string number = (text ?? "").Trim();
            if (number.EndsWith("%", StringComparison.Ordinal)) number = number.Substring(0, number.Length - 1);
            int point = number.IndexOf('.');
            percent = 0;
            return (point < 0 || number.Length - point - 1 <= 2) && decimal.TryParse(number, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out percent) && FriendlyFireSettings.ValidPercent(percent);
        }
        public static FriendlyFireParseResult Parse(string text, out FriendlyFireCommand command)
        {
            command = default;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("friendlyfire", StringComparison.OrdinalIgnoreCase)) return FriendlyFireParseResult.NotCommand;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length <= 3)
            {
                if (op == "help") return FriendlyFireParseResult.Help;
                if (op == "status") return FriendlyFireParseResult.Status;
                if (op == "resetkda") return FriendlyFireParseResult.ResetScores;
                if (op == "on" || op == "off" || op == "reset")
                { command = new FriendlyFireCommand(op == "on", reset: op == "reset"); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "damage" && TryPercent(parts[3], out decimal percent))
            { command = new FriendlyFireCommand(false, percent); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "refill")
            {
                decimal refill = FriendlyFireSettings.DefaultRefillPercent;
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                    TryPercent(parts[3], out refill) && FriendlyFireSettings.ValidRefillPercent(refill))
                { command = new FriendlyFireCommand(false, refillPercent: refill); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "balance" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, balance: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "block-lobby-regen" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, blockLobbyRegen: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            return FriendlyFireParseResult.Invalid;
        }
    }
}
