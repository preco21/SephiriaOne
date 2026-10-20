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
        internal static double ZombieHumanIncomingScale => SessionSettings.ZombieDynamicBalance && SessionSettings.ZombieModifiers.IncomingEnabled
            ? zombieBalance.HumanIncomingScale(UnityEngine.Time.unscaledTime) : 1d;

        private enum ModifierRole { None, Chaser, Zombie, Human }
        private static bool TryRoleSettings(PlayerAvatar player, out CombatRoleSettings settings, out ModifierRole role)
        {
            if (IsActiveChaser(player)) { settings = SessionSettings.PlayTagChaser; role = ModifierRole.Chaser; return true; }
            if (TryActiveZombieRole(player, out bool zombie) && (zombie || SessionSettings.ZombieDynamicBalance))
            {
                settings = SessionSettings.ZombieModifiers;
                role = zombie ? ModifierRole.Zombie : ModifierRole.Human;
                return true;
            }
            settings = default; role = ModifierRole.None; return false;
        }
        internal static double IncomingRoleScale(PlayerAvatar player) =>
            TryRoleSettings(player, out var settings, out var role) && settings.IncomingEnabled
                ? role == ModifierRole.Zombie ? ZombieIncomingScale :
                    role == ModifierRole.Human ? ZombieHumanIncomingScale : settings.IncomingScale : 1;
        internal static double RefillRoleScale(PlayerAvatar player) =>
            TryRoleSettings(player, out var settings, out var role) && settings.RefillEnabled
                ? role == ModifierRole.Zombie ? ZombieRefillScale :
                    role == ModifierRole.Human ? 1 : settings.RefillScale : 1;
    }
}
