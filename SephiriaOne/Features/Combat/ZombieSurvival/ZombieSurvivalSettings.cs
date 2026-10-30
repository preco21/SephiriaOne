namespace SephiriaOne
{
    internal static class ZombieSurvivalSettings
    {
        internal const int DefaultDuration = 40, MinDuration = 3, MaxDuration = 3600;
        internal const int DefaultStartingCount = 1;
        internal const decimal DefaultRefillMultiplier = 3m, DefaultIncomingPercent = 30m;
        internal static readonly CombatRoleSettings DefaultModifiers =
            new CombatRoleSettings(true, DefaultRefillMultiplier, true, DefaultIncomingPercent);
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
        internal static bool ValidStartingCount(int count) => count >= 1;
    }
}
