using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class TournamentCommand
    {
        internal static string Usage => L.T("Tournament: /one tournament start|stop|resetkda|status; /one tournament duration 10..3600 (seconds per round, default 30). Duration changes apply to the next tournament; /one save stores the duration.");
        internal static bool Execute(string[] parts, out bool success, out string message)
        {
            success = false; message = Usage;
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("tournament", StringComparison.OrdinalIgnoreCase)) return false;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length <= 3 && op == "help") { success = true; return true; }
            if (!Mirror.NetworkServer.active) { message = L.T("Only the host can control tournament."); return true; }
            if (parts.Length <= 3 && op == "status") { success = true; message = CombatMatchRuntime.DescribeTournament(); }
            else if (parts.Length == 3 && op == "start") success = CombatMatchRuntime.StartTournament(out message);
            else if (parts.Length == 3 && op == "resetkda") success = SessionSettings.TryResetFriendlyFireScores(out message);
            else if (parts.Length == 3 && op == "stop")
            {
                if (!CombatMatchRuntime.IsTournament) { message = L.T("No tournament is running."); return true; }
                CombatMatchRuntime.Stop(true, true); success = true; message = L.T("Tournament stopped.");
            }
            else if (parts.Length == 4 && op == "duration" && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture,
                out int seconds) && TournamentSettings.ValidDuration(seconds))
                success = SessionSettings.TrySetTournamentDuration(seconds, out message);
            return true;
        }
    }
}
