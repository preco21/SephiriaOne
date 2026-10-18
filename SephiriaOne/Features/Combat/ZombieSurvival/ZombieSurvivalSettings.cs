namespace SephiriaOne
{
    internal static class ZombieSurvivalSettings
    {
        internal const int DefaultDuration = 120, MinDuration = 3, MaxDuration = 3600;
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
    }
}
