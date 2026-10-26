using System;
using System.Globalization;

namespace SephiriaOne
{
    internal static class ZombieSurvivalCommand
    {
        internal static string Usage => L.T("Zombie Survival: /one zombie start|stop|resetkda|status|help; duration 3..3600|reset (default 120s); count N|reset (positive integer, default 1); any-death on|off|reset (default off). Duration and count apply to the next match; count leaves at least one Human. Other settings apply live. /one save stores settings.") + " " +
            L.T("Zombie bonuses: /one zombie refill on|off|xN|N|reset (0.1..10, default x3); damage on|off|N%|N|reset (0..100, default 30%). Both default on; up to 2 decimals. Refill requires PvP balancing.") + " " +
            L.T("Dynamic Balancing: /one zombie dynamic on|off|reset (default off). Favors the minority, neutral at equal teams. Zombie refill x0.5..1.5; Zombie incoming damage 80..160%; Human incoming damage 60..100%. Changes ease over about 3s. Incoming switch controls both teams; manual values return when disabled.") + " " +
            L.T("Zombie HP potions: /one zombie block-hp on|off|reset (default on). Blocks Small, Large and Sample HP potions for current Zombies; Humans retain PvP potion rules.");
        internal static string DescribeHumanModifier() => L.F("[H] Incoming damage: {0}%.",
            (CombatMatchRuntime.ZombieHumanIncomingScale * 100).ToString("0.##", CultureInfo.InvariantCulture));
        internal static string DescribeModifiers()
        {
            var settings = SessionSettings.ZombieModifiers;
            string refill = settings.RefillNumber, incoming = settings.IncomingNumber;
            if (SessionSettings.ZombieDynamicBalance)
            {
                refill = CombatMatchRuntime.ZombieRefillScale.ToString("0.##", CultureInfo.InvariantCulture);
                incoming = (CombatMatchRuntime.ZombieIncomingScale * 100).ToString("0.##", CultureInfo.InvariantCulture);
            }
            return L.F("Dynamic Balancing: {0}.", L.T(SessionSettings.ZombieDynamicBalance ? "ON" : "OFF")) + " " +
                L.F("Zombie refill: {0} (x{1}, PvP balancing only); incoming damage: {2} ({3}%).",
                L.T(settings.RefillEnabled ? "ON" : "OFF"), refill,
                L.T(settings.IncomingEnabled ? "ON" : "OFF"), incoming) + " " +
                (SessionSettings.ZombieDynamicBalance ? DescribeHumanModifier() + " " : "") +
                L.F("Convert on any Human death: {0}.", L.T(SessionSettings.ZombieAnyDeath ? "ON" : "OFF")) + " " +
                L.T("[Z] Movement speed: 70%.") + " " +
                L.F("Block Zombie HP potions: {0}.", L.T(SessionSettings.ZombieBlockHpPotions ? "ON" : "OFF"));
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
                success = true; message = CombatMatchRuntime.DescribeZombie() + "\n" +
                    L.F("Starting Zombies: {0} for the next match (clamped to leave one Human).", SessionSettings.ZombieStartingCount) + "\n" + DescribeModifiers();
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
            else if (parts.Length == 4 && op == "count")
            {
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
                    success = SessionSettings.TrySetZombieStartingCount(ZombieSurvivalSettings.DefaultStartingCount, out message);
                else if (int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int count) && ZombieSurvivalSettings.ValidStartingCount(count))
                    success = SessionSettings.TrySetZombieStartingCount(count, out message);
            }
            else if (parts.Length == 4 && (op == "any-death" || op == "dynamic" || op == "block-hp"))
            {
                string value = parts[3].ToLowerInvariant();
                if (value == "on" || value == "off" || value == "reset")
                    success = op == "block-hp" ? SessionSettings.TrySetZombieBlockHpPotions(value != "off", out message) :
                        op == "dynamic" ? SessionSettings.TrySetZombieDynamicBalance(value == "on", out message) :
                        SessionSettings.TrySetZombieAnyDeath(value == "on", out message);
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
