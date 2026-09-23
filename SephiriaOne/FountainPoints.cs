using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static class FountainPoints
    {
        private const string LimitKey = "DIMENSIONPOCKETLIMIT";
        private const string ContributionKey = "SEPHIRIAONE_FOUNTAINPOINTS";
        private const string OriginalLimitKey = "SEPHIRIAONE_FOUNTAIN_ORIGINALLIMIT";
        private const string AppliedLimitKey = "SEPHIRIAONE_FOUNTAIN_APPLIEDLIMIT";

        public static bool TryExecute(FountainCommand command, out string message)
        {
            // Commands come only from this machine's input box, never received chat.
            if (!NetworkServer.active)
            {
                message = "Only the host can change everyone's Wishing Fountain points.";
                return false;
            }

            DungeonManager dungeon = DungeonManager.Instance;
            if (!dungeon || !dungeon.isServer || !dungeon.constValueDictionary.TryGetValue(LimitKey, out int limit))
            {
                message = "Fountain session data is not ready. Enter town or a run first.";
                return false;
            }

            var inventories = new List<GridInventory>();
            var players = new List<PlayerAvatar>();
            var balances = new List<int>();
            var contributions = new List<int>();
            var seen = new HashSet<GridInventory>();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || spawner.netId == 0)
                {
                    continue;
                }

                PlayerAvatar player = spawner.PlayerAvatar;
                GridInventory inventory = player ? player.Inventory : null;
                if (!player || !player.isServer || player.netId == 0 || !inventory || !inventory.isServer || inventory.netId == 0)
                {
                    message = "A player is still initializing. Wait a moment and retry; nobody was changed.";
                    return false;
                }

                if (seen.Add(inventory))
                {
                    inventories.Add(inventory);
                    players.Add(player);
                    balances.Add(inventory.dimensionPocket);
                    player.customStats.TryGetValue(ContributionKey, out int contribution);
                    contributions.Add(contribution);
                }
            }

            int? originalLimit = dungeon.constValueDictionary.TryGetValue(OriginalLimitKey, out int original) ? original : (int?)null;
            int? appliedLimit = dungeon.constValueDictionary.TryGetValue(AppliedLimitKey, out int applied) ? applied : (int?)null;
            if (!command.TryPlanTracked(balances, contributions, limit, originalLimit, appliedLimit, out FountainPlan plan, out message))
            {
                return false;
            }

            // Validate the entire batch first, including carryover capacity. Both
            // writes use the game's existing synchronization for unmodified guests.
            if (plan.Limit != limit)
            {
                dungeon.constValueDictionary[LimitKey] = plan.Limit;
            }
            if (plan.OriginalLimit.HasValue)
            {
                dungeon.constValueDictionary[OriginalLimitKey] = plan.OriginalLimit.Value;
                dungeon.constValueDictionary[AppliedLimitKey] = plan.AppliedLimit.Value;
            }
            else
            {
                dungeon.constValueDictionary.Remove(OriginalLimitKey);
                dungeon.constValueDictionary.Remove(AppliedLimitKey);
            }

            int minimum = int.MaxValue;
            int maximum = 0;
            for (int i = 0; i < inventories.Count; i++)
            {
                inventories[i].NetworkdimensionPocket = plan.Points[i];
                if (plan.Contributions[i] == 0) players[i].customStats.Remove(ContributionKey);
                else players[i].customStats[ContributionKey] = plan.Contributions[i];
                minimum = System.Math.Min(minimum, plan.Points[i]);
                maximum = System.Math.Max(maximum, plan.Points[i]);
            }

            string points = minimum == maximum ? minimum.ToString() : $"{minimum}..{maximum}";
            string action = command.Operation == FountainOperation.Reset ? "Reset addon adjustments to" : "Updated";
            message = $"{action} Wishing Fountain points for {inventories.Count} player(s). Points now: {points}. Reopen the Fountain panel.";
            return true;
        }
    }
}
