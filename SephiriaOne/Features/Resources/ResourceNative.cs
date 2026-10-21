using System;

namespace SephiriaOne
{
    internal static class ResourceNative
    {
        public static ResourceSnapshot Capture(PlayerAvatar player, ResourceKind kind)
        {
            if (kind == ResourceKind.Slots) return InventoryResources.Capture(player);
            if (kind == ResourceKind.Talents || kind == ResourceKind.Fruit) return ResourceBudgets.Capture(player, kind);
            if (kind == ResourceKind.Dice)
            {
                int native = StartingResourceHooks.NativeDice(player);
                return new ResourceSnapshot(ResourceCatalog.Get(kind), player.maxRerollDice, checked(player.maxRerollDice - native));
            }
            return new ResourceSnapshot(ResourceCatalog.Get(kind), StartingResourceHooks.NativeLeaves(player), 0);
        }

        public static void AddWrites(StateWriteBatch batch, PlayerAvatar player, ResourceUpdate update)
        {
            if (update.Definition.Kind == ResourceKind.Slots) InventoryResources.AddWrites(batch, player, update);
            else if (update.Definition.Kind == ResourceKind.Dice) StartingResourceHooks.AppendLobbyDice(batch, player, update.Target, update.Owned);
            else if (!update.Definition.StartingOnly) ResourceBudgets.AddWrites(batch, player, update);
            else throw new InvalidOperationException("Starting resources are applied only at native grant boundaries.");
        }
    }
}
