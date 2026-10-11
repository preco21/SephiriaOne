using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static int TournamentDuration => policy.TournamentDuration;
        internal static bool TrySetTournamentDuration(int seconds, out string message)
        {
            message = TournamentCommand.Usage;
            if (!NetworkServer.active || !TournamentSettings.ValidDuration(seconds) || !EnsureResourceScope()) return false;
            policy.RecordTournamentDuration(seconds); intentRevision++; NotifySettingsChanged();
            message = L.F("Tournament round duration set to {0} seconds for the next tournament.", seconds); return true;
        }
    }
}
