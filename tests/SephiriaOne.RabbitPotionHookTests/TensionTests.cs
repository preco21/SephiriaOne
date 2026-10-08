using Mirror;
using SephiriaOne;

internal static class TensionTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<int, bool, (PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup,
        Action install)
    {
        void Boss()
        {
            DungeonManager.Instance.hardModeEnvironment["HOSTILITY"] = 1;
            FloorGenerator.All["floor"] = new FloorGenerator { BossBattle = true };
        }
        foreach (bool local in new[] { true, false })
        foreach (int id in new[] { 0, 1 })
            check($"Tension admits infinite Rabbit HP potion {id}, local {local}", () =>
            {
                var (player, controller, _, item) = setup(id, local); Boss();
                controller.currentWieldingItem = null; // Tension runs before a wielded potion exists.
                var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(player);
                SessionSettings.RabbitPotionsForUse = new(true, false, true, true, 10);
                install();
                if (local) controller.UseItemKeyDown(); else controller.GuestUseItemKeyDown();
                assert(controller.StartedDrinks == 1 && controller.BlockedNotices == 0, "Rabbit HP potion was blocked during a boss fight");
                controller.RunDrink();
                assert(player.Hp == 40 && item.Quantity == 3 && player.mp == 20 && survival.StatGains == 0,
                    "Tension exception must preserve HP healing, infinite use, MP fee and Survival suppression");
                assert(DungeonManager.Instance.hardModeEnvironment["HOSTILITY"] == 1, "Tension was modified globally");
            });
        foreach (string condition in new[] { "off", "share-only", "sample", "mp", "buff", "other-costume", "dead", "unready", "disconnected", "empty", "wrong-effect", "can-drink", "attacking" })
            check("Tension remains effective: " + condition, () =>
            {
                var (player, controller, _, item) = setup(0, false); Boss();
                SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
                var effect = item.Entity.resourcePrefab.GetComponent<PotionEffect>();
                switch (condition)
                {
                    case "off": SessionSettings.RabbitPotionsForUse = default; break;
                    case "share-only": SessionSettings.RabbitPotionsForUse = new(false, true, true, true); break;
                    case "sample": item.EntityID = 37; break;
                    case "mp": item.EntityID = 2; break;
                    case "buff": item.EntityID = 99; break;
                    case "other-costume": player.currentCostume = "PinkRabbit"; break;
                    case "dead": player.IsDead = true; break;
                    case "unready": player.spawner.connectionToClient.isReady = false; break;
                    case "disconnected": NetworkServer.connections.Clear(); break;
                    case "empty": item.Quantity = 0; break;
                    case "wrong-effect": item.Entity.resourcePrefab.Component = new PotionEffect_Concentration(); break;
                    case "can-drink": effect.DrinkAllowed = false; break;
                    case "attacking": controller.IsAttacking = true; break;
                }
                install(); controller.GuestUseItemKeyDown();
                assert(controller.StartedDrinks == 0 && player.Heals.Count == 0 && player.mp == 30,
                    "An unrelated potion/player/use restriction was bypassed");
            });
        check("Tension reevaluates current costume and settings for every attempt", () =>
        {
            var (player, controller, _, _) = setup(0, false); Boss(); install();
            controller.GuestUseItemKeyDown(); assert(controller.StartedDrinks == 0, "Off blocked incorrectly");
            SessionSettings.RabbitPotionsForUse = new(true, false);
            controller.GuestUseItemKeyDown(); assert(controller.StartedDrinks == 1, "Enabling did not immediately permit HP use");
            player.currentCostume = "PinkRabbit"; controller.GuestUseItemKeyDown();
            SessionSettings.RabbitPotionsForUse = default; player.currentCostume = "HolyRabbit"; controller.GuestUseItemKeyDown();
            assert(controller.StartedDrinks == 1, "Costume change or reset retained a stale exemption");
            RabbitPotionFeature.Shutdown(); SessionSettings.RabbitPotionsForUse = new(true, false); controller.GuestUseItemKeyDown();
            assert(controller.StartedDrinks == 1, "Unloading left a native Tension patch installed");
        });
        check("Tension exception keeps insufficient-MP rejection at completion", () =>
        {
            var (player, controller, _, item) = setup(0, false); Boss(); player.mp = 5;
            SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 10);
            install(); controller.GuestUseItemKeyDown(); controller.RunDrink();
            assert(controller.StartedDrinks == 1 && player.Heals.Count == 0 && item.Quantity == 3 && player.mp == 5,
                "Boss admission bypassed the completion-time MP guard");
        });
        check("Tension reevaluates a rejoining player's new connection", () =>
        {
            var old = setup(0, false); Boss(); SessionSettings.RabbitPotionsForUse = new(true, false); install();
            old.controller.GuestUseItemKeyDown(); assert(old.controller.StartedDrinks == 1, "Initial connection was blocked");
            NetworkServer.connections.Clear(); PlayerSpawner.MultiplayerList.Remove(old.player.spawner);
            var rejoined = setup(0, false);
            old.controller.GuestUseItemKeyDown(); rejoined.controller.GuestUseItemKeyDown();
            assert(old.controller.StartedDrinks == 1 && rejoined.controller.StartedDrinks == 1,
                "Rejoin reused stale permission or failed to apply current policy to the new connection");
            SessionSettings.RabbitPotionsForUse = default;
            rejoined.controller.GuestUseItemKeyDown();
            assert(rejoined.controller.StartedDrinks == 1, "Reset did not revoke the new connection's exemption");
        });
        check("Tension uses the exact native item despite a CanDrink callback changing selection", () =>
        {
            var (player, controller, _, item) = setup(2, false); Boss();
            player.Inventory.items[new(1, 0)] = new NewItemOwnInstance { EntityID = 0, InstanceID = 53, Quantity = 3 };
            controller.quickSlotTable.Add(new ItemController.QuickSlot { idx = 1 });
            item.Entity.resourcePrefab.GetComponent<PotionEffect>().OnCanDrink = () => controller.SelectedQuickSlotIdx = 1;
            SessionSettings.RabbitPotionsForUse = new(true, false); install(); controller.GuestUseItemKeyDown();
            assert(controller.StartedDrinks == 0, "Re-reading a different selected HP potion permitted the original MP potion");
        });
        check("no Tension or no boss keeps normal potion admission", () =>
        {
            var (_, controller, _, _) = setup(2, false); Boss(); install();
            FloorGenerator.All["floor"].BossBattle = false; controller.GuestUseItemKeyDown();
            FloorGenerator.All["floor"].BossBattle = true; DungeonManager.Instance.hardModeEnvironment.Clear(); controller.GuestUseItemKeyDown();
            assert(controller.StartedDrinks == 2, "Native unblocked potion use changed");
        });
    }
}
