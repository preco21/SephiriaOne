using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // Starting grants, not inventory reconciliation: run only at native costume
    // equip/restock boundaries. Native code owns replication and companion life.
    internal static class CollinRuntime
    {
        internal const int ItemId = 1197;
        private const string Marker = "SephiriaOne_CollinStartingGift";
        private static readonly FieldInfo OwnedField = typeof(PlayerAvatar).GetField("costumeStartingItemInstanceIDs", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly HashSet<PlayerAvatar> busy = new HashSet<PlayerAvatar>();
        // Native restart clears global item metadata but keeps startingItems and
        // costume ownership. Retain only provenance, tied to the avatar object
        // and dungeon lifetime; never reuse a network/player ID on reconnect.
        private sealed class Receipt
        {
            internal readonly DungeonManager Dungeon = DungeonManager.Instance;
            internal readonly HashSet<int> Ids = new HashSet<int>();
        }
        private static readonly ConditionalWeakTable<PlayerAvatar, Receipt> receipts = new ConditionalWeakTable<PlayerAvatar, Receipt>();
        internal static bool ItemAvailable { get; private set; }
        internal static bool ValidateFields() => OwnedField?.FieldType == typeof(List<int>);
        internal static bool Eligible(string costume) => costume == "Mole" || costume == "Squirrel" || costume == "Turtle";
        internal static bool ValidateItem()
        {
            ItemAvailable = false;
            ItemEntity item = ItemDatabase.FindItemById(ItemId);
            bool valid = item && item.id == ItemId && item.aName?.key == "Item_WeaselKnight_Name";
            foreach (string id in new[] { "Mole", "Squirrel", "Turtle" })
            {
                CostumeEntity costume = CostumeDatabase.FindCostumeByID(id);
                valid &= costume && costume.id == id;
            }
            return ItemAvailable = valid;
        }

        private static List<int> Owned(PlayerAvatar player) => (List<int>)OwnedField.GetValue(player);
        private static Receipt GetReceipt(PlayerAvatar player)
        {
            if (receipts.TryGetValue(player, out Receipt value) && !ReferenceEquals(value.Dungeon, DungeonManager.Instance))
                receipts.Remove(player);
            return receipts.GetValue(player, _ => new Receipt());
        }
        private static bool IsGift(PlayerAvatar player, int id)
        {
            Receipt receipt = GetReceipt(player);
            if (receipt.Ids.Contains(id)) return true;
            if (DungeonManager.Instance.GetGlobalItemStatValue(id, Marker) != "1") return false;
            receipt.Ids.Add(id); return true;
        }
        private static void Mark(PlayerAvatar player, int id)
        {
            GetReceipt(player).Ids.Add(id);
            DungeonManager.Instance.globalItemStatTable[DungeonManager.Instance.BuildGlobalItemStatKey(id, Marker)] = "1";
        }
        private static void Unmark(PlayerAvatar player, int id)
        {
            GetReceipt(player).Ids.Remove(id);
            DungeonManager.Instance.globalItemStatTable.Remove(DungeonManager.Instance.BuildGlobalItemStatKey(id, Marker));
        }
        private static bool IsHost(PlayerAvatar player) => NetworkServer.active && player && player.isServer && player.Inventory && player.spawner && DungeonManager.Instance;

        internal static void BeforeCostume(PlayerAvatar player)
        {
            if (!IsHost(player) || !busy.Add(player)) return;
            try
            {
                // Native cleanup handles the owner's inventory, pending items,
                // sub-bag and ground drops. Extend exact-ID cleanup to gifts that
                // were transferred using the optional given-item unlock.
                foreach (int id in Owned(player))
                    if (IsGift(player, id)) { RemoveTransferred(player, id); Unmark(player, id); }
            }
            catch (Exception error) { Warn(error); }
            finally { busy.Remove(player); }
        }

        internal static void AfterCostume(PlayerAvatar player, string costume)
        {
            if (!IsHost(player) || !busy.Add(player)) return;
            try
            {
                if (CollinFeature.Available && SessionSettings.CollinForUse && Eligible(costume) &&
                    !HorayNetworkAuthenticator.AccessDeny_InDungeon) AddIfMissing(player, false);
            }
            catch (Exception error) { Warn(error); }
            finally { busy.Remove(player); }
        }

        internal static void BeforeRestock(GridInventory inventory)
        {
            var player = inventory.UnitAvatar as PlayerAvatar;
            if (!IsHost(player) || !ReferenceEquals(player.Inventory, inventory) || !busy.Add(player)) return;
            try
            {
                bool wanted = CollinFeature.Available && SessionSettings.CollinForUse && Eligible(player.currentCostume);
                var owned = Owned(player);
                if (!wanted)
                {
                    for (int i = owned.Count - 1; i >= 0; i--)
                    {
                        int id = owned[i];
                        if (!IsGift(player, id)) continue;
                        RemoveTransferred(player, id);
                        using (new GridInventory.Permission(inventory)) inventory.RemoveStartingItem(id);
                        owned.RemoveAt(i); Unmark(player, id);
                    }
                }
                else AddIfMissing(player, true);
            }
            catch (Exception error) { Warn(error); }
            finally { busy.Remove(player); }
        }

        private static void AddIfMissing(PlayerAvatar player, bool restock)
        {
            var inventory = player.Inventory;
            var owned = Owned(player);
            foreach (ItemMetadata item in inventory.startingItems)
                if (item.entityID == ItemId && owned.Contains(item.instanceID))
                {
                    // Re-publish provenance erased by the native restart clear.
                    if (IsGift(player, item.instanceID)) Mark(player, item.instanceID);
                    return;
                }
            // Recheck the identity on this infrequent boundary, including after
            // database replacement. Never manufacture an unknown game item.
            if (!ValidateItem()) return;
            if (restock)
            {
                // AddStartingItem would grant immediately in the lobby, then the
                // original RestockStartingItem would grant it a second time.
                // Register metadata only; the original method grants exactly once.
                var item = new ItemMetadata(ItemDatabase.GenerateInstanceID(new System.Random()), ItemId, 1);
                owned.Add(item.instanceID); Mark(player, item.instanceID);
                inventory.startingItems.Add(item);
                return;
            }

            var before = new HashSet<int>();
            foreach (ItemMetadata item in inventory.startingItems) before.Add(item.instanceID);
            try { inventory.AddStartingItem(new ItemMetadata(-1, ItemId, 1), player.spawner.currentPlayerIdx); }
            finally
            {
                // Native AddStartingItem registers before attempting placement.
                // Preserve removal ownership even if a placement callback throws.
                foreach (ItemMetadata item in inventory.startingItems)
                    if (item.entityID == ItemId && !before.Contains(item.instanceID))
                    {
                        if (!owned.Contains(item.instanceID)) owned.Add(item.instanceID);
                        Mark(player, item.instanceID);
                    }
            }
        }

        private static void RemoveTransferred(PlayerAvatar owner, int id)
        {
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = spawner ? spawner.PlayerAvatar : null;
                if (!player || !player.isServer || ReferenceEquals(player, owner) || !player.Inventory) continue;
                using (new GridInventory.Permission(player.Inventory)) player.Inventory.RemoveStartingItem(id);
            }
        }
        private static void Warn(Exception error) => Debug.LogWarning("[SephiriaOne] Collin starting-artifact boundary failed; native behavior continues: " + error);
    }
}
