using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class FriendlyFireSelectionCommand
    {
        internal static string Usage => L.T("Players: /one friendlyfire players | player <id> on|off | players all|reset. No selection includes everyone.");

        internal static bool Execute(string[] parts, object expectedPlayer, out bool success, out string message)
        {
            success = false; message = Usage;
            if (parts.Length < 3 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("friendlyfire", StringComparison.OrdinalIgnoreCase)) return false;
            bool list = parts[2].Equals("players", StringComparison.OrdinalIgnoreCase);
            if (!list && !parts[2].Equals("player", StringComparison.OrdinalIgnoreCase)) return false;
            if (list && parts.Length == 3)
                success = SessionSettings.TryDescribeFriendlyFirePlayers(out message);
            else if (list && parts.Length == 4 && parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
                success = SessionSettings.TryResetFriendlyFireSelection(out message);
            else if (list && parts.Length == 4 && parts[3].Equals("all", StringComparison.OrdinalIgnoreCase))
                success = SessionSettings.TrySelectAllFriendlyFirePlayers(out message);
            else if (!list && parts.Length == 5 && uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) &&
                (parts[4].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[4].Equals("off", StringComparison.OrdinalIgnoreCase)))
                success = SessionSettings.TrySelectFriendlyFirePlayer(id, parts[4].Equals("on", StringComparison.OrdinalIgnoreCase), expectedPlayer, out message);
            return true;
        }
    }
}
