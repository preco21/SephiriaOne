using System;

namespace SephiriaOne
{
    internal static class ResourceNative
    {
        public static ResourceSnapshot Capture(PlayerAvatar player, ResourceKind kind)
        {
            if (kind == ResourceKind.Slots) return InventoryResources.Capture(player);
            if (kind == ResourceKind.Talents || kind == ResourceKind.Fruit) return ResourceBudgets.Capture(player, kind);
            return new ResourceSnapshot(ResourceCatalog.Get(kind), kind == ResourceKind.Dice ?
                StartingResourceHooks.NativeDice(player) : StartingResourceHooks.NativeLeaves(player), 0);
        }

        public static void AddWrites(StateWriteBatch batch, PlayerAvatar player, ResourceUpdate update)
        {
            if (update.Definition.Kind == ResourceKind.Slots) InventoryResources.AddWrites(batch, player, update);
            else if (!update.Definition.StartingOnly) ResourceBudgets.AddWrites(batch, player, update);
            else throw new InvalidOperationException("Starting resources are applied only at native grant boundaries.");
        }
    }
}
