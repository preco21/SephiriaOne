using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static class InventoryResources
    {
        public static ResourceSnapshot Capture(PlayerAvatar player)
        {
            if (!player || !player.Inventory) throw new InvalidOperationException("The native inventory is not available.");
            GridInventory inventory = player.Inventory;
            ValidateCapacity(inventory, inventory.CurrentInventoryStorage);
            ResourceDefinition definition = ResourceCatalog.Get(ResourceKind.Slots);
            player.customStats.TryGetValue(definition.Marker, out int owned);
            int minimum = definition.Minimum;
            foreach (var item in inventory.inventoryMatrix)
                minimum = Math.Max(minimum, RequiredPosition(inventory, item.Key));
            foreach (ItemPosition position in inventory.mysticPositions)
                minimum = Math.Max(minimum, RequiredPosition(inventory, position));
            return new ResourceSnapshot(definition, inventory.CurrentInventoryStorage, owned,
                minimumSafe: minimum, busy: Busy(player, inventory));
        }

        public static void AddWrites(StateWriteBatch batch, PlayerAvatar player, ResourceUpdate update)
        {
            if (update.Definition.Kind != ResourceKind.Slots) throw new ArgumentOutOfRangeException(nameof(update));
            ResourceSnapshot before = Capture(player);
            GridInventory inventory = player.Inventory;
            ValidateCapacity(inventory, update.Target);
            if (update.Raw != update.Target || update.Target < before.MinimumSafe ||
                (before.Busy && update.Target < before.Raw))
                throw new InvalidOperationException("Inventory reduction would affect occupied slots, engravings, or a pending native item interaction.");
            var items = new Dictionary<ItemPosition, NewItemOwnInstance>(inventory.inventoryMatrix);
            var mystics = new List<ItemPosition>(inventory.mysticPositions);
            int engravings = inventory.engravings.Count;
            int fixedEngravings = inventory.fixedEngravingsOnServer.Count;
            byte width = inventory.Width;
            Func<bool> unchanged = () => player && inventory && inventory.isServer && ReferenceEquals(player.Inventory, inventory) &&
                inventory.Width == width && inventory.engravings.Count == engravings && inventory.fixedEngravingsOnServer.Count == fixedEngravings &&
                Busy(player, inventory) == before.Busy && Matches(inventory, items, mystics);
            batch.Require(unchanged);
            batch.RequireAfter(unchanged);
            batch.Add("Inventory slots #" + player.netId, () => (int)inventory.CurrentInventoryStorage,
                value =>
                {
                    // Journal recovery deliberately skips planning preconditions.
                    // Recheck immediately before native negative AddStorage can move/drop items.
                    if (!unchanged()) throw new InvalidOperationException("Inventory inputs changed before the native resize.");
                    ResourceSnapshot current = Capture(player);
                    if (value < current.MinimumSafe || (current.Busy && value < current.Raw))
                        throw new InvalidOperationException("Inventory resize no longer preserves native items or pending interactions.");
                    inventory.AddStorage(checked((short)(value - inventory.CurrentInventoryStorage)));
                }, update.Raw);
            NativeStateWrites.Dictionary(batch, player.customStats, update.Definition.Marker,
                update.Owned == 0 ? (int?)null : update.Owned);
        }

        internal static void ValidateCapacity(GridInventory inventory, int capacity)
        {
            // Native coordinates are signed bytes; row 100 is the potion belt.
            // Command limits are stricter, but restoring legitimate native capacity
            // must not silently truncate it to the addon's configurable maximum.
            if (!inventory || inventory.Width == 0 || inventory.Width > sbyte.MaxValue ||
                capacity < ResourceCatalog.Get(ResourceKind.Slots).Minimum || capacity > short.MaxValue ||
                capacity > inventory.Width * 100)
                throw new InvalidOperationException("Inventory capacity is outside the native main-grid coordinate range.");
        }

        private static int RequiredPosition(GridInventory inventory, ItemPosition position)
        {
            if (position.y < 0 || position.y >= 100) return 0;
            if (position.x < 0 || position.x >= inventory.Width)
                throw new InvalidOperationException("The native inventory contains an invalid grid position.");
            return position.y * inventory.Width + position.x + 1;
        }

        private static bool Busy(PlayerAvatar player, GridInventory inventory)
        {
            if (inventory.engravings.Count != 0 || inventory.fixedEngravingsOnServer.Count != 0) return true;
            if (!player.localDataStorage) return true;
            if (!player.isOwned) return player.localDataStorage.preparingUIThings || player.localDataStorage.doingSomeUIThings;
            if (!UIManager.Instance) return false;
            var mouse = UIManager.Instance.GetElement<UI_NewItemPicker>();
            var controller = UIManager.Instance.GetElement<UI_NewItemPicker_Controller>();
            return (mouse && mouse.CurrentAny) || (controller && controller.CurrentAny);
        }

        private static bool Matches(GridInventory inventory, Dictionary<ItemPosition, NewItemOwnInstance> items, List<ItemPosition> mystics)
        {
            if (inventory.inventoryMatrix.Count != items.Count || inventory.mysticPositions.Count != mystics.Count) return false;
            foreach (var item in items)
                if (!inventory.inventoryMatrix.TryGetValue(item.Key, out NewItemOwnInstance current) || !ReferenceEquals(current, item.Value)) return false;
            for (int i = 0; i < mystics.Count; i++)
                if (inventory.mysticPositions[i].x != mystics[i].x || inventory.mysticPositions[i].y != mystics[i].y) return false;
            return true;
        }
    }
}
