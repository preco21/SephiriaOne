using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static class ResourceBudgets
    {
        private const string FruitStat = "FRUITCOUNT";
        private const string FruitBase = "fruitSkewerDefaultCount";

        public static ResourceSnapshot Capture(PlayerAvatar player, ResourceKind kind)
        {
            if (!player) throw new InvalidOperationException("The resource owner is no longer available.");
            ResourceDefinition definition = ResourceCatalog.Get(kind);
            player.customStats.TryGetValue(definition.Marker, out int owned);
            if (kind == ResourceKind.Talents)
                return new ResourceSnapshot(definition, player.maxPassivePoint, owned, minimumSafe: TalentSpent(player));
            if (kind != ResourceKind.Fruit) throw new ArgumentOutOfRangeException(nameof(kind));
            PlayerLocalDataStorage storage = player.localDataStorage;
            if (!storage) throw new InvalidOperationException("The player's native fruit-skewer selections are not available yet.");
            player.customStats.TryGetValue(FruitStat, out int raw);
            player.calculatedBonusStats.TryGetValue(FruitStat, out int bonus);
            player.customStatsAmp.TryGetValue(FruitStat, out int amplifier);
            // The host's addon panel may itself raise these generic native UI flags.
            // Remote drafts are invisible until their owner saves, so defer decreases.
            bool busy = !player.isOwned && (storage.preparingUIThings || storage.doingSomeUIThings);
            int cost = checked(storage.fruitSkewerBonus.Count + (storage.adaptiveItemDropBonus != 0 ? 1 : 0));
            return new ResourceSnapshot(definition, raw, owned, bonus, amplifier,
                KeywordDatabase.GetConstValue(FruitBase), cost, busy);
        }

        public static void AddWrites(StateWriteBatch batch, PlayerAvatar player, ResourceUpdate update)
        {
            ResourceKind kind = update.Definition.Kind;
            ResourceSnapshot before = Capture(player, kind);
            int current = Total(before);
            var planned = new ResourceSnapshot(update.Definition, update.Raw, update.Owned,
                before.Bonus, before.Amplifier, before.DisplayOffset);
            if (Total(planned) != update.Target || update.Target < before.MinimumSafe || update.Target < 0 ||
                (before.Busy && update.Target < current))
                throw new InvalidOperationException("The resource budget cannot preserve native selections or a guest has an unfinished menu.");

            if (kind == ResourceKind.Talents)
            {
                var selections = new Dictionary<ulong, int>(player.passiveStats);
                Func<bool> unchanged = () => player && TalentSelectionsMatch(player, selections);
                batch.Require(unchanged);
                batch.RequireAfter(unchanged);
                batch.Add("Talent budget #" + player.netId, () => player.maxPassivePoint,
                    value =>
                    {
                        if (!unchanged() || value < TalentSpent(player))
                            throw new InvalidOperationException("Talent allocations changed before the native budget write.");
                        player.NetworkmaxPassivePoint = value;
                    }, update.Raw);
                NativeStateWrites.Dictionary(batch, player.customStats, update.Definition.Marker,
                    update.Owned == 0 ? (int?)null : update.Owned);
            }
            else
            {
                PlayerLocalDataStorage storage = player.localDataStorage;
                var selections = new List<GridInventory.ItemDropBonusData>(storage.fruitSkewerBonus);
                int adaptive = storage.adaptiveItemDropBonus;
                bool preparing = storage.preparingUIThings;
                bool doing = storage.doingSomeUIThings;
                bool owned = player.isOwned;
                Func<bool> unchanged = () => player && storage && ReferenceEquals(player.localDataStorage, storage) &&
                    player.isOwned == owned && storage.preparingUIThings == preparing && storage.doingSomeUIThings == doing &&
                    storage.adaptiveItemDropBonus == adaptive && FruitSelectionsMatch(storage, selections) &&
                    Value(player.calculatedBonusStats, FruitStat) == before.Bonus &&
                    Value(player.customStatsAmp, FruitStat) == before.Amplifier &&
                    KeywordDatabase.GetConstValue(FruitBase) == before.DisplayOffset;
                batch.Require(unchanged);
                batch.RequireAfter(unchanged);
                // Keep the same raw+owned journal semantics as NativeStateWrites.Stat,
                // with a write-time guard that also executes during explicit recovery.
                batch.Add<int?>(FruitStat, () => player.customStats.TryGetValue(FruitStat, out int raw) ? raw : (int?)null,
                    value =>
                    {
                        if (!unchanged()) throw new InvalidOperationException("Fruit-skewer inputs changed before the native budget write.");
                        ResourceSnapshot current = Capture(player, ResourceKind.Fruit);
                        int target = Total(new ResourceSnapshot(update.Definition, value.GetValueOrDefault(), update.Owned,
                            current.Bonus, current.Amplifier, current.DisplayOffset));
                        if (target < current.MinimumSafe || (current.Busy && target < Total(current)))
                            throw new InvalidOperationException("The fruit-skewer budget no longer preserves native selections.");
                        if (value.HasValue) player.customStats[FruitStat] = value.Value;
                        else player.customStats.Remove(FruitStat);
                    }, update.Raw);
                NativeStateWrites.Dictionary(batch, player.customStats, update.Definition.Marker,
                    update.Owned == 0 ? (int?)null : update.Owned);
            }
            batch.RequireAfter(() => player && Total(Capture(player, kind)) == update.Target);
        }

        internal static int RequiredTalentLoad(PlayerAvatar player, PlayerAvatar.PassiveStatSaveData[] data)
        {
            int total = TalentSpent(player);
            int peak = total;
            if (data == null) return peak;
            var selections = new Dictionary<ulong, int>(player.passiveStats);
            // Match native database order, repeated ids, and per-talent limits. The
            // peak matters: a later refund must not hide an earlier clamp.
            foreach (PassiveEntity passive in PassiveDatabase.GetAll())
            {
                if (!passive || passive.maxLevel < 0)
                    throw new InvalidOperationException("The native talent database has an invalid level limit.");
                foreach (PlayerAvatar.PassiveStatSaveData entry in data)
                {
                    if (entry.id != passive.id) continue;
                    selections.TryGetValue(entry.id, out int current);
                    int next = (int)Math.Max(0L, Math.Min(passive.maxLevel, (long)current + entry.point));
                    total = checked(total - current + next);
                    peak = Math.Max(peak, total);
                    selections[entry.id] = next;
                }
            }
            return peak;
        }

        internal static int Total(ResourceSnapshot value)
        {
            if (value.Definition.Kind != ResourceKind.Fruit) return value.Raw;
            long sum = (long)value.Raw + value.Bonus;
            long factor = 100L + value.Amplifier;
            if (sum < int.MinValue || sum > int.MaxValue || factor < int.MinValue || factor > int.MaxValue ||
                sum * factor < int.MinValue || sum * factor > int.MaxValue)
                throw new InvalidOperationException("The native resource budget arithmetic is outside its safe integer range.");
            return checked((int)((float)(sum * factor) / 100f) + value.DisplayOffset);
        }

        private static int TalentSpent(PlayerAvatar player)
        {
            int total = 0;
            foreach (int value in player.passiveStats.Values)
            {
                if (value < 0) throw new InvalidOperationException("A native talent allocation is negative.");
                total = checked(total + value);
            }
            return total;
        }

        private static bool TalentSelectionsMatch(PlayerAvatar player, Dictionary<ulong, int> expected)
        {
            if (player.passiveStats.Count != expected.Count) return false;
            foreach (var entry in expected)
                if (!player.passiveStats.TryGetValue(entry.Key, out int current) || current != entry.Value) return false;
            return true;
        }

        private static bool FruitSelectionsMatch(PlayerLocalDataStorage storage, List<GridInventory.ItemDropBonusData> expected)
        {
            if (storage.fruitSkewerBonus.Count != expected.Count) return false;
            for (int i = 0; i < expected.Count; i++)
                if (storage.fruitSkewerBonus[i].categoryName != expected[i].categoryName ||
                    storage.fruitSkewerBonus[i].weight != expected[i].weight) return false;
            return true;
        }

        private static int Value(IDictionary<string, int> values, string key) => values.TryGetValue(key, out int value) ? value : 0;
    }
}
