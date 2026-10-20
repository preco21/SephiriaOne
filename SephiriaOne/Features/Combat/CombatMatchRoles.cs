namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        internal static long RoleRevision { get; private set; }
        internal static void RoleSettingsChanged() => RoleRevision++;
        private static ZombieDynamicBalance zombieBalance;
        internal static double ZombieRefillScale => SessionSettings.ZombieDynamicBalance
            ? zombieBalance.RefillScale(UnityEngine.Time.unscaledTime) : SessionSettings.ZombieModifiers.RefillScale;
        internal static double ZombieIncomingScale => SessionSettings.ZombieDynamicBalance
            ? zombieBalance.IncomingScale(UnityEngine.Time.unscaledTime) : SessionSettings.ZombieModifiers.IncomingScale;

        private static bool TryRoleSettings(PlayerAvatar player, out CombatRoleSettings settings, out bool zombie)
        {
            zombie = false;
            if (IsActiveChaser(player)) { settings = SessionSettings.PlayTagChaser; return true; }
            if (IsActiveZombie(player)) { settings = SessionSettings.ZombieModifiers; zombie = true; return true; }
            settings = default; return false;
        }
        internal static double IncomingRoleScale(PlayerAvatar player) =>
            TryRoleSettings(player, out var settings, out bool zombie) && settings.IncomingEnabled
                ? zombie ? ZombieIncomingScale : settings.IncomingScale : 1;
        internal static double RefillRoleScale(PlayerAvatar player) =>
            TryRoleSettings(player, out var settings, out bool zombie) && settings.RefillEnabled
                ? zombie ? ZombieRefillScale : settings.RefillScale : 1;
    }
}
