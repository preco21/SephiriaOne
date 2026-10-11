namespace SephiriaOne
{
    internal static class TournamentSettings
    {
        internal const int DefaultDuration = 30, MinDuration = 10, MaxDuration = 3600;
        internal static bool ValidDuration(int seconds) => seconds >= MinDuration && seconds <= MaxDuration;
    }
}
