using System;

namespace SephiriaOne
{
    internal enum ItemRestrictionParseResult { NotCommand, Help, Status, Invalid, Valid }

    internal static class ItemRestrictionCommand
    {
        internal static string Usage => L.T("Host only: /one items unlock on|off, /one items status, /one items reset. Default off. Save for future sessions: /one save.");

        internal static ItemRestrictionParseResult Parse(string text, out bool enabled)
        {
            enabled = false;
            string[] parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool Is(int index, string value) => parts.Length > index && parts[index].Equals(value, StringComparison.OrdinalIgnoreCase);
            if (!Is(0, "/one") || !Is(1, "items")) return ItemRestrictionParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && Is(2, "help"))) return ItemRestrictionParseResult.Help;
            if (parts.Length == 3 && Is(2, "status")) return ItemRestrictionParseResult.Status;
            if (parts.Length == 3 && Is(2, "reset")) return ItemRestrictionParseResult.Valid;
            if (parts.Length != 4 || !Is(2, "unlock") || (!Is(3, "on") && !Is(3, "off"))) return ItemRestrictionParseResult.Invalid;
            enabled = Is(3, "on");
            return ItemRestrictionParseResult.Valid;
        }
    }
}
