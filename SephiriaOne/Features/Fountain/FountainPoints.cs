using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static class FountainPoints
    {
        internal const string LimitKey = "DIMENSIONPOCKETLIMIT";
        internal const string ContributionKey = "SEPHIRIAONE_FOUNTAINPOINTS";
        internal const string OriginalLimitKey = "SEPHIRIAONE_FOUNTAIN_ORIGINALLIMIT";
        internal const string AppliedLimitKey = "SEPHIRIAONE_FOUNTAIN_APPLIEDLIMIT";

        public static bool TryExecute(FountainCommand command, out string message)
        {
            // Commands come only from this machine's input box, never received chat.
            if (!NetworkServer.active)
            {
                message = "Only the host can change everyone's Wishing Fountain points.";
                return false;
            }

            bool reset = command.Operation == FountainOperation.Reset || (command.Operation == FountainOperation.Multiply && command.Amount == 1);
            if (!SessionSettings.PrepareCommand("fountain", reset, out HostCommandContext context, out message)) return false;

            DungeonManager dungeon = context.Dungeon;
            if (!dungeon || !dungeon.isServer || !dungeon.constValueDictionary.TryGetValue(LimitKey, out int limit))
            {
                message = "Fountain session data is not ready. Enter town or a run first.";
                return false;
            }

            var inventories = new List<GridInventory>();
            List<PlayerAvatar> players = context.Players;
            var balances = new List<int>();
            var contributions = new List<int>();
            foreach (PlayerAvatar player in players)
            {
                GridInventory inventory = player.Inventory;
                inventories.Add(inventory);
                balances.Add(inventory.dimensionPocket);
                player.customStats.TryGetValue(ContributionKey, out int contribution);
                contributions.Add(contribution);
            }

            int? originalLimit = dungeon.constValueDictionary.TryGetValue(OriginalLimitKey, out int original) ? original : (int?)null;
            int? appliedLimit = dungeon.constValueDictionary.TryGetValue(AppliedLimitKey, out int applied) ? applied : (int?)null;
            if (!SessionSettings.TryPlanFountain(command, balances, contributions, limit, originalLimit, appliedLimit, out FountainPlan plan, out message))
            {
                return false;
            }

            StateWriteBatch batch = context.CreateBatch();
            NativeStateWrites.Fountain(batch, dungeon, players, plan);
            if (!SessionSettings.Commit("fountain", batch, () => SessionSettings.Remember(command), out message)) return false;
            int minimum = int.MaxValue;
            int maximum = 0;
            for (int i = 0; i < inventories.Count; i++)
            {
                minimum = System.Math.Min(minimum, plan.Points[i]);
                maximum = System.Math.Max(maximum, plan.Points[i]);
            }

            string points = minimum == maximum ? minimum.ToString() : $"{minimum}..{maximum}";
            string action = reset ? "Reset addon adjustments to" : "Updated";
            message = $"{action} Wishing Fountain points for {inventories.Count} player(s). Points now: {points}. Reopen the Fountain panel.";
            return true;
        }

        // Derive only the carryover cap; never replay a points command.
        internal static ReconcileResult RestoreCarryoverLimit(DungeonManager dungeon, ISet<PlayerAvatar> configuredPlayers)
        {
            if (!NetworkServer.active || !dungeon || !dungeon.isServer ||
                !dungeon.constValueDictionary.TryGetValue(LimitKey, out int limit))
                return ReconcileResult.Waiting("Fountain session data is not ready.");
            var balances = new List<int>();
            var contributions = new List<int>();
            var participants = new List<HostPlayer>();
            var seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
            bool allReady = true;
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || spawner.netId == 0) continue;
                if (!HostStateAdapter.IsReady(spawner)) { allReady = false; continue; }
                PlayerAvatar player = spawner.PlayerAvatar;
                if (!seen.Add(player)) continue;
                player.customStats.TryGetValue(ContributionKey, out int contribution);
                if (!configuredPlayers.Contains(player) && contribution == 0) continue;
                // A negative native capacity needs no allowance and must not block others.
                balances.Add(System.Math.Max(0, player.Inventory.dimensionPocket));
                contributions.Add(contribution);
                participants.Add(new HostPlayer(spawner));
            }
            if (balances.Count > 0)
            {
                var context = new HostCommandContext(dungeon, participants);
                int? original = dungeon.constValueDictionary.TryGetValue(OriginalLimitKey, out int first) ? first : (int?)null;
                int? applied = dungeon.constValueDictionary.TryGetValue(AppliedLimitKey, out int last) ? last : (int?)null;
                if (!new FountainCommand(FountainOperation.Add, 0).TryPlanTracked(balances, contributions, limit,
                    original, applied, out FountainPlan plan, out string error))
                    return ReconcileResult.Rejected(error);
                StateWriteBatch batch = context.CreateBatch();
                NativeStateWrites.Limit(batch, dungeon, plan);
                if (!batch.TryCommit(out error))
                {
                    if (batch.MayHaveWritten) SessionSettings.RecordFault("fountain", batch, error);
                    return batch.MayHaveWritten ? ReconcileResult.Faulted(error) : ReconcileResult.Waiting(error);
                }
                if (plan.Limit != limit)
                    UnityEngine.Debug.Log($"[SephiriaOne] Reconciled Fountain carryover limit: {limit} -> {plan.Limit}.");
            }
            return allReady ? ReconcileResult.Applied() : ReconcileResult.Waiting("Fountain participants are still initializing.");
        }
    }
}
