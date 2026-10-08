#nullable disable
using System;

namespace SephiriaOne
{
    internal enum CollinParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal static class CollinCommand
    {
        internal static string Usage => L.T("Host only: /one collin on|off|status|reset. Grants Hiring Crest Collin to Mole, Farmer Squirrel and Turtle at the next costume equip or fresh-run restock. Default off. /one save persists settings.");
        internal static CollinParseResult Parse(string text, out bool enabled)
        {
            enabled = false;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool Is(int n, string s) => parts.Length > n && parts[n].Equals(s, StringComparison.OrdinalIgnoreCase);
            if (!Is(0, "/one") || !Is(1, "collin")) return CollinParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && Is(2, "help"))) return CollinParseResult.Help;
            if (parts.Length != 3) return CollinParseResult.Invalid;
            if (Is(2, "status")) return CollinParseResult.Status;
            if (!Is(2, "on") && !Is(2, "off") && !Is(2, "reset")) return CollinParseResult.Invalid;
            enabled = Is(2, "on");
            return CollinParseResult.Valid;
        }
    }
}
