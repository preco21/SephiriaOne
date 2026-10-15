using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class DeathmatchCommand
    {
        internal static string Usage => L.T("Deathmatch: /one deathmatch start|stop|resetkda|status; /one deathmatch duration 10..3600 (seconds, default 300). Duration changes apply to the next match; /one save stores the duration.");
        internal static bool Execute(string[] parts, out bool success, out string message)
        {
            success = false; message = Usage;
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("deathmatch", StringComparison.OrdinalIgnoreCase)) return false;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length <= 3 && op == "help") { success = true; return true; }
            if (!Mirror.NetworkServer.active) { message = L.T("Only the host can control deathmatch."); return true; }
            if (parts.Length <= 3 && op == "status") { success = true; message = CombatMatchRuntime.Describe(); }
            else if (parts.Length == 3 && op == "start") success = CombatMatchRuntime.Start(out message);
            else if (parts.Length == 3 && op == "resetkda")
            {
                if (CombatMatchRuntime.IsRunning && !CombatMatchRuntime.IsDeathmatch)
                { message = L.T("Deathmatch is not running. Use the active match's resetkda command."); return true; }
                success = SessionSettings.TryResetFriendlyFireScores(out message);
            }
            else if (parts.Length == 3 && op == "stop")
            {
                if (CombatMatchRuntime.IsTournament) { message = L.T("Tournament is running. Use /one tournament stop."); return true; }
                if (CombatMatchRuntime.IsPlayTag) { message = L.T("Play Tag is running. Use /one tag stop."); return true; }
                CombatMatchRuntime.Stop(true, true); success = true; message = L.T("Deathmatch stopped.");
            }
            else if (parts.Length == 4 && op == "duration" && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture,
                out int seconds) && DeathmatchSettings.ValidDuration(seconds))
                success = SessionSettings.TrySetDeathmatchDuration(seconds, out message);
            return true;
        }
    }
}
