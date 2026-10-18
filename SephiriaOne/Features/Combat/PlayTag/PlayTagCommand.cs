using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class PlayTagCommand
    {
        internal static string Usage => L.T("Play Tag: /one tag start|stop|resetkda|status|help; /one tag duration 3..3600|reset (initial fuse seconds, default 30). Duration changes apply to the next match; /one save stores the duration.") + " " +
            L.T("Rounds: /one tag rounds 1..100|reset (default 1). Changes apply to the next match; /one save stores the round count.") + " " +
            L.T("Chaser: /one tag chaser-refill on|off|xN|N|reset (0.1..10, default x2); /one tag chaser-damage on|off|N%|N|reset (0..100, default 75%). Both default on; up to 2 decimals. Changes apply live; refill requires PvP balancing. /one save stores these settings.");
        internal static string DescribeChaser()
        {
            var settings = SessionSettings.PlayTagChaser;
            return L.F("Chaser refill: {0} (x{1}, PvP balancing only); incoming damage: {2} ({3}%).",
                L.T(settings.RefillEnabled ? "ON" : "OFF"), settings.RefillNumber,
                L.T(settings.IncomingEnabled ? "ON" : "OFF"), settings.IncomingNumber);
        }
        internal static bool Execute(string[] parts, out bool success, out string message)
        {
            success = false; message = Usage;
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("tag", StringComparison.OrdinalIgnoreCase)) return false;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length == 3 && op == "help") { success = true; return true; }
            if (!Mirror.NetworkServer.active) { message = L.T("Only the host can control Play Tag."); return true; }
            if (parts.Length <= 3 && op == "status")
            {
                SessionSettings.EnsureResourceScope();
                success = true; message = CombatMatchRuntime.DescribePlayTag() + "\n" + DescribeChaser();
            }
            else if (parts.Length == 3 && op == "start") success = CombatMatchRuntime.StartPlayTag(out message);
            else if (parts.Length == 3 && op == "resetkda") success = SessionSettings.TryResetFriendlyFireScores(out message);
            else if (parts.Length == 3 && op == "stop")
            {
                if (!CombatMatchRuntime.IsPlayTag) { message = L.T("No Play Tag match is running."); return true; }
                CombatMatchRuntime.Stop(true, true); success = true; message = L.T("Play Tag stopped.");
            }
            else if (parts.Length == 4 && op == "rounds")
            {
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
                    success = SessionSettings.TrySetPlayTagRounds(PlayTagSettings.DefaultRounds, out message);
                else if (int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int rounds) &&
                    PlayTagSettings.ValidRounds(rounds)) success = SessionSettings.TrySetPlayTagRounds(rounds, out message);
            }
            else if (parts.Length == 4 && op == "duration")
            {
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
                    success = SessionSettings.TrySetPlayTagDuration(PlayTagSettings.DefaultDuration, out message);
                else if (int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) &&
                    PlayTagSettings.ValidDuration(seconds)) success = SessionSettings.TrySetPlayTagDuration(seconds, out message);
            }
            else if (parts.Length == 4 && (op == "chaser-refill" || op == "chaser-damage"))
            {
                // Scope binding can load the preset before the first LateUpdate.
                // Derive this single-option edit from that current policy.
                if (!SessionSettings.EnsureResourceScope()) return true;
                var settings = SessionSettings.PlayTagChaser;
                bool refill = op == "chaser-refill";
                string value = parts[3].ToLowerInvariant();
                if (value == "on" || value == "off")
                    settings = refill ? settings.WithRefill(enabled: value == "on") : settings.WithIncoming(enabled: value == "on");
                else if (value == "reset")
                    settings = refill ? settings.WithRefill(true, CombatRoleSettings.DefaultRefillMultiplier) :
                        settings.WithIncoming(true, CombatRoleSettings.DefaultIncomingPercent);
                else if (CombatRoleSettings.TryNumber(value, refill, out decimal number))
                    settings = refill ? settings.WithRefill(multiplier: number) : settings.WithIncoming(percent: number);
                else return true;
                success = SessionSettings.TrySetPlayTagChaser(settings, out message);
            }
            return true;
        }
    }
}
