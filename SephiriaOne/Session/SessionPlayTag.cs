using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static int PlayTagDuration => policy.PlayTagDuration;
        internal static bool TrySetPlayTagDuration(int seconds, out string message)
        {
            message = PlayTagCommand.Usage;
            if (!NetworkServer.active || !PlayTagSettings.ValidDuration(seconds) || !EnsureResourceScope()) return false;
            policy.RecordPlayTagDuration(seconds); intentRevision++; NotifySettingsChanged();
            message = L.F("Play Tag initial fuse set to {0} seconds for the next match.", seconds); return true;
        }
    }
}
