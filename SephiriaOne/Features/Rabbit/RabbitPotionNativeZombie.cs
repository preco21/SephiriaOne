using Mirror;

namespace SephiriaOne
{
    internal static partial class RabbitPotionNativeHooks
    {
        // Sample remains excluded from Rabbit/PvP infinite, MP and talent overrides.
        // This separate use restriction covers all three native HP potions.
        private static bool IsRestrictedZombieHpPotion(int id) => id == 0 || id == 1 || id == 37;

        private static bool AllowZombiePotionUse(ItemController __instance)
        {
            if (!Available || !NetworkServer.active) return true;
            try
            {
                var player = __instance?.Avatar as PlayerAvatar;
                if (!player || !CombatMatchRuntime.BlocksZombieHpPotions(player) ||
                    !PlayerReady(__instance, player, player.spawner)) return true;
                int selected = __instance.SelectedQuickSlotIdx;
                if (selected < 0 || selected >= __instance.quickSlotTable.Count) return true;
                var item = player.Inventory.FindItem(player.Inventory.IdxToPos(__instance.quickSlotTable[selected].idx));
                if (item == null || item.Quantity <= 0 || !IsRestrictedZombieHpPotion(item.EntityID) ||
                    !item.Entity || item.Entity.type != EItemType.Potion) return true;
                NotifyZombiePotionBlocked(player);
                return false;
            }
            catch { return true; } // Invalid native inventory/ownership retains its own admission checks.
        }

        private static void RejectZombiePotionCompletion(WieldingPotion potion)
        {
            if (!Available || !NetworkServer.active || !potion || !potion.isServer || potion.netId == 0 ||
                !IsRestrictedZombieHpPotion(potion.entityID)) return;
            var controller = potion.NetworkController;
            var player = controller?.Avatar as PlayerAvatar;
            if (!player || !CombatMatchRuntime.BlocksZombieHpPotions(player) ||
                !SourceReady(potion, controller, player, player.spawner, allowDead: true)) return;
            // Re-evaluate live faction/settings: a Human can convert during the animation.
            // The existing native catch still unwields the item, without running any drink events.
            NotifyZombiePotionBlocked(player);
            throw new RejectedDrink();
        }

        private static void NotifyZombiePotionBlocked(PlayerAvatar player) => NativePlayerAlert.Show(player,
            player.spawner.connectionToClient, L.T("Zombies cannot use HP potions."));
    }
}
