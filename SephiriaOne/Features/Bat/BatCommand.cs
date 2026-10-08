#nullable disable
using System;

namespace SephiriaOne
{
    internal enum BatParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal static class BatCommand
    {
        internal static string Usage => L.T("Host only: /one bat hp-steal on|off; /one bat status|reset. On reduces Wingless Bat's costume HP steal from 5 to 1. Default off. /one save persists settings.");
        internal static BatParseResult Parse(string text, out bool enabled)
        {
            enabled = false;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool Is(int n, string s) => parts.Length > n && parts[n].Equals(s, StringComparison.OrdinalIgnoreCase);
            if (!Is(0, "/one") || !Is(1, "bat")) return BatParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && Is(2, "help"))) return BatParseResult.Help;
            if (parts.Length == 3 && Is(2, "status")) return BatParseResult.Status;
            if (parts.Length == 3 && Is(2, "reset")) return BatParseResult.Valid;
            if (parts.Length != 4 || !Is(2, "hp-steal") || (!Is(3, "on") && !Is(3, "off"))) return BatParseResult.Invalid;
            enabled = Is(3, "on");
            return BatParseResult.Valid;
        }
    }
}
