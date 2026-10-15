namespace SephiriaOne
{
    internal static class PlayTagSettings
    {
        internal const int DefaultDuration = 30, MinDuration = 3, MaxDuration = 3600;
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
    }
}
