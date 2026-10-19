using Mirror;

namespace SephiriaOne
{
    // Shared by recovery rules. Effective combat includes the current mode's
    // countdown, pair/team membership and connection lifetime, not Balance.
    internal static class PvpParticipation
    {
        internal static bool IsActive(PlayerAvatar player)
        {
            if (!FriendlyFireFeature.Available || !NetworkServer.active || !player || !player.isServer ||
                !SessionSettings.FriendlyFireForHit.Enabled) return false;
            // Pruning stale selections can stop a match. Re-read the effective
            // toggle after admission so recovery resumes in the same callback.
            return FriendlyFireParticipants.Includes(player) && CombatMatchRuntime.AllowsParticipant(player) &&
                SessionSettings.FriendlyFireForHit.Enabled;
        }

        internal static bool CanShareRecovery(PlayerAvatar source, PlayerAvatar recipient) =>
            !IsActive(source) && !IsActive(recipient);
    }
}
