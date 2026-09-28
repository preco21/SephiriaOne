using Mirror;
using SephiriaOne;

internal static class LocalizationAlertTests
{
    public static int Run(Action<string> selectLanguage)
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        { if (!condition) throw new Exception(scenario); checks++; }
        try
        {
            foreach (string language in new[] { "en", "ko" })
                foreach (bool local in new[] { true, false })
                {
                    RabbitPotionFeature.Shutdown();
                    NetworkServer.active = true;
                    NetworkServer.connections.Clear();
                    PlayerSpawner.MultiplayerList.Clear();
                    selectLanguage(language);
                    var player = new PlayerAvatar();
                    _ = new PlayerSpawner(player, local);
                    player.mp = 6;
                    var controller = new ItemController { Avatar = player, connectionToClient = player.spawner.connectionToClient };
                    var potion = new WieldingPotion { entityID = 0, effect = new PotionEffect_Regeneration(), NetworkController = controller };
                    controller.currentWieldingItem = potion;
                    var item = new NewItemOwnInstance { EntityID = 0, InstanceID = 52, Quantity = 3 };
                    player.Inventory.items[new(0, 0)] = item;
                    SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 25);
                    RabbitPotionFeature.Initialize();
                    Check(RabbitPotionFeature.Available, "Rabbit potion hook is available");
                    controller.RunDrink();
                    string expected = language == "en" ? "Not enough MP to heal (6/25 MP)." : "회복에 필요한 MP가 부족합니다 (6/25 MP).";
                    if (local)
                        Check(player.SystemMessages.Count == 1 && player.SystemMessages[0].Message == expected &&
                            player.spawner.connectionToClient.Notices.Count == 0, "Local alert follows the host language");
                    else
                    {
                        var notices = player.spawner.connectionToClient.Notices;
                        Check(notices.Count == 1 && player.SystemMessages.Count == 0 &&
                            (string)notices[0].Payload[1] == expected && ReferenceEquals(notices[0].Owner, player),
                            "Guest native message carries host-localized text without a guest translator");
                    }
                    Check(player.mp == 6 && player.MpWrites == 0 && item.Quantity == 3 && player.Heals.Count == 0 &&
                        player.DrinkEvents == 0 && controller.CleanupCalls == 1 && NetworkWriterPool.Outstanding == 0,
                        "Language leaves rejected-drink state and transport cleanup unchanged");
                }
        }
        finally
        {
            RabbitPotionFeature.Shutdown();
            SessionSettings.RabbitPotionsForUse = default;
            selectLanguage("en");
        }
        return checks;
    }
}
