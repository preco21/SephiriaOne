#nullable disable
using System;

namespace SephiriaOne
{
    internal enum ExperienceOperation { Reset, Block, RabbitExempt }
    internal enum ExperienceParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal readonly struct ExperienceCommand
    {
        public ExperienceOperation Operation { get; }
        public bool Enabled { get; }
        public ExperienceCommand(ExperienceOperation operation, bool enabled = false)
        { Operation = operation; Enabled = enabled; }

        public static string Usage => L.T("Host only: /one xp block on|off|reset; rabbit-exempt on|off|reset; status; reset. Both default off. Blocks new XP for all players; existing levels stay unchanged. /one save persists settings.");
        public static ExperienceParseResult Parse(string text, out ExperienceCommand command)
        {
            command = default;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool Is(int i, string value) => parts.Length > i && parts[i].Equals(value, StringComparison.OrdinalIgnoreCase);
            if (!Is(0, "/one") || !Is(1, "xp")) return ExperienceParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && Is(2, "help"))) return ExperienceParseResult.Help;
            if (parts.Length == 3)
            {
                if (Is(2, "status")) return ExperienceParseResult.Status;
                return Is(2, "reset") ? ExperienceParseResult.Valid : ExperienceParseResult.Invalid;
            }
            if (parts.Length != 4 || (!Is(2, "block") && !Is(2, "rabbit-exempt")) ||
                (!Is(3, "on") && !Is(3, "off") && !Is(3, "reset"))) return ExperienceParseResult.Invalid;
            command = new ExperienceCommand(Is(2, "block") ? ExperienceOperation.Block : ExperienceOperation.RabbitExempt, Is(3, "on"));
            return ExperienceParseResult.Valid;
        }
    }
}
