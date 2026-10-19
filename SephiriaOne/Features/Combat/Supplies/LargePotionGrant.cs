using System;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal enum PotionGrantResult { Granted, Owned, Waiting, Failed }

    // Native item/inventory replication works for clients without the addon.
    // This is a one-shot supply action, not a retained inventory quantity policy.
    internal static class LargePotionGrant
    {
        internal const int ItemId = 1;
        private static readonly System.Random random = new System.Random();
        private static readonly FieldInfo permission = typeof(GridInventory).GetField("writePermission", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool granting;
        internal static bool Available { get; private set; }

        internal static void Load()
        {
            Available = permission?.FieldType == typeof(bool) && ValidItem();
            if (!Available) Debug.LogWarning("[SephiriaOne] Large Potion identity/inventory contract unavailable.");
        }
        internal static void Clear() { Available = false; PvpPotionSupplies.Clear(); }
        private static bool ValidItem()
        {
            var item = ItemDatabase.FindItemById(ItemId);
            return item && item.id == ItemId && item.type == EItemType.Potion && item.activeType != EItemActiveType.Disabled &&
                item.aName?.key == "Item_BigPotionOfRegeneration_Name";
        }

        internal static bool CanExecute => Available && SessionSettings.EnsureResourceScope() &&
            ReviveAllAction.IsRunOpen(DungeonManager.Instance, SaveManager.CurrentRun);

        internal static bool Owns(GridInventory inventory)
        {
            foreach (var entry in inventory.inventoryMatrix)
                if (entry.Value.EntityID == ItemId && entry.Value.Quantity > 0) return true;
            foreach (var entry in inventory.subBagMatrix)
                if (entry.Value.entityID == ItemId && entry.Value.quantity > 0) return true;
            foreach (var item in inventory.temporaryInventory)
                if (item.entityID == ItemId && item.quantity > 0) return true;
            return false;
        }

        internal static PotionGrantResult Give(FriendlyFireParticipants.Receipt receipt, Func<bool> admitted = null)
        {
            if (granting || !CanExecute || !FriendlyFireParticipants.IsCurrent(receipt) || !HostStateAdapter.IsReady(receipt.Spawner))
                return PotionGrantResult.Waiting;
            var inventory = receipt.Player.Inventory;
            var dungeon = DungeonManager.Instance;
            var run = SaveManager.CurrentRun;
            long generation = SessionSettings.ResourceGeneration;
            bool Current() => NetworkServer.active && ReferenceEquals(dungeon, DungeonManager.Instance) &&
                ReferenceEquals(run, SaveManager.CurrentRun) && generation == SessionSettings.ResourceGeneration &&
                ReviveAllAction.IsRunOpen(dungeon, run) && FriendlyFireParticipants.IsCurrent(receipt) &&
                HostStateAdapter.IsReady(receipt.Spawner) && ReferenceEquals(inventory, receipt.Player.Inventory) &&
                (admitted == null || admitted());
            granting = true;
            try
            {
                if (!Current()) return PotionGrantResult.Waiting;
                if (Owns(inventory)) return PotionGrantResult.Owned;
                if (!ValidItem()) throw new InvalidOperationException("Large Potion database entry changed.");
                // Permission is not reentrant. Borrow an existing transaction.
                using ((bool)permission.GetValue(inventory) ? null : new GridInventory.Permission(inventory))
                {
                    if (!Current() || !(bool)permission.GetValue(inventory)) return PotionGrantResult.Waiting;
                    if (Owns(inventory)) return PotionGrantResult.Owned;
                    int instance = ItemDatabase.GenerateInstanceID(random);
                    if (inventory.LocalAddItem(instance, ItemId, 1, 0, notification: true, isReward: false))
                        return PotionGrantResult.Granted;
                    if (!Current() || !(bool)permission.GetValue(inventory) || !ValidItem()) return PotionGrantResult.Failed;
                    if (Owns(inventory)) return PotionGrantResult.Owned;
                    // A definite full-inventory result uses the native overflow UI.
                    inventory.temporaryInventory.Add(new ItemMetadata(instance, ItemId, 1));
                    return PotionGrantResult.Granted;
                }
            }
            catch (Exception error)
            {
                // A callback could have committed before throwing. Never retry
                // automatically or add an overflow copy after an exception.
                Debug.LogWarning("[SephiriaOne] Large Potion grant failed for " + receipt.NetId + ": " + error);
                return PotionGrantResult.Failed;
            }
            finally { granting = false; }
        }

        internal static bool GiveAll(out string message)
        {
            message = L.T("Only the host can give Large Potions.");
            if (!NetworkServer.active) return false;
            message = L.T("Large Potion requires a ready hosted session; see Player.log if unavailable.");
            if (!CanExecute) return false;
            var targets = new HashSet<FriendlyFireParticipants.Receipt>();
            int skipped = 0, granted = 0, owned = 0, failed = 0;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                var receipt = spawner ? FriendlyFireParticipants.Capture(spawner.PlayerAvatar) : null;
                if (receipt == null) skipped++; else targets.Add(receipt);
            }
            // Snapshot before native callbacks can mutate the connection roster.
            foreach (var receipt in targets)
            {
                switch (Give(receipt))
                {
                    case PotionGrantResult.Granted: granted++; break;
                    case PotionGrantResult.Owned: owned++; break;
                    case PotionGrantResult.Waiting: skipped++; break;
                    default: failed++; break;
                }
            }
            message = L.F("Large Potion: granted {0}; already owned {1}; not ready {2}; failed {3}.", granted, owned, skipped, failed);
            return failed == 0 && skipped == 0;
        }
    }
}
