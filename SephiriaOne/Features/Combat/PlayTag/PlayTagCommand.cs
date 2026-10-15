using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class PlayTagCommand
    {
        internal static string Usage => L.T("Play Tag: /one tag start|stop|resetkda|status|help; /one tag duration 3..3600|reset (initial fuse seconds, default 30). Duration changes apply to the next match; /one save stores the duration.");
        internal static bool Execute(string[] parts, out bool success, out string message)
        {
            success = false; message = Usage;
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("tag", StringComparison.OrdinalIgnoreCase)) return false;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length == 3 && op == "help") { success = true; return true; }
            if (!Mirror.NetworkServer.active) { message = L.T("Only the host can control Play Tag."); return true; }
            if (parts.Length <= 3 && op == "status") { success = true; message = CombatMatchRuntime.DescribePlayTag(); }
            else if (parts.Length == 3 && op == "start") success = CombatMatchRuntime.StartPlayTag(out message);
            else if (parts.Length == 3 && op == "resetkda") success = SessionSettings.TryResetFriendlyFireScores(out message);
            else if (parts.Length == 3 && op == "stop")
            {
                if (!CombatMatchRuntime.IsPlayTag) { message = L.T("No Play Tag match is running."); return true; }
                CombatMatchRuntime.Stop(true, true); success = true; message = L.T("Play Tag stopped.");
            }
            else if (parts.Length == 4 && op == "duration")
            {
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
                    success = SessionSettings.TrySetPlayTagDuration(PlayTagSettings.DefaultDuration, out message);
                else if (int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) &&
                    PlayTagSettings.ValidDuration(seconds)) success = SessionSettings.TrySetPlayTagDuration(seconds, out message);
            }
            return true;
        }
    }
}
