using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static int PlayTagDuration => policy.PlayTagDuration;
        internal static int PlayTagRounds => policy.PlayTagRounds;
        internal static bool TrySetPlayTagRounds(int rounds, out string message)
        {
            message = PlayTagCommand.Usage;
            if (!NetworkServer.active || !PlayTagSettings.ValidRounds(rounds) || !EnsureResourceScope()) return false;
            if (policy.PlayTagRounds != rounds)
            {
                policy.RecordPlayTagRounds(rounds); intentRevision++; NotifySettingsChanged();
            }
            message = L.F("Play Tag round count set to {0} for the next match.", rounds); return true;
        }
        internal static PlayTagChaserSettings PlayTagChaser => policy.PlayTagChaser;
        internal static bool TrySetPlayTagChaser(PlayTagChaserSettings settings, out string message)
        {
            message = PlayTagCommand.Usage;
            if (!NetworkServer.active || !EnsureResourceScope()) return false;
            if (!policy.PlayTagChaser.Equals(settings))
            {
                policy.RecordPlayTagChaser(settings); intentRevision++;
                CombatMatchRuntime.ChaserSettingsChanged(); NotifySettingsChanged();
            }
            message = PlayTagCommand.DescribeChaser(); return true;
        }
        internal static bool TrySetPlayTagDuration(int seconds, out string message)
        {
            message = PlayTagCommand.Usage;
            if (!NetworkServer.active || !PlayTagSettings.ValidDuration(seconds) || !EnsureResourceScope()) return false;
            policy.RecordPlayTagDuration(seconds); intentRevision++; NotifySettingsChanged();
            message = L.F("Play Tag initial fuse set to {0} seconds for the next match.", seconds); return true;
        }
    }
}
