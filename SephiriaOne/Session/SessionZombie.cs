using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static int ZombieDuration => policy.ZombieDuration;
        internal static int ZombieStartingCount => policy.ZombieStartingCount;
        internal static bool ZombieAnyDeath => policy.ZombieAnyDeath;
        internal static bool ZombieDynamicBalance => policy.ZombieDynamicBalance;
        internal static CombatRoleSettings ZombieModifiers => policy.ZombieModifiers;
        internal static bool TrySetZombieDuration(int seconds, out string message)
        {
            message = ZombieSurvivalCommand.Usage;
            if (!NetworkServer.active || !ZombieSurvivalSettings.ValidDuration(seconds) || !EnsureResourceScope()) return false;
            if (policy.ZombieDuration != seconds)
            { policy.RecordZombieDuration(seconds); intentRevision++; NotifySettingsChanged(); }
            message = L.F("Zombie Survival duration set to {0} seconds for the next match.", seconds); return true;
        }
        internal static bool TrySetZombieAnyDeath(bool enabled, out string message)
        {
            message = ZombieSurvivalCommand.Usage;
            if (!NetworkServer.active || !EnsureResourceScope()) return false;
            if (policy.ZombieAnyDeath != enabled)
            { policy.RecordZombieAnyDeath(enabled); intentRevision++; NotifySettingsChanged(); }
            message = L.F("Convert on any Human death: {0}.", L.T(enabled ? "ON" : "OFF")); return true;
        }
        internal static bool TrySetZombieStartingCount(int count, out string message)
        {
            message = ZombieSurvivalCommand.Usage;
            if (!NetworkServer.active || !ZombieSurvivalSettings.ValidStartingCount(count) || !EnsureResourceScope()) return false;
            if (policy.ZombieStartingCount != count)
            { policy.RecordZombieStartingCount(count); intentRevision++; NotifySettingsChanged(); }
            message = L.F("Starting Zombies: {0} for the next match (clamped to leave one Human).", count); return true;
        }
        internal static bool TrySetZombieModifiers(CombatRoleSettings settings, out string message)
        {
            message = ZombieSurvivalCommand.Usage;
            if (!NetworkServer.active || !EnsureResourceScope()) return false;
            if (!policy.ZombieModifiers.Equals(settings))
            {
                bool effectiveChange = !policy.ZombieDynamicBalance ||
                    policy.ZombieModifiers.RefillEnabled != settings.RefillEnabled ||
                    policy.ZombieModifiers.IncomingEnabled != settings.IncomingEnabled;
                policy.RecordZombieModifiers(settings); intentRevision++;
                if (effectiveChange) CombatMatchRuntime.RoleSettingsChanged();
                NotifySettingsChanged();
            }
            message = ZombieSurvivalCommand.DescribeModifiers(); return true;
        }
        internal static bool TrySetZombieDynamicBalance(bool enabled, out string message)
        {
            message = ZombieSurvivalCommand.Usage;
            if (!NetworkServer.active || !EnsureResourceScope()) return false;
            if (policy.ZombieDynamicBalance != enabled)
            {
                policy.RecordZombieDynamicBalance(enabled); intentRevision++;
                CombatMatchRuntime.RoleSettingsChanged(); NotifySettingsChanged();
            }
            message = ZombieSurvivalCommand.DescribeModifiers(); return true;
        }
    }
}
