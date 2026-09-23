using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static class FountainPoints
    {
        private const string LimitKey = "DIMENSIONPOCKETLIMIT";

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
            var balances = new List<int>();
            var seen = new HashSet<GridInventory>();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || spawner.netId == 0)
                {
                    continue;
                }

                PlayerAvatar player = spawner.PlayerAvatar;
                GridInventory inventory = player ? player.Inventory : null;
                if (!player || !inventory || !inventory.isServer || inventory.netId == 0)
                {
                    message = "A player is still initializing. Wait a moment and retry; nobody was changed.";
                    return false;
                }

                if (seen.Add(inventory))
                {
                    inventories.Add(inventory);
                    balances.Add(inventory.dimensionPocket);
                }
            }

            if (!command.TryPlan(balances, limit, out int[] updated, out int updatedLimit, out message))
            {
                return false;
            }

            // Validate the entire batch first, including carryover capacity. Both
            // writes use the game's existing synchronization for unmodified guests.
            if (updatedLimit != limit)
            {
                dungeon.constValueDictionary[LimitKey] = updatedLimit;
            }

            int minimum = int.MaxValue;
            int maximum = 0;
            for (int i = 0; i < inventories.Count; i++)
            {
                inventories[i].NetworkdimensionPocket = updated[i];
                minimum = System.Math.Min(minimum, updated[i]);
                maximum = System.Math.Max(maximum, updated[i]);
            }

            string points = minimum == maximum ? minimum.ToString() : $"{minimum}..{maximum}";
            message = $"Updated Wishing Fountain points for {inventories.Count} player(s). Points now: {points}. Reopen the Fountain panel.";
            return true;
        }
    }
}
