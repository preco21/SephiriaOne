namespace SephiriaOne
{
    internal static class DeathmatchSettings
    {
        internal const int DefaultDuration = 300, MinDuration = 10, MaxDuration = 3600;
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
    }
}
