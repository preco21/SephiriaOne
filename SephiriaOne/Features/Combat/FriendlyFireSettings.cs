#nullable disable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct FriendlyFireSettings
    {
        private readonly int damageOffset;
        public bool Enabled { get; }
        public int DamagePercent => damageOffset + 100;
        public bool HasChanges => Enabled || damageOffset != 0;
        public FriendlyFireSettings(bool enabled, int damagePercent)
        {
            if (damagePercent < 0 || damagePercent > 300) throw new ArgumentOutOfRangeException(nameof(damagePercent));
            Enabled = enabled; damageOffset = damagePercent - 100;
        }
        public FriendlyFireSettings Apply(FriendlyFireCommand command) => command.Reset ? default :
            new FriendlyFireSettings(command.Percent.HasValue ? Enabled : command.Enabled, command.Percent ?? DamagePercent);
    }

    internal enum FriendlyFireParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal readonly struct FriendlyFireCommand
    {
        public bool Reset { get; }
        public bool Enabled { get; }
        public int? Percent { get; }
        public FriendlyFireCommand(bool enabled, int? percent = null, bool reset = false)
        { Enabled = enabled; Percent = percent; Reset = reset; }
        public bool IsReset => Reset || !Percent.HasValue && !Enabled;
        public static string Usage => L.T("Friendly fire: /one friendlyfire on|off|status|reset; /one friendlyfire damage 0..300 (percent, default 100). /one save persists settings.");
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
                if (op == "on" || op == "off" || op == "reset")
                { command = new FriendlyFireCommand(op == "on", reset: op == "reset"); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "damage" && int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out int percent) && percent >= 0 && percent <= 300)
            { command = new FriendlyFireCommand(false, percent); return FriendlyFireParseResult.Valid; }
            return FriendlyFireParseResult.Invalid;
        }
    }
}
