#nullable disable
using System;

namespace SephiriaOne
{
    internal enum EventSpawnParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal static class EventSpawnCommand
    {
        public static string Usage => L.T("Random events: /one events chance xN; /one events status|reset. N: 0..10000, up to 2 decimals. x1/reset restores native. Affects future chapter generation only; /one save persists settings.");
        public static EventSpawnParseResult Parse(string text, out EventSpawnSettings settings)
        {
            settings = default;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("events", StringComparison.OrdinalIgnoreCase)) return EventSpawnParseResult.NotCommand;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length <= 3)
            {
                if (op == "help") return EventSpawnParseResult.Help;
                if (op == "status") return EventSpawnParseResult.Status;
                if (op == "reset" || op == "off") return EventSpawnParseResult.Valid;
            }
            if (parts.Length == 4 && op == "chance" && RelativeMultiplier.TryParse(parts[3], out decimal factor))
            { settings = new EventSpawnSettings(factor); return EventSpawnParseResult.Valid; }
            return EventSpawnParseResult.Invalid;
        }
    }
}
