using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private static readonly HashSet<PlayerAvatar> fountainPlayers =
            new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);

        private static bool IsFountainEnrolled(PlayerAvatar player) => fountainPlayers.Contains(player) ||
            (player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution) && contribution != 0);

        private static (bool, GridInventory, int, int) CaptureFountain(PlayerAvatar player)
        {
            bool enrolled = IsFountainEnrolled(player);
            player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution);
            return (enrolled, enrolled ? player.Inventory : null,
                enrolled ? player.Inventory.dimensionPocket : 0, enrolled ? contribution : 0);
        }

        private static ReconcileResult MaintainFountainMultiplier(HostPlayer subject)
        {
            PlayerAvatar player = subject.Player;
            // A rejected inheritance must not partially apply this family.
            if (!policy.HasFountainMultiplier || !fountainPlayers.Contains(player)) return ReconcileResult.Applied();
            if (failedBatch != null) return ReconcileResult.Waiting(L.T("Another state write is faulted."));
            SessionPlayerSnapshot snapshot = Capture(player);
            if (!snapshot.FountainLimit.HasValue) return ReconcileResult.Waiting(L.T("Fountain limit is not ready."));
            bool valid = policy.TryPlanFountainMultiplier(snapshot, out FountainPlan plan, out string error);
            if (!valid)
            {
                // Restore only this player's contribution. Global cap ownership
                // must remain intact for the other configured participants.
                var reset = new FountainCommand(FountainOperation.Reset, 0);
                if (!reset.TryPlanTracked(new[] { snapshot.FountainPoints }, new[] { snapshot.FountainContribution },
                    snapshot.FountainLimit.Value, null, null, out FountainPlan native, out _))
                    return ReconcileResult.Suspended(error + L.T(" Native capacity could not be restored; values were left unchanged."));
                plan = new FountainPlan(native.Points, native.Contributions, snapshot.FountainLimit.Value,
                    snapshot.OriginalLimit, snapshot.AppliedLimit);
            }
            var context = new HostCommandContext(dungeon, new[] { subject });
            StateWriteBatch batch = context.CreateBatch();
            NativeStateWrites.Fountain(batch, dungeon, new[] { player }, plan);
            if (!batch.TryCommit(out string writeError))
            {
                if (batch.MayHaveWritten) RecordFault("fountain", batch, writeError);
                return ReconcileResult.Faulted(writeError);
            }
            if (valid) return ReconcileResult.Applied();
            if (!players.TryGetResult(subject, "fountain-multiplier", out var previous) || previous.State != ReconcileState.NativeFallback)
                Report(L.F("Fountain multiplier uses native capacity for player {0}: {1} Native capacity restored. It will retry when native inputs change.", player.netId, L.TrimSuffix(error, " Nobody was changed.")), false);
            return ReconcileResult.NativeFallback(L.TrimSuffix(error, " Nobody was changed.") + L.T(" Native capacity restored."));
        }
    }
}
