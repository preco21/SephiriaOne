using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class ZombieSurvivalCommand
    {
        internal static string Usage => L.T("Zombie Survival: /one zombie start|stop|resetkda|status|help; duration 3..3600|reset (default 120s); any-death on|off|reset (default off). Duration applies to the next match; other settings apply live. /one save stores settings.") + " " +
            L.T("Zombie bonuses: /one zombie refill on|off|xN|N|reset (0.1..10, default x3); damage on|off|N%|N|reset (0..100, default 30%). Both default on; up to 2 decimals. Refill requires PvP balancing.");
        internal static string DescribeModifiers()
        {
            var settings = SessionSettings.ZombieModifiers;
            return L.F("Zombie refill: {0} (x{1}, PvP balancing only); incoming damage: {2} ({3}%).",
                L.T(settings.RefillEnabled ? "ON" : "OFF"), settings.RefillNumber,
                L.T(settings.IncomingEnabled ? "ON" : "OFF"), settings.IncomingNumber) + " " +
                L.F("Convert on any Human death: {0}.", L.T(SessionSettings.ZombieAnyDeath ? "ON" : "OFF"));
        }
        internal static bool Execute(string[] parts, out bool success, out string message)
        {
            success = false; message = Usage;
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("zombie", StringComparison.OrdinalIgnoreCase)) return false;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length == 3 && op == "help") { success = true; return true; }
            if (!Mirror.NetworkServer.active) { message = L.T("Only the host can control Zombie Survival."); return true; }
            if (parts.Length <= 3 && op == "status")
            {
                SessionSettings.EnsureResourceScope();
                success = true; message = CombatMatchRuntime.DescribeZombie() + "\n" + DescribeModifiers();
            }
            else if (parts.Length == 3 && op == "start") success = CombatMatchRuntime.StartZombie(out message);
            else if (parts.Length == 3 && op == "resetkda") success = SessionSettings.TryResetFriendlyFireScores(out message);
            else if (parts.Length == 3 && op == "stop")
            {
                if (!CombatMatchRuntime.IsZombieSurvival) { message = L.T("No Zombie Survival match is running."); return true; }
                CombatMatchRuntime.Stop(true, true); success = true; message = L.T("Zombie Survival stopped.");
            }
            else if (parts.Length == 4 && op == "duration")
            {
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
                    success = SessionSettings.TrySetZombieDuration(ZombieSurvivalSettings.DefaultDuration, out message);
                else if (int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) && ZombieSurvivalSettings.ValidDuration(seconds))
                    success = SessionSettings.TrySetZombieDuration(seconds, out message);
            }
            else if (parts.Length == 4 && op == "any-death")
            {
                string value = parts[3].ToLowerInvariant();
                if (value == "on" || value == "off" || value == "reset") success = SessionSettings.TrySetZombieAnyDeath(value == "on", out message);
            }
            else if (parts.Length == 4 && (op == "refill" || op == "damage"))
            {
                if (!SessionSettings.EnsureResourceScope()) return true;
                var settings = SessionSettings.ZombieModifiers;
                bool refill = op == "refill";
                string value = parts[3].ToLowerInvariant();
                if (value == "on" || value == "off")
                    settings = refill ? settings.WithRefill(enabled: value == "on") : settings.WithIncoming(enabled: value == "on");
                else if (value == "reset")
                    settings = refill ? settings.WithRefill(true, ZombieSurvivalSettings.DefaultRefillMultiplier) : settings.WithIncoming(true, ZombieSurvivalSettings.DefaultIncomingPercent);
                else if (CombatRoleSettings.TryNumber(value, refill, out decimal number))
                    settings = refill ? settings.WithRefill(multiplier: number) : settings.WithIncoming(percent: number);
                else return true;
                success = SessionSettings.TrySetZombieModifiers(settings, out message);
            }
            return true;
        }
    }
}
