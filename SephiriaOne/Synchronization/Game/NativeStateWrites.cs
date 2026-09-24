using System.Collections.Generic;

namespace SephiriaOne
{
    internal static class NativeStateWrites
    {
        public static void Dictionary(StateWriteBatch batch, IDictionary<string, int> values, string key, int? target)
        {
            batch.Add<int?>(key, () => values.TryGetValue(key, out int value) ? value : (int?)null,
                value => { if (value.HasValue) values[key] = value.Value; else values.Remove(key); }, target);
        }

        public static void Stat(StateWriteBatch batch, PlayerAvatar player, string key, string marker, int raw, int contribution)
        {
            Dictionary(batch, player.customStats, key, raw);
            Dictionary(batch, player.customStats, marker, contribution == 0 ? (int?)null : contribution);
        }

        public static void Fountain(StateWriteBatch batch, DungeonManager dungeon, IReadOnlyList<PlayerAvatar> players, FountainPlan plan)
        {
            Limit(batch, dungeon, plan);
            for (int i = 0; i < players.Count; i++)
            {
                PlayerAvatar player = players[i];
                GridInventory inventory = player.Inventory;
                batch.Add("Fountain capacity #" + player.netId, () => inventory.dimensionPocket,
                    value => inventory.NetworkdimensionPocket = value, plan.Points[i]);
                Dictionary(batch, player.customStats, FountainPoints.ContributionKey,
                    plan.Contributions[i] == 0 ? (int?)null : plan.Contributions[i]);
            }
        }

        public static void Limit(StateWriteBatch batch, DungeonManager dungeon, FountainPlan plan)
        {
            Dictionary(batch, dungeon.constValueDictionary, FountainPoints.LimitKey, plan.Limit);
            Dictionary(batch, dungeon.constValueDictionary, FountainPoints.OriginalLimitKey, plan.OriginalLimit);
            Dictionary(batch, dungeon.constValueDictionary, FountainPoints.AppliedLimitKey, plan.AppliedLimit);
        }
    }
}
