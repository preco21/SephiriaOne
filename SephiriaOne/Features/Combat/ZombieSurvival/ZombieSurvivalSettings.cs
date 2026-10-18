namespace SephiriaOne
{
    internal static class ZombieSurvivalSettings
    {
        internal const int DefaultDuration = 120, MinDuration = 3, MaxDuration = 3600;
        internal const decimal DefaultRefillMultiplier = 3m, DefaultIncomingPercent = 50m;
        internal static readonly CombatRoleSettings DefaultModifiers =
            new CombatRoleSettings(true, DefaultRefillMultiplier, true, DefaultIncomingPercent);
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
    }
}
