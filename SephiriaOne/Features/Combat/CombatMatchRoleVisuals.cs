namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static PlayerAvatar tagVisualCarrier;
        private static long tagAssignment, tagAssignmentShown = -1;

        internal static bool IsCombatRoleVisualCurrent(PlayerAvatar player, FriendlyFireParticipants.Receipt receipt, long match) =>
            IsZombieVisualCurrent(player, receipt, match) || IsPlayTagVisualCurrent(player, receipt, match);

        private static bool IsPlayTagVisualCurrent(PlayerAvatar player, FriendlyFireParticipants.Receipt receipt, long match) =>
            !stopping && phase == Phase.Active && CurrentTagRevival(receipt, match, out int index) &&
            ReferenceEquals(receipt.Player, player) && tagBomb != null && !tagBomb.Resolved && tagBomb.Carrier == index && tagPlayers[index].Subject.IsReady;

        private static void RefreshPlayTagVisuals()
        {
            if (!IsPlayTag || phase != Phase.Active || !CurrentScope) return;
            long match = token, assignment = tagAssignment;
            var next = tagBomb == null || tagBomb.Resolved ? null : tagPlayers[tagBomb.Carrier].Subject.Player;
            if (tagVisualCarrier && !ReferenceEquals(tagVisualCarrier, next)) CombatRoleVisuals.Update(tagVisualCarrier);
            if (!IsPlayTag || token != match || tagAssignment != assignment) return;
            tagVisualCarrier = next;
            if (!next) return;
            var receipt = FriendlyFireParticipants.Capture(next);
            CombatRoleVisuals.Update(next);
            if (assignment == tagAssignmentShown || assignment != tagAssignment || !IsPlayTagVisualCurrent(next, receipt, match)) return;
            // Consume before native presentation callbacks; readiness restoration,
            // respawn and marker replacement must not replay this transition.
            tagAssignmentShown = assignment;
            CombatImpactVisuals.ShowAssignment(next);
        }

        private static void ClearPlayTagVisuals()
        {
            tagVisualCarrier = null; tagAssignment = 0; tagAssignmentShown = -1;
            CombatRoleVisuals.Clear();
        }

    }
}
