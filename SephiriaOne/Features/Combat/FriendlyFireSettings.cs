#nullable disable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct FriendlyFireSettings
    {
        // Hundredths of a percent keep equality/presets exact and default(T) at 100%.
        private readonly int damageOffset;
        public bool Enabled { get; }
        public bool Balance { get; }
        public decimal DamagePercent => (damageOffset + 10000) / 100m;
        public double DamageScale => (damageOffset + 10000) / 10000d;
        public bool HasDamage => damageOffset > -10000;
        public bool HasFractionalPercent => damageOffset % 100 != 0;
        public string Number => DamagePercent.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => Enabled || Balance || damageOffset != 0;
        public FriendlyFireSettings(bool enabled, decimal damagePercent, bool balance = false)
        {
            if (!ValidPercent(damagePercent)) throw new ArgumentOutOfRangeException(nameof(damagePercent));
            Enabled = enabled; Balance = balance; damageOffset = (int)(damagePercent * 100) - 10000;
        }
        public static bool ValidPercent(decimal percent) => percent >= 0 && percent <= 300 && percent * 100 == decimal.Truncate(percent * 100);
        private FriendlyFireSettings(FriendlyFireSettings source, bool enabled)
        { Enabled = enabled; Balance = source.Balance; damageOffset = source.damageOffset; }
        // Deathmatch reads this at hit time; keep toggle overlays in integer space.
        public FriendlyFireSettings WithEnabled(bool enabled) => new FriendlyFireSettings(this, enabled);
        public FriendlyFireSettings Apply(FriendlyFireCommand command) => command.Reset ? default :
            command.Balance.HasValue ? new FriendlyFireSettings(Enabled, DamagePercent, command.Balance.Value) :
            command.Percent.HasValue ? new FriendlyFireSettings(Enabled, command.Percent.Value, Balance) : WithEnabled(command.Enabled);
    }

    internal enum FriendlyFireParseResult { NotCommand, Help, Status, ResetScores, Invalid, Valid }
    internal readonly struct FriendlyFireCommand
    {
        public bool Reset { get; }
        public bool Enabled { get; }
        public decimal? Percent { get; }
        public bool? Balance { get; }
        public FriendlyFireCommand(bool enabled, decimal? percent = null, bool reset = false, bool? balance = null)
        { Enabled = enabled; Percent = percent; Reset = reset; Balance = balance; }
        public bool IsReset => Reset || (Balance.HasValue ? !Balance.Value : !Percent.HasValue && !Enabled);
        public bool TouchesMasterToggle => Reset || !Percent.HasValue && !Balance.HasValue;
        public static string Usage => L.T("Friendly fire: /one friendlyfire on|off|status|reset|resetkda; /one friendlyfire damage 0..300 (percent, up to 2 decimals; e.g. 0.1). Default 100. /one save persists settings.") + " " +
            L.T("PvP balancing: /one friendlyfire balance on|off. Default off; softens large player hits and limits Dark Cloud repeats.");
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
            if (parts.Length == 4 && op == "balance" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, balance: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            return FriendlyFireParseResult.Invalid;
        }
    }
}
