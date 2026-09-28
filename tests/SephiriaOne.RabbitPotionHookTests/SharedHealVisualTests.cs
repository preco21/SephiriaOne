using Mirror;
using SephiriaOne;
using HarmonyLib;

internal static class SharedHealVisualTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<(PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup,
        Action install)
    {
        check("shared HP gain broadcasts one native FX without recipient potion events", () =>
        {
            var source = setup();
            var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
            var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(recipient);
            SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
            install(); source.controller.RunDrink();
            assert(recipient.Hp == 40 && recipient.Heals.Count == 1 && recipient.HealVisuals == 1,
                "Shared recipient must get one heal and one native green FX");
            assert(source.player.HealVisuals == 0 && recipient.DrinkEvents == 0 && survival.StatGains == 0 &&
                source.item.Quantity == 3 && source.player.mp == 20 && NetworkWriterPool.Outstanding == 0,
                "FX must preserve source potion protections, recipient talents and native payload ownership");
        });
        foreach (string kind in new[] { "full", "zero", "dead", "range", "floor", "off", "mp", "nan", "infinity" })
            check("no shared FX for " + kind, () =>
            {
                var source = setup();
                var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
                SessionSettings.RabbitPotionsForUse = new(true, kind != "off", true, true);
                switch (kind)
                {
                    case "full": recipient.Hp = 100; break;
                    case "zero": recipient.HealingPenalty = 1; break;
                    case "dead": recipient.IsDead = true; break;
                    case "range": recipient.transform.position = new(10, 0, 0); break;
                    case "floor": recipient.currentFloorGuid = "other"; break;
                    case "mp": source.player.mp = 0; break;
                    case "nan": recipient.Hp = float.NaN; break;
                    case "infinity": recipient.MaxHp = float.PositiveInfinity; break;
                }
                install(); source.controller.RunDrink();
                assert(recipient.HealVisuals == 0 && source.player.HealVisuals == 0, "Non-healed recipient received FX");
            });
        foreach (string change in new[] { "recipient death", "source death", "recipient floor", "source floor", "run", "dungeon",
            "removed connection", "replaced connection", "replaced identity", "replaced owner", "not ready", "avatar",
            "netId", "removed player", "source connection", "source costume", "sharing off", "shutdown", "destroyed" })
            check("shared FX rejects lifetime change after heal: " + change, () =>
            {
                var source = setup();
                var recipient = new PlayerAvatar(); var spawner = new PlayerSpawner(recipient);
                SessionSettings.RabbitPotionsForUse = new(true, true, false, true);
                recipient.OnHealed = () =>
                {
                    switch (change)
                    {
                        case "recipient death": recipient.IsDead = true; break;
                        case "source death": source.player.IsDead = true; break;
                        case "recipient floor": recipient.currentFloorGuid = "next"; break;
                        case "source floor": source.player.currentFloorGuid = "next"; break;
                        case "run": SaveManager.CurrentRun = new(); break;
                        case "dungeon": DungeonManager.Instance = new(); break;
                        case "removed connection": NetworkServer.connections.Remove(2); break;
                        case "replaced connection": NetworkServer.connections[2] = new(); break;
                        case "replaced identity": spawner.connectionToClient.identity = new() { Owner = spawner }; break;
                        case "replaced owner": spawner.connectionToClient.identity.Owner = source.player.spawner; break;
                        case "not ready": spawner.connectionToClient.isReady = false; break;
                        case "avatar": spawner.PlayerAvatar = new(); break;
                        case "netId": recipient.netId++; break;
                        case "removed player": PlayerSpawner.MultiplayerList.Remove(spawner); break;
                        case "source connection": NetworkServer.connections.Remove(1); break;
                        case "source costume": source.player.currentCostume = "PinkRabbit"; break;
                        case "sharing off": SessionSettings.RabbitPotionsForUse = default; break;
                        case "shutdown": RabbitPotionFeature.Shutdown(); break;
                        case "destroyed": recipient.Destroyed = true; break;
                    }
                };
                install(); source.controller.RunDrink();
                assert(recipient.Heals.Count == 1 && recipient.Hp == 40 && recipient.HealVisuals == 0,
                    "FX crossed the old recipient/source lifetime or replayed healing");
            });
        check("failed visual disables only particles and never retries healing", () =>
        {
            var source = setup();
            var first = new PlayerAvatar(); _ = new PlayerSpawner(first);
            var later = new PlayerAvatar(); _ = new PlayerSpawner(later);
            var survival = new PassiveObject_PotionAndRandomStat(); survival.Enable(source.player);
            SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
            install();
            int attempted = 0;
            AccessTools.Field(typeof(RabbitPotionNativeHooks), "healVisualChecked").SetValue(null, true);
            AccessTools.Field(typeof(RabbitPotionNativeHooks), "sendHealVisual").SetValue(null,
                (Action<UnitAvatar>)(_ => { attempted++; throw new InvalidOperationException("FX unavailable"); }));
            source.controller.RunDrink(); source.controller.RunDrink();
            assert(attempted == 1 && first.Heals.Count == 2 && later.Heals.Count == 2 &&
                source.item.Quantity == 3 && source.player.mp == 10 && survival.StatGains == 0 && RabbitPotionFeature.Available,
                "Optional FX failure interrupted mechanics, retried the RPC or skipped a recipient");
            RabbitPotionFeature.Shutdown(); install();
            source.controller.RunDrink();
            assert(first.HealVisuals == 1 && later.HealVisuals == 1, "Reload did not reset optional visual compatibility state");
        });
        check("rejoin and new run receive only new shared-heal visuals", () =>
        {
            var source = setup();
            var old = new PlayerAvatar(); var oldSpawner = new PlayerSpawner(old);
            SessionSettings.RabbitPotionsForUse = new(true, true);
            install(); source.controller.RunDrink();
            NetworkServer.connections.Remove(2); PlayerSpawner.MultiplayerList.Remove(oldSpawner);
            var replacement = new PlayerAvatar { netId = old.netId }; _ = new PlayerSpawner(replacement);
            assert(old.HealVisuals == 1 && replacement.HealVisuals == 0, "Reconnect replayed an old transient visual");
            source.controller.RunDrink();
            assert(old.HealVisuals == 1 && replacement.HealVisuals == 1, "Rejoin reused stale visual recipient state");
            SaveManager.CurrentRun = new();
            assert(replacement.HealVisuals == 1, "New run replayed a visual");
            source.controller.RunDrink();
            assert(replacement.HealVisuals == 2 && replacement.Heals.Count == 2, "Fresh run drink did not emit exactly one new visual");
        });
        check("duplicate connection entry cannot duplicate a shared heal or FX", () =>
        {
            var source = setup();
            var recipient = new PlayerAvatar(); var spawner = new PlayerSpawner(recipient);
            NetworkServer.connections[99] = spawner.connectionToClient;
            SessionSettings.RabbitPotionsForUse = new(true, true);
            install(); source.controller.RunDrink();
            assert(recipient.Heals.Count == 1 && recipient.HealVisuals == 1, "Duplicate observer bookkeeping doubled healing/FX");
        });
        check("throwing HP callback produces no misleading FX and preserves later recipients", () =>
        {
            var source = setup();
            var failed = new PlayerAvatar(); _ = new PlayerSpawner(failed);
            var later = new PlayerAvatar(); _ = new PlayerSpawner(later, local: true);
            failed.OnHealed = () => throw new InvalidOperationException("HP callback failed");
            SessionSettings.RabbitPotionsForUse = new(true, true);
            install(); source.controller.RunDrink();
            assert(failed.Heals.Count == 1 && failed.Hp == 40 && failed.HealVisuals == 0 &&
                later.Heals.Count == 1 && later.HealVisuals == 1 && source.item.Quantity == 3,
                "Failed HP callback replayed healing, emitted FX or interrupted the host recipient");
        });
    }
}
