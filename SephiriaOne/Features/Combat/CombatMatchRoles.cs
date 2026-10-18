namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        internal static long RoleRevision { get; private set; }
        internal static void RoleSettingsChanged() => RoleRevision++;

        private static bool TryRoleSettings(PlayerAvatar player, out CombatRoleSettings settings)
        {
            if (IsActiveChaser(player)) { settings = SessionSettings.PlayTagChaser; return true; }
            if (IsActiveZombie(player)) { settings = SessionSettings.ZombieModifiers; return true; }
            settings = default; return false;
        }
        internal static double IncomingRoleScale(PlayerAvatar player) =>
            TryRoleSettings(player, out var settings) && settings.IncomingEnabled ? settings.IncomingScale : 1;
        internal static double RefillRoleScale(PlayerAvatar player) =>
            TryRoleSettings(player, out var settings) && settings.RefillEnabled ? settings.RefillScale : 1;
    }
}
