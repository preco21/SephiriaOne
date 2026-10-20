using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class RequitalRuntime
    {
        private static bool Registered(GridInventory inventory, ItemMetadata owned)
        {
            foreach (var item in inventory.startingItems)
                if (item.instanceID == owned.instanceID && item.entityID == owned.entityID && item.quantity == 1) return true;
            return false;
        }
        private static bool Owns(Ownership state, int id, int entity = 0)
        {
            foreach (var item in state.Items)
                if (item.instanceID == id && (entity == 0 || item.entityID == entity)) return true;
            return false;
        }
        private static SlateKind SavedKind(DungeonManager dungeon, int id)
        {
            string value = dungeon.GetGlobalItemStatValue(id, KindMarker);
            // Pre-selection versions wrote only the owner marker: those exact
            // IDs are Requital. Never infer type from a recipient's loadout.
            if (string.IsNullOrEmpty(value)) return SlateKind.Requital;
            if (value == "heaven") return SlateKind.Heaven;
            throw new InvalidOperationException("Unknown saved automatic Slate type.");
        }
        private static void Recover(Ownership state)
        {
            // BuildGlobalItemStatKey's exact native ID/purpose format is checked
            // by the adapter hook contract. Never infer provenance from type.
            string suffix = "/" + Marker;
            foreach (var entry in state.Dungeon.globalItemStatTable)
            {
                if (entry.Value != state.Guid || !entry.Key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                string prefix = entry.Key.Substring(0, entry.Key.Length - suffix.Length);
                if (int.TryParse(prefix, out int id) && entry.Key == state.Dungeon.BuildGlobalItemStatKey(id, Marker) &&
                    !IsRetired(state.Dungeon, id) &&
                    !Owns(state, id)) state.Items.Add(new ItemMetadata(id, ItemIdFor(SavedKind(state.Dungeon, id)), 1));
            }
        }
        private static bool IsRetired(DungeonManager dungeon, int id) =>
            !string.IsNullOrEmpty(dungeon.GetGlobalItemStatValue(id, RetiredMarker));

        private static void Retire(DungeonManager dungeon, int id, string owner)
        {
            // Persist the tombstone before deleting live provenance. A transferred
            // saved copy may be offline; its next enrollment must still retract it.
            dungeon.globalItemStatTable[dungeon.BuildGlobalItemStatKey(id, RetiredMarker)] = owner;
            dungeon.globalItemStatTable.Remove(dungeon.BuildGlobalItemStatKey(id, Marker));
            dungeon.globalItemStatTable.Remove(dungeon.BuildGlobalItemStatKey(id, KindMarker));
        }

        private static List<(int Id, string Owner)> Marked(DungeonManager dungeon, string marker)
        {
            var result = new List<(int, string)>();
            string suffix = "/" + marker;
            foreach (var entry in dungeon.globalItemStatTable)
                if (!string.IsNullOrWhiteSpace(entry.Value) && entry.Key.EndsWith(suffix, StringComparison.Ordinal) &&
                    int.TryParse(entry.Key.Substring(0, entry.Key.Length - suffix.Length), out int id) &&
                    entry.Key == dungeon.BuildGlobalItemStatKey(id, marker)) result.Add((id, entry.Value));
            return result;
        }

        private static void RetireExcess(RequitalSettings settings, Func<bool> currentOwner)
        {
            var dungeon = DungeonManager.Instance;
            if (retirementObserved && ReferenceEquals(retirementDungeon, dungeon) && retirementSettings.Equals(settings)) return;
            // This boundary covers absent owners too. Connected recipients each
            // prune tombstones on their settings/new-receipt observation below.
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int desired = settings.Automatic ? settings.Count : 0;
            foreach (var entry in Marked(dungeon, Marker))
            {
                Require(currentOwner());
                counts.TryGetValue(entry.Owner, out int count);
                if (count >= desired || SavedKind(dungeon, entry.Id) != settings.Kind) Retire(dungeon, entry.Id, entry.Owner);
                else counts[entry.Owner] = count + 1;
            }
            Require(currentOwner());
            retirementObserved = true; retirementDungeon = dungeon; retirementSettings = settings;
        }

        private static void RemoveRetired(GridInventory inventory, Func<bool> currentOwner)
        {
            foreach (var entry in Marked(DungeonManager.Instance, RetiredMarker))
            {
                Require(currentOwner());
                using ((bool)permission.GetValue(inventory) ? null : new GridInventory.Permission(inventory))
                    inventory.RemoveStartingItem(entry.Id);
                Require(currentOwner());
            }
        }
        private static void Mark(Ownership state, ItemMetadata item)
        {
            int id = item.instanceID;
            if (item.entityID == HeavenItemId) state.Dungeon.globalItemStatTable[state.Dungeon.BuildGlobalItemStatKey(id, KindMarker)] = "heaven";
            state.Dungeon.globalItemStatTable[state.Dungeon.BuildGlobalItemStatKey(id, Marker)] = state.Guid;
        }
        private static void Require(bool valid) { if (!valid) throw new InvalidOperationException("Requital native owner/settings changed during write."); }

        private static void DeliverStarting(PlayerAvatar player, GridInventory inventory, ItemMetadata item, bool notification,
            Func<bool> currentOwner, bool restock = false)
        {
            using ((bool)permission.GetValue(inventory) ? null : new GridInventory.Permission(inventory))
            {
                Require(currentOwner() && (bool)permission.GetValue(inventory));
                if (restock)
                {
                    // ForceRemoveAll preserves this queue; Update may also drain
                    // it during the native restart coroutine's waits. Remove only
                    // this proven ID's stale entries, then avoid a second delivery.
                    var retries = (List<GridInventory.StartingItemAdditionFailedInfo>)pending.GetValue(inventory);
                    for (int i = retries.Count - 1; i >= 0; i--)
                        if (retries[i].instanceID == item.instanceID) retries.RemoveAt(i);
                    if (PresentAnywhere(item.instanceID)) return;
                }
                if (inventory.LocalAddItem(item.instanceID, item.entityID, 1, 0, notification, isReward: false))
                {
                    Require(currentOwner());
                    DungeonManager.Instance.BoundOnServer(item.instanceID, player.spawner.currentPlayerIdx);
                    DungeonManager.Instance.OwnRestrictionOnServer(item.instanceID, ERestrictedOwnType.StartingItem);
                }
                else
                {
                    Require(currentOwner() && (bool)permission.GetValue(inventory));
                    ((List<GridInventory.StartingItemAdditionFailedInfo>)pending.GetValue(inventory)).Add(
                        new GridInventory.StartingItemAdditionFailedInfo {
                            instanceID = item.instanceID, entityID = item.entityID, quantity = 1, playerIdx = player.spawner.currentPlayerIdx });
                }
            }
        }

        private static bool PresentAnywhere(int id)
        {
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                var player = spawner ? spawner.PlayerAvatar : null;
                if (!player || !player.isServer || !player.Inventory) continue;
                var inventory = player.Inventory;
                if (inventory.FindItemByInstanceID(id) != null || inventory.FindEngravingByInstanceID(id) != null ||
                    inventory.TryGetSubBagItemByInstanceID(id, out _, out _)) return true;
                foreach (var item in inventory.temporaryInventory) if (item.instanceID == id) return true;
                foreach (var item in inventory.fixedEngravingsOnServer) if (item.instanceID == id) return true;
            }
            return false;
        }

        // Replace only the AddItem call in native restock. Native AddItem throws
        // away LocalAddItem's false result; our proven gifts must retain overflow
        // in the same pending queue used by native AddStartingItem.
        internal static void RestockItem(GridInventory inventory, ItemMetadata item, int rotation, bool notification, bool isReward)
        {
            var player = inventory ? inventory.UnitAvatar as PlayerAvatar : null;
            if ((item.entityID != ItemId && item.entityID != HeavenItemId) || !IsHost(player) || !ReferenceEquals(player.Inventory, inventory))
            { inventory.AddItem(item, rotation, notification, isReward); return; }
            // Ordinary native items retain native error/overflow semantics too.
            if (!ownership.TryGetValue(inventory, out var state) || !ReferenceEquals(state.Dungeon, DungeonManager.Instance) ||
                state.Guid != player.spawner.playerGuid || !Owns(state, item.instanceID, item.entityID))
            { inventory.AddItem(item, rotation, notification, isReward); return; }
            if (state.Faulted) return;
            try
            {
                var settings = SessionSettings.Requital;
                if (writing || !AvailableFor(settings.Kind) || !ValidItem(settings.Kind) || item.entityID != ItemIdFor(settings.Kind))
                    throw new InvalidOperationException("Slate restock adapter unavailable, changed or reentrant.");
                var run = SaveManager.CurrentRun;
                long observedScope = scope, generation = SessionSettings.ResourceGeneration;
                var receipt = FriendlyFireParticipants.Capture(player);
                bool Current() => IsHost(player) && scope == observedScope && generation == SessionSettings.ResourceGeneration &&
                    ReferenceEquals(state.Dungeon, DungeonManager.Instance) && ReferenceEquals(run, SaveManager.CurrentRun) &&
                    ReferenceEquals(inventory, player.Inventory) && player.spawner.playerGuid == state.Guid &&
                    SessionSettings.Requital.Equals(settings) && (receipt == null || FriendlyFireParticipants.IsCurrent(receipt));
                writing = true;
                try { DeliverStarting(player, inventory, item, notification, Current, restock: true); Require(Current()); }
                finally { writing = false; }
            }
            catch (Exception error)
            {
                if (state != null) state.Faulted = true;
                Warn("native restock delivery failed", error);
            }
        }

        private static void RemoveEverywhere(PlayerAvatar owner, int id, Func<bool> currentOwner)
        {
            var inventories = new List<(PlayerAvatar Player, GridInventory Inventory)> { (owner, owner.Inventory) };
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                var player = spawner ? spawner.PlayerAvatar : null;
                if (player && player.isServer && player.Inventory && !ReferenceEquals(player, owner))
                    inventories.Add((player, player.Inventory));
            }
            foreach (var target in inventories)
            {
                Require(currentOwner() && IsHost(target.Player) && ReferenceEquals(target.Inventory, target.Player.Inventory));
                var inventory = target.Inventory;
                using ((bool)permission.GetValue(inventory) ? null : new GridInventory.Permission(inventory))
                    inventory.RemoveStartingItem(id);
                Require(currentOwner());
            }
        }

    }
}
