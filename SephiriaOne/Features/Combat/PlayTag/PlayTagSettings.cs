namespace SephiriaOne
{
    internal static class PlayTagSettings
    {
        internal const int DefaultDuration = 30, MinDuration = 3, MaxDuration = 3600;
        internal const int DefaultRounds = 1, MinRounds = 1, MaxRounds = 100;
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
        internal static bool ValidRounds(int rounds) => rounds >= MinRounds && rounds <= MaxRounds;
    }
}
