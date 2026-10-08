using Mirror;
using SephiriaOne;
using UnityEngine;

int passed = 0, failed = 0;
void Check(string name, Action action)
{
    RabbitPotionFeature.Shutdown();
    NetworkServer.active = true;
    NetworkServer.connections.Clear();
    PlayerSpawner.MultiplayerList.Clear();
    DungeonManager.Instance = new(); FloorGenerator.All.Clear();
    SessionSettings.RabbitPotionsForUse = default;
    try { action(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception error) { Console.WriteLine("FAIL " + name + ": " + error); failed++; }
    finally { RabbitPotionFeature.Shutdown(); }
}
void Assert(bool ok, string reason) { if (!ok) throw new Exception(reason); }
(PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item) Setup(int id = 0, bool local = false)
{
    var player = new PlayerAvatar(); _ = new PlayerSpawner(player, local);
    var controller = new ItemController { Avatar = player, connectionToClient = player.spawner.connectionToClient };
    var potion = new WieldingPotion { entityID = id, effect = new PotionEffect_Regeneration(), NetworkController = controller };
    controller.currentWieldingItem = potion;
    var item = new NewItemOwnInstance { EntityID = id, InstanceID = 52, Quantity = 3 };
    player.Inventory.items[new(0, 0)] = item;
    return (player, controller, potion, item);
}
void Install() { RabbitPotionFeature.Initialize(); Assert(RabbitPotionFeature.Available, "Hook unavailable"); }

TensionTests.Run(Check, Assert, Setup, Install);

Check("off preserves native consumption, healing and both events", () =>
{
    var (player, controller, _, item) = Setup(); int serverEvents = 0;
    controller.OnDrinkPotionServerside += _ => serverEvents++;
    Install(); controller.RunDrink();
    Assert(item.Quantity == 2 && player.Heals.SequenceEqual(new[] { 20f }) && player.DrinkEvents == 1 && serverEvents == 1, "Native behavior changed");
});
Check("infinite retains item after native effect and both events", () =>
{
    var (player, controller, _, item) = Setup(); int serverEvents = 0;
    controller.OnDrinkPotionServerside += _ => serverEvents++;
    SessionSettings.RabbitPotionsForUse = new(true, false);
    Install(); controller.RunDrink();
    Assert(item.Quantity == 3 && player.Heals.SequenceEqual(new[] { 20f }) && player.DrinkEvents == 1 && serverEvents == 1, "Infinite behavior incorrect");
});
foreach (bool local in new[] { false, true })
Check("Sample consumes each unit and retains Survival with every option enabled, local " + local, () =>
{
    var (player, controller, _, item) = Setup(37, local); item.Quantity = 2;
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    var near = new PlayerAvatar(); _ = new PlayerSpawner(near);
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(item.Quantity == 1 && passive.StatGains == 1, "First Sample was retained or Survival was suppressed");
    controller.RunDrink();
    Assert(item.Quantity == 0 && passive.StatGains == 2 && player.mp == 10 && near.Heals.Count == 2,
        "Last Sample was retained or independent MP/sharing behavior changed");
});
Check("share forwards actual bonus adjusted percentage once within strict radius", () =>
{
    var (player, controller, _, item) = Setup(); player.PotionBonus = 25;
    var near = new PlayerAvatar(); _ = new PlayerSpawner(near); near.transform.position = new(9.99f, 0, 0);
    var edge = new PlayerAvatar(); _ = new PlayerSpawner(edge); edge.transform.position = new(10, 0, 0);
    var invalid = new PlayerAvatar(); _ = new PlayerSpawner(invalid); invalid.transform.position = new(float.NaN, 0, 0);
    SessionSettings.RabbitPotionsForUse = new(false, true);
    Install(); controller.RunDrink();
    Assert(item.Quantity == 2 && player.Heals.SequenceEqual(new[] { 25f }) && near.Heals.SequenceEqual(new[] { 25f }) &&
        edge.Heals.Count == 0 && invalid.Heals.Count == 0, "Share percentage or boundary incorrect");
});
Check("live costume and quickslot instance changes disable optional effects", () =>
{
    var (player, controller, potion, item) = Setup(); SessionSettings.RabbitPotionsForUse = new(true, true);
    var nearby = new PlayerAvatar(); _ = new PlayerSpawner(nearby);
    Install(); player.currentCostume = "PinkRabbit"; controller.RunDrink();
    Assert(item.Quantity == 2 && nearby.Heals.Count == 0, "Costume change ignored");
    player.currentCostume = "HolyRabbit"; item.InstanceID = 53;
    player.OnPotionEvent = () => item.InstanceID = 54;
    controller.RunDrink();
    Assert(item.Quantity == 1 && nearby.Heals.Count == 0, "Stale instance shared or retained");
});
Check("unsupported id and concrete effect remain native", () =>
{
    SessionSettings.RabbitPotionsForUse = new(true, true); Install();
    var (player, controller, potion, item) = Setup(2);
    controller.RunDrink(); Assert(item.Quantity == 2 && player.Heals.Count == 1, "Unsupported id intercepted");
    var second = Setup(); second.potion.effect = new OtherEffect(); second.controller.RunDrink();
    Assert(second.item.Quantity == 2 && second.player.DrinkEvents == 1, "Other effect intercepted");
});
Check("failed effect and cancelled controller call do not consume or share", () =>
{
    var (player, controller, potion, item) = Setup(); var near = new PlayerAvatar(); _ = new PlayerSpawner(near);
    SessionSettings.RabbitPotionsForUse = new(true, true); Install();
    controller.SelectedQuickSlotIdx = -1; controller.RunDrink();
    Assert(item.Quantity == 3 && player.Heals.Count == 0, "Cancelled call ran");
    controller.SelectedQuickSlotIdx = 0; potion.effect = new ThrowingRegeneration();
    controller.RunDrink();
    Assert(item.Quantity == 3 && near.Heals.Count == 0, "Failed call forwarded healing");
});
Check("recipient filtering, error containment, and shutdown", () =>
{
    var (player, controller, _, item) = Setup(); SessionSettings.RabbitPotionsForUse = new(true, true);
    var dead = new PlayerAvatar { IsDead = true }; _ = new PlayerSpawner(dead);
    var floor = new PlayerAvatar { currentFloorGuid = "elsewhere" }; _ = new PlayerSpawner(floor);
    var stale = new PlayerAvatar(); _ = new PlayerSpawner(stale); stale.spawner.connectionToClient.isReady = false;
    var error = new ErrorAvatar(); _ = new PlayerSpawner(error);
    var valid = new PlayerAvatar(); _ = new PlayerSpawner(valid);
    Install(); controller.RunDrink();
    Assert(item.Quantity == 3 && dead.Heals.Count == 0 && floor.Heals.Count == 0 && stale.Heals.Count == 0 && valid.Heals.Count == 1, "Recipient filtering failed");
    RabbitPotionFeature.Shutdown(); controller.RunDrink();
    Assert(item.Quantity == 2 && valid.Heals.Count == 1, "Shutdown left hook active");
});
Check("nested unrelated regeneration does not count as the outer drink heal", () =>
{
    var (player, controller, potion, item) = Setup();
    var nearby = new PlayerAvatar(); _ = new PlayerSpawner(nearby);
    var nested = new WieldingPotion { entityID = 0, effect = new PotionEffect_Regeneration(), NetworkController = controller };
    player.OnPotionEvent = () => { player.OnPotionEvent = null; nested.Drink(out _, item.InstanceID); };
    SessionSettings.RabbitPotionsForUse = new(true, true);
    Install(); controller.RunDrink();
    Assert(player.Heals.Count == 2 && nearby.Heals.Count == 1 && item.Quantity == 3,
        "Nested drink leaked into outer context or duplicated share");
});
Check("mid-drink reset, stale connection and changed run fail closed", () =>
{
    var (player, controller, _, item) = Setup();
    var nearby = new PlayerAvatar(); _ = new PlayerSpawner(nearby);
    SessionSettings.RabbitPotionsForUse = new(true, true); Install();
    player.OnPotionEvent = () => SessionSettings.RabbitPotionsForUse = default;
    controller.RunDrink();
    Assert(item.Quantity == 2 && nearby.Heals.Count == 0, "Mid-drink reset ignored");
    SessionSettings.RabbitPotionsForUse = new(true, true);
    player.OnPotionEvent = () => SaveManager.CurrentRun = new();
    controller.RunDrink();
    Assert(item.Quantity == 1 && nearby.Heals.Count == 0, "Run change ignored");
    player.OnPotionEvent = null;
    player.spawner.connectionToClient.identity.Owner = nearby.spawner;
    controller.RunDrink();
    Assert(item.Quantity == 0 && nearby.Heals.Count == 0, "Stale connection authorized");
});
Check("dungeon replacement during effect fails closed even with same run object", () =>
{
    var (player, controller, _, item) = Setup();
    var nearby = new PlayerAvatar(); _ = new PlayerSpawner(nearby);
    SessionSettings.RabbitPotionsForUse = new(true, true); Install();
    player.OnPotionEvent = () => DungeonManager.Instance = new();
    controller.RunDrink();
    Assert(item.Quantity == 2 && nearby.Heals.Count == 0, "Old session applied after dungeon replacement");
});
Check("source floor change during effect fails closed", () =>
{
    var (player, controller, _, item) = Setup();
    var nearby = new PlayerAvatar(); _ = new PlayerSpawner(nearby);
    SessionSettings.RabbitPotionsForUse = new(true, true); Install();
    player.OnPotionEvent = () => player.currentFloorGuid = "next-floor";
    controller.RunDrink();
    Assert(item.Quantity == 2 && nearby.Heals.Count == 0, "Old-floor drink applied after travel");
});
Check("connection removed during share does not heal stale recipient", () =>
{
    var (_, controller, _, _) = Setup();
    var first = new PlayerAvatar(); _ = new PlayerSpawner(first);
    var later = new PlayerAvatar(); _ = new PlayerSpawner(later);
    first.OnHealed = () => NetworkServer.connections.Remove(3);
    SessionSettings.RabbitPotionsForUse = new(false, true); Install(); controller.RunDrink();
    Assert(first.Heals.Count == 1 && later.Heals.Count == 0, "Disconnected snapshot recipient healed");
});
Check("standard HP potion stays infinite but Sample consumes normally", () =>
{
    SessionSettings.RabbitPotionsForUse = new(true, false); Install();
    foreach (int id in new[] { 1, 37 })
    {
        var (_, controller, _, item) = Setup(id); controller.RunDrink();
        Assert(item.Quantity == (id == 37 ? 2 : 3), "Wrong consumption policy for stock id: " + id);
    }
    var zero = Setup(); zero.item.Quantity = 0; zero.controller.RunDrink();
    Assert(zero.item.Quantity == -1, "Empty item intercepted");
});
Check("shared healing uses each recipient's native penalty and HP cap", () =>
{
    var (player, controller, _, _) = Setup(); player.PotionBonus = 50;
    var limited = new PlayerAvatar { Hp = 10f, HealingPenalty = .5f }; _ = new PlayerSpawner(limited);
    var capped = new PlayerAvatar { Hp = 95f }; _ = new PlayerSpawner(capped);
    SessionSettings.RabbitPotionsForUse = new(false, true); Install(); controller.RunDrink();
    Assert(limited.Heals.SequenceEqual(new[] { 30f }) && limited.Hp == 25f &&
        capped.Heals.SequenceEqual(new[] { 30f }) && capped.Hp == 100f,
        "Recipient native healing calculation bypassed");
});
Check("MP fee uses synchronized state once and keeps native potion callbacks", () =>
{
    var (player, controller, _, item) = Setup(); int events = 0;
    controller.OnDrinkPotionServerside += _ => events++;
    SessionSettings.RabbitPotionsForUse = new(false, false, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 20 && player.MpWrites == 1 && player.MpUseEvents == 0 && player.Heals.Count == 1 &&
        player.DrinkEvents == 1 && events == 1 && item.Quantity == 2 && controller.CleanupCalls == 1, "Fee or native completion incorrect");
});
Check("insufficient MP rejects full operation and preserves cleanup", () =>
{
    var (player, controller, _, item) = Setup(); player.mp = 9; int events = 0;
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    var near = new PlayerAvatar(); _ = new PlayerSpawner(near);
    controller.OnDrinkPotionServerside += _ => events++;
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 9 && player.MpWrites == 0 && player.Heals.Count == 0 && player.DrinkEvents == 0 &&
        passive.StatGains == 0 && near.Heals.Count == 0 && events == 0 && item.Quantity == 3 && controller.CleanupCalls == 1,
        "Rejected drink leaked effects or skipped cleanup");
});
Check("Survival suppression is independent and preserves unrelated drink listeners", () =>
{
    var (player, controller, _, item) = Setup(); var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    int other = 0; player.OnDrinkPotion += _ => other++;
    SessionSettings.RabbitPotionsForUse = new(false, false, false, true);
    Install(); controller.RunDrink();
    Assert(passive.StatGains == 0 && other == 1 && player.DrinkEvents == 1 && player.mp == 30 &&
        player.Heals.Count == 1 && item.Quantity == 2, "Survival suppression changed unrelated effects");
});
Check("nested direct call with the same potion does not inherit completion suppression", () =>
{
    var (player, controller, potion, item) = Setup();
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    player.OnPotionEvent = () => { player.OnPotionEvent = null; potion.Drink(out _, item.InstanceID); };
    SessionSettings.RabbitPotionsForUse = new(false, false, false, true);
    Install(); controller.RunDrink();
    Assert(passive.StatGains == 1, "Nested direct call borrowed outer completion scope");
});
Check("server shutdown within event restores native Survival", () =>
{
    var (player, controller, _, _) = Setup();
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    player.OnPotionEvent = () => NetworkServer.active = false;
    SessionSettings.RabbitPotionsForUse = new(false, false, false, true);
    Install(); controller.RunDrink();
    Assert(passive.StatGains == 1, "Stopped server retained suppression scope");
});
foreach (int potionId in new[] { 0, 1, 37 })
foreach (int flags in Enumerable.Range(0, 16))
    Check($"potion {potionId}, independent option combination {flags}", () =>
    {
        bool infinite = (flags & 1) != 0, share = (flags & 2) != 0, mp = (flags & 4) != 0, suppress = (flags & 8) != 0;
        var (player, controller, _, item) = Setup(potionId); var near = new PlayerAvatar(); _ = new PlayerSpawner(near);
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        var recipientPassive = new PassiveObject_PotionAndRandomStat(); recipientPassive.Enable(near);
        SessionSettings.RabbitPotionsForUse = new(infinite, share, mp, suppress);
        Install(); controller.RunDrink();
        Assert(item.Quantity == (infinite && potionId != 37 ? 3 : 2) && near.Heals.Count == (share ? 1 : 0) &&
            player.mp == (mp ? 20 : 30) && passive.StatGains == (suppress && potionId != 37 ? 0 : 1) &&
            recipientPassive.StatGains == 0 && near.DrinkEvents == 0, "Option coupling or recipient gained potion stats");
    });
foreach (int id in new[] { 0, 1, 37 })
    foreach (string costume in new[] { "HolyRabbit", "PinkRabbit" })
        foreach (bool suppress in new[] { false, true })
            Check($"shared HP potion {id}, recipient {costume}, suppression {suppress} grants no recipient Survival stats", () =>
            {
                var source = Setup(id); var recipient = Setup(); recipient.player.currentCostume = costume;
                recipient.player.transform.position = new(9, 0, 0);
                var sourcePassive = new PassiveObject_PotionAndRandomStat(); sourcePassive.Enable(source.player);
                var recipientPassive = new PassiveObject_PotionAndRandomStat(); recipientPassive.Enable(recipient.player);
                int recipientEvents = 0; recipient.controller.OnDrinkPotionServerside += _ => recipientEvents++;
                SessionSettings.RabbitPotionsForUse = new(true, true, true, suppress);
                Install(); source.controller.RunDrink();
                Assert(sourcePassive.StatGains == (suppress && id != 37 ? 0 : 1) && recipientPassive.StatGains == 0,
                    "Shared potion granted recipient Survival or changed source suppression");
                Assert(recipient.player.Hp == 40 && recipient.player.Heals.Count == 1 && recipient.player.DrinkEvents == 0 &&
                    recipientEvents == 0 && recipient.item.Quantity == 3 && recipient.player.mp == 30,
                    "Recipient must gain only healing, without potion events, inventory loss or MP cost");
            });
foreach (bool manaPotion in new[] { false, true })
    Check($"recipient's own {(manaPotion ? "mana" : "non-Rabbit HP")} potion retains Survival during and after shared healing", () =>
    {
        var source = Setup(); var recipient = Setup();
        if (manaPotion) recipient.potion.effect = new PotionEffect_Concentration();
        else recipient.player.currentCostume = "PinkRabbit";
        var sourcePassive = new PassiveObject_PotionAndRandomStat(); sourcePassive.Enable(source.player);
        var recipientPassive = new PassiveObject_PotionAndRandomStat(); recipientPassive.Enable(recipient.player);
        recipient.player.OnHealed = () => { recipient.player.OnHealed = null; recipient.controller.RunDrink(); };
        SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
        Install(); source.controller.RunDrink();
        Assert(sourcePassive.StatGains == 0 && recipientPassive.StatGains == 1 && recipient.player.DrinkEvents == 1 &&
            recipient.item.Quantity == 2 && recipient.player.mp == 30, "Shared healing suppressed a separate native potion use");
        recipient.controller.RunDrink();
        Assert(recipientPassive.StatGains == 2 && recipient.player.DrinkEvents == 2 && recipient.item.Quantity == 1,
            "Suppression leaked beyond shared healing");
    });
Check("failed recipient healing leaves other recipients and later native Survival intact", () =>
{
    var source = Setup(); var failedRecipient = Setup(); var laterRecipient = Setup();
    failedRecipient.player.currentCostume = laterRecipient.player.currentCostume = "PinkRabbit";
    var failedPassive = new PassiveObject_PotionAndRandomStat(); failedPassive.Enable(failedRecipient.player);
    var laterPassive = new PassiveObject_PotionAndRandomStat(); laterPassive.Enable(laterRecipient.player);
    failedRecipient.player.OnHealed = () => throw new InvalidOperationException("recipient HP callback failed");
    SessionSettings.RabbitPotionsForUse = new(true, true, false, true);
    Install(); source.controller.RunDrink();
    Assert(failedPassive.StatGains == 0 && laterPassive.StatGains == 0 && laterRecipient.player.Hp == 40,
        "Recipient failure granted potion stats or stopped subsequent sharing");
    failedRecipient.player.OnHealed = null;
    failedRecipient.controller.RunDrink(); laterRecipient.controller.RunDrink();
    Assert(failedPassive.StatGains == 1 && laterPassive.StatGains == 1, "Failure leaked suppression into subsequent native drinks");
});
Check("fixed fee ignores MP immunity and exact funds reach zero", () =>
{
    var (player, controller, _, _) = Setup(); player.mp = 10; player.InfinityMp = true;
    SessionSettings.RabbitPotionsForUse = new(false, false, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 0 && player.Heals.Count == 1 && player.MpUseEvents == 0, "Fixed fee unexpectedly waived");
});
Check("cancelled animation costs nothing and cleanup remains", () =>
{
    var (player, controller, _, item) = Setup(); controller.SelectedQuickSlotIdx = -1;
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 30 && player.MpWrites == 0 && player.Heals.Count == 0 && item.Quantity == 3 &&
        controller.CleanupCalls == 1, "Cancellation charged or skipped cleanup");
});
Check("failed synchronized fee write blocks healing and both events", () =>
{
    var (player, controller, _, item) = Setup(); player.FailMpWrite = true; int events = 0;
    controller.OnDrinkPotionServerside += _ => events++;
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 30 && player.Heals.Count == 0 && player.DrinkEvents == 0 && events == 0 &&
        item.Quantity == 3 && controller.CleanupCalls == 1, "Failed fee granted free potion");
});
Check("callback fault retains fee and unrelated MP changes without replay", () =>
{
    var (player, controller, potion, item) = Setup();
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    player.OnPotionEvent = () => { player.Networkmp += 3; throw new InvalidOperationException("native proc"); };
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 23 && player.Heals.Count == 0 && item.Quantity == 3 && controller.CleanupCalls == 1,
        "Callback fault refunded, overwrote native MP or skipped cleanup");
    player.OnPotionEvent = null;
    passive.Invoke(potion.effect);
    Assert(passive.StatGains == 1, "Fault leaked suppression scope");
    SessionSettings.RabbitPotionsForUse = default;
    controller.RunDrink();
    Assert(player.mp == 23 && player.Heals.Count == 1 && item.Quantity == 2, "Fee replayed after reset");
});
Check("controller callback fault retains fee and cleanup without consuming item", () =>
{
    var (player, controller, _, item) = Setup();
    controller.OnDrinkPotionServerside += _ => throw new InvalidOperationException("controller proc");
    SessionSettings.RabbitPotionsForUse = new(false, false, true, true);
    Install(); controller.RunDrink();
    Assert(player.mp == 20 && player.Heals.Count == 1 && item.Quantity == 3 && controller.CleanupCalls == 1,
        "Controller fault rolled back fee or failed cleanup");
});
Check("other costume mana status buff and derived regeneration remain native", () =>
{
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true); Install();
    foreach (int scenario in Enumerable.Range(0, 5))
    {
        var (player, controller, potion, item) = Setup(scenario == 1 ? 2 : 0);
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        if (scenario == 0) player.currentCostume = "PinkRabbit";
        if (scenario == 1) potion.effect = new PotionEffect_Concentration();
        if (scenario == 2) potion.effect = new PotionEffect_StatusInstance();
        if (scenario == 3) potion.effect = new OtherEffect();
        if (scenario == 4) potion.effect = new DerivedRegeneration();
        controller.RunDrink();
        Assert(player.mp == 30 && item.Quantity == 2 && passive.StatGains == 1, "Non-eligible native potion intercepted: " + scenario);
    }
});
Check("other player and other effect Survival callbacks stay active inside eligible drink", () =>
{
    var (player, controller, potion, _) = Setup();
    var own = new PassiveObject_PotionAndRandomStat(); own.Enable(player);
    var other = new PassiveObject_PotionAndRandomStat(); other.Enable(new PlayerAvatar());
    player.OnPotionEvent = () => { own.Invoke(new OtherEffect()); other.Invoke(potion.effect); };
    SessionSettings.RabbitPotionsForUse = new(false, false, false, true);
    Install(); controller.RunDrink();
    Assert(own.StatGains == 1 && other.StatGains == 1, "Other player/effect callback suppressed");
});
Check("nested eligible controller charges independently and restores outer suppression", () =>
{
    var first = Setup(); var second = Setup();
    var one = new PassiveObject_PotionAndRandomStat(); one.Enable(first.player);
    var two = new PassiveObject_PotionAndRandomStat(); two.Enable(second.player);
    first.player.OnPotionEvent = () => second.controller.RunDrink();
    SessionSettings.RabbitPotionsForUse = new(false, false, true, true);
    Install(); first.controller.RunDrink();
    Assert(first.player.mp == 20 && second.player.mp == 20 && one.StatGains == 0 && two.StatGains == 0 &&
        first.player.Heals.Count == 1 && second.player.Heals.Count == 1, "Nested completion scopes leaked");
});
foreach (int outerId in new[] { 0, 37 })
    Check($"nested sample/regular drinks preserve separate consumption and Survival policy, outer {outerId}", () =>
    {
        var outer = Setup(outerId); var inner = Setup(outerId == 0 ? 37 : 0);
        var outerPassive = new PassiveObject_PotionAndRandomStat(); outerPassive.Enable(outer.player);
        var innerPassive = new PassiveObject_PotionAndRandomStat(); innerPassive.Enable(inner.player);
        outer.player.OnPotionEvent = () => inner.controller.RunDrink();
        SessionSettings.RabbitPotionsForUse = new(true, false, true, true);
        Install(); outer.controller.RunDrink();
        Assert(outerPassive.StatGains == (outerId == 37 ? 1 : 0) &&
            innerPassive.StatGains == (outerId == 37 ? 0 : 1) && outer.player.mp == 20 && inner.player.mp == 20,
            "Nested sample exemption leaked into another drink or changed MP charging");
        Assert(outer.item.Quantity == (outerId == 37 ? 2 : 3) && inner.item.Quantity == (outerId == 37 ? 3 : 2),
            "Nested Sample consumption exemption leaked into the regular potion");
    });
Check("live guest ownership is required and inactive client stays native", () =>
{
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true); Install();
    var guest = Setup(); guest.controller.RunDrink();
    Assert(guest.player.mp == 20 && guest.item.Quantity == 3, "Live guest not charged by host");
    var stale = Setup(); NetworkServer.connections.Remove(PlayerSpawner.MultiplayerList.Count);
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(stale.player);
    stale.controller.RunDrink();
    Assert(stale.player.mp == 30 && stale.item.Quantity == 2 && passive.StatGains == 1, "Disconnected source intercepted");
    var client = Setup(); NetworkServer.active = false; client.controller.RunDrink();
    Assert(client.player.mp == 30 && client.item.Quantity == 2, "Client applied host fee");
});
Check("mid-drink lifetime changes disable Survival without a deferred MP debit", () =>
{
    foreach (int change in Enumerable.Range(0, 6))
    {
        var (player, controller, _, _) = Setup();
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        SessionSettings.RabbitPotionsForUse = new(false, false, true, true);
        player.OnPotionEvent = () =>
        {
            if (change == 0) player.currentCostume = "PinkRabbit";
            if (change == 1) SaveManager.CurrentRun = new();
            if (change == 2) DungeonManager.Instance = new();
            if (change == 3) player.currentFloorGuid = "next";
            if (change == 4) player.spawner.connectionToClient.isReady = false;
            if (change == 5) SessionSettings.RabbitPotionsForUse = default;
            player.Networkmp = 7;
        };
        Install(); controller.RunDrink();
        Assert(player.mp == 7 && passive.StatGains == 1, "Stale lifetime retained suppression or deferred fee: " + change);
    }
});
Check("unload removes fee and Survival patches", () =>
{
    var (player, controller, _, _) = Setup(); var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    SessionSettings.RabbitPotionsForUse = new(false, false, true, true);
    Install(); RabbitPotionFeature.Shutdown(); controller.RunDrink();
    Assert(player.mp == 30 && passive.StatGains == 1, "Balance hook survived unload");
});
Check("replaced source connection and despawned potion cannot retain completion scope", () =>
{
    foreach (bool replaceConnection in new[] { false, true })
    {
        var (player, controller, potion, _) = Setup();
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        SessionSettings.RabbitPotionsForUse = new(false, false, true, true);
        player.OnPotionEvent = () =>
        {
            if (!replaceConnection) potion.isServer = false;
            else
            {
                var replacement = new NetworkConnectionToClient { identity = new NetworkIdentity { Owner = player.spawner } };
                player.spawner.connectionToClient = replacement; controller.connectionToClient = replacement;
                NetworkServer.connections[PlayerSpawner.MultiplayerList.Count] = replacement;
            }
        };
        Install(); controller.RunDrink();
        Assert(passive.StatGains == 1 && player.mp == 20, "Changed source retained scoped suppression");
    }
});
foreach (int cost in new[] { 0, 1, 25, 10000 })
    foreach (int balance in new[] { cost - 1, cost, cost + 7 })
        Check($"custom fee {cost} with balance {balance} preserves full drink boundary", () =>
        {
            var (player, controller, _, item) = Setup(); player.mp = balance;
            var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
            var near = new PlayerAvatar(); _ = new PlayerSpawner(near);
            SessionSettings.RabbitPotionsForUse = new(true, true, true, true, cost);
            Install(); controller.RunDrink();
            bool allowed = cost == 0 || balance >= cost;
            Assert(player.mp == (allowed ? balance - cost : balance) && player.MpWrites == (allowed && cost > 0 ? 1 : 0), "Configured debit incorrect");
            Assert(player.Heals.Count == (allowed ? 1 : 0) && near.Heals.Count == (allowed ? 1 : 0) &&
                player.DrinkEvents == (allowed ? 1 : 0) && passive.StatGains == 0 && item.Quantity == 3 && controller.CleanupCalls == 1,
                "Configured fee changed drink events, sharing, consumption, Survival or cleanup");
        });
Check("fee changes before completion use current amount and callbacks do not reprice drink", () =>
{
    var (player, controller, _, _) = Setup();
    SessionSettings.RabbitPotionsForUse = new(true, false, true, false, 25); Install();
    SessionSettings.RabbitPotionsForUse = new(true, false, true, false, 7);
    player.OnPotionEvent = () => SessionSettings.RabbitPotionsForUse = new(true, false, true, false, 50);
    controller.RunDrink();
    Assert(player.mp == 23 && player.MpWrites == 1 && player.Heals.Count == 1, "Completion did not capture current fee exactly once");
    controller.RunDrink();
    Assert(player.mp == 23 && player.Heals.Count == 1, "Next drink used stale affordable fee");
});
Check("disabled saved custom amount leaves native MP and consumption unchanged", () =>
{
    var (player, controller, _, item) = Setup();
    SessionSettings.RabbitPotionsForUse = new(false, false, false, false, 10000);
    Install(); controller.RunDrink();
    Assert(player.mp == 30 && player.MpWrites == 0 && player.Heals.Count == 1 && item.Quantity == 2, "Disabled custom cost changed native drink");
});
foreach (int potionId in new[] { 0, 1, 37 })
foreach (int phase in new[] { 0, 1, 2 })
    foreach (bool unwield in new[] { false, true })
        Check($"potion {potionId}, death at phase {phase}, wield cleanup {unwield} retains admitted policy", () =>
        {
            var (player, controller, potion, item) = Setup(potionId);
            var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
            var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
            void Die()
            {
                if (unwield) controller.CancelAction();
                player.IsDead = true; player.Hp = 0;
            }
            if (phase == 0) player.OnPotionEvent = Die;
            if (phase == 1) player.OnHealed = Die;
            if (phase == 2) controller.OnDrinkPotionServerside += _ => Die();
            SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
            Install(); controller.RunDrink();
            Assert(passive.StatGains == (potionId == 37 ? 1 : 0), "Death changed the potion's Survival suppression policy");
            Assert(item.Quantity == (potionId == 37 ? 2 : 3), "Death changed the potion's consumption policy");
            Assert(player.IsDead && player.Hp == 0 && player.mp == 20 && player.MpWrites == 1 && controller.CleanupCalls == 1,
                "Death, charged MP or native completion cleanup was altered");
            Assert(recipient.Heals.Count == (phase == 2 ? 1 : 0), "Dead source started shared healing");
        });
foreach (bool infinite in new[] { false, true })
Check("sample with inapplicable infinite/suppression preserves native late completion, infinite " + infinite, () =>
{
    var (player, controller, _, item) = Setup(37); player.IsDead = true; player.Hp = 0;
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    SessionSettings.RabbitPotionsForUse = new(infinite, false, false, true);
    Install(); controller.RunDrink();
    Assert(passive.StatGains == 1 && player.DrinkEvents == 1 && item.Quantity == 2 &&
        player.mp == 30 && controller.CleanupCalls == 1, "Inapplicable options still intercepted the sample");
});
Check("late HP potion completion after death rejects all effects without cost or consumption", () =>
{
    var (player, controller, _, item) = Setup(); player.IsDead = true; player.Hp = 0;
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    int controllerEvents = 0; controller.OnDrinkPotionServerside += _ => controllerEvents++;
    var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(item.Quantity == 3 && passive.StatGains == 0 && player.DrinkEvents == 0 && controllerEvents == 0,
        "Dead pending completion consumed a potion or triggered potion events");
    Assert(player.mp == 30 && player.MpWrites == 0 && player.Hp == 0 && recipient.Heals.Count == 0 && controller.CleanupCalls == 1,
        "Dead pending completion charged MP, healed or skipped cleanup");
});
foreach (int flags in Enumerable.Range(0, 16))
    Check("death retains independent options " + flags, () =>
    {
        var (player, controller, _, item) = Setup();
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
        player.OnPotionEvent = () => { player.IsDead = true; player.Hp = 0; };
        bool infinite = (flags & 1) != 0, share = (flags & 2) != 0, mp = (flags & 4) != 0, suppress = (flags & 8) != 0;
        SessionSettings.RabbitPotionsForUse = new(infinite, share, mp, suppress);
        Install(); controller.RunDrink();
        Assert(item.Quantity == (infinite ? 3 : 2) && passive.StatGains == (suppress ? 0 : 1) &&
            player.mp == (mp ? 20 : 30) && recipient.Heals.Count == 0, "Death coupled independent settings");
    });
foreach (string change in new[] { "run", "dungeon", "floor", "costume", "connection", "inventory", "item", "entity", "slot", "reset", "server" })
    Check("death exception still rejects changed " + change, () =>
    {
        var (player, controller, _, item) = Setup();
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        player.OnPotionEvent = () =>
        {
            player.IsDead = true; player.Hp = 0;
            if (change == "run") SaveManager.CurrentRun = new();
            if (change == "dungeon") DungeonManager.Instance = new();
            if (change == "floor") player.currentFloorGuid = "next";
            if (change == "costume") player.currentCostume = "PinkRabbit";
            if (change == "connection") player.spawner.connectionToClient.isReady = false;
            if (change == "inventory")
            {
                player.Inventory = new();
                player.Inventory.items[new(0, 0)] = item;
            }
            if (change == "item") player.Inventory.items[new(0, 0)] = new NewItemOwnInstance { EntityID = 0, InstanceID = 53, Quantity = 3 };
            if (change == "entity") item.EntityID = 2;
            if (change == "slot") controller.SelectedQuickSlotIdx = -1;
            if (change == "reset") SessionSettings.RabbitPotionsForUse = default;
            if (change == "server") NetworkServer.active = false;
        };
        SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
        Install(); controller.RunDrink();
        Assert(passive.StatGains == 1 && player.Inventory.items[new(0, 0)].Quantity == 2,
            "Death bypassed real lifetime or inventory invalidation");
    });
Check("revival and later native drink do not inherit the completed death scope", () =>
{
    var (player, controller, _, item) = Setup();
    var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
    player.OnPotionEvent = () => { player.IsDead = true; player.Hp = 0; };
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    Assert(passive.StatGains == 0 && item.Quantity == 3, "Dying drink lost protection");
    player.OnPotionEvent = null; player.IsDead = false; player.Hp = 20; player.currentCostume = "PinkRabbit";
    controller.RunDrink();
    Assert(passive.StatGains == 1 && item.Quantity == 2 && player.mp == 20, "Completed death scope leaked into revived player");
});
foreach (string scenario in new[] { "all-off", "remembered-fee-only", "other-costume", "mana-potion" })
    Check("dead late completion preserves native behavior for " + scenario, () =>
    {
        var (player, controller, potion, item) = Setup(); player.IsDead = true; player.Hp = 0;
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        SessionSettings.RabbitPotionsForUse = scenario == "all-off" ? default :
            scenario == "remembered-fee-only" ? new(false, false, false, false, 25) : new(true, true, true, true);
        if (scenario == "other-costume") player.currentCostume = "PinkRabbit";
        if (scenario == "mana-potion") potion.effect = new PotionEffect_Concentration();
        Install(); controller.RunDrink();
        Assert(item.Quantity == 2 && passive.StatGains == 1 && player.mp == 30 && controller.CleanupCalls == 1,
            "Death guard intercepted an unmodified native drink");
    });
foreach (bool local in new[] { true, false })
    Check($"insufficient MP displays one private {(local ? "system" : "floating")} alert with live amounts", () =>
    {
        var (player, controller, _, item) = Setup(local: local); player.mp = 6;
        var other = Setup(local: !local);
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 25);
        Install(); controller.RunDrink();
        string expected = "Not enough MP to heal (6/25 MP).";
        if (local)
            Assert(player.SystemMessages.Count == 1 && player.SystemMessages[0] == (expected, 2.5f, false) &&
                player.spawner.connectionToClient.Notices.Count == 0, "Local alert missing or duplicated over network");
        else
        {
            var notices = player.spawner.connectionToClient.Notices;
            Assert(notices.Count == 1 && player.SystemMessages.Count == 0, "Guest alert missing or displayed on host");
            var notice = notices[0];
            Assert(ReferenceEquals(notice.Owner, player) && notice.Hash == -1687250273 && notice.Channel == 0 &&
                notice.Payload.Length == 7 && (string)notice.Payload[1] == expected &&
                (int)notice.Payload[3] == 0 && !(bool)notice.Payload[4] &&
                ReferenceEquals(notice.Payload[5], player) && ReferenceEquals(notice.Payload[6], player),
                "Native payload or targeted reliable delivery incorrect");
        }
        Assert(other.player.SystemMessages.Count == 0 && other.player.spawner.connectionToClient.Notices.Count == 0 &&
            player.mp == 6 && player.MpWrites == 0 && player.Heals.Count == 0 && player.DrinkEvents == 0 &&
            passive.StatGains == 0 && item.Quantity == 3 && controller.CleanupCalls == 1 && NetworkWriterPool.Outstanding == 0,
            "Notice leaked to another player, changed rejected drink behavior, or leaked writer");
    });
foreach (bool local in new[] { true, false })
    Check($"{(local ? "local UI" : "guest transport")} failure cannot bypass rejection", () =>
    {
        var (player, controller, _, item) = Setup(local: local); player.mp = 0;
        player.FailSystemMessage = true; player.spawner.connectionToClient.FailNotice = true;
        var passive = new PassiveObject_PotionAndRandomStat(); passive.Enable(player);
        SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
        Install(); controller.RunDrink();
        Assert(item.Quantity == 3 && player.mp == 0 && player.MpWrites == 0 && player.Heals.Count == 0 &&
            player.DrinkEvents == 0 && passive.StatGains == 0 && controller.CleanupCalls == 1 && NetworkWriterPool.Outstanding == 0,
            "Feedback failure consumed, healed, raised Survival or leaked writer");
    });
foreach (string scenario in new[] { "enough", "zero-cost", "disabled", "other-costume", "mana", "dead", "cancelled", "disconnected", "stale-owner" })
    Check("no low-MP alert for " + scenario, () =>
    {
        var (player, controller, potion, _) = Setup(); player.mp = scenario == "enough" ? 10 : 0;
        SessionSettings.RabbitPotionsForUse = new(true, true, scenario != "disabled", true, scenario == "zero-cost" ? 0 : 10);
        switch (scenario)
        {
            case "other-costume": player.currentCostume = "PinkRabbit"; break;
            case "mana": potion.effect = new PotionEffect_Concentration(); break;
            case "dead": player.IsDead = true; break;
            case "cancelled": controller.SelectedQuickSlotIdx = -1; break;
            case "disconnected": NetworkServer.connections.Clear(); break;
            case "stale-owner": player.spawner.connectionToClient.identity.Owner = Setup().player.spawner; break;
        }
        Install(); controller.RunDrink();
        Assert(player.SystemMessages.Count == 0 && player.spawner.connectionToClient.Notices.Count == 0,
            "Misleading low-MP alert outside an eligible insufficient-MP rejection");
    });
Check("repeated low-MP attempts use current fee and reconnection without stale notices", () =>
{
    var (player, controller, _, _) = Setup(); player.mp = 1;
    SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
    Install(); controller.RunDrink();
    var old = player.spawner.connectionToClient;
    NetworkServer.connections.Clear(); controller.RunDrink();
    var replacement = new NetworkConnectionToClient { identity = new NetworkIdentity { Owner = player.spawner } };
    player.spawner.connectionToClient = controller.connectionToClient = replacement;
    NetworkServer.connections[1] = replacement;
    player.mp = 4; SessionSettings.RabbitPotionsForUse = new(true, true, true, true, 30);
    controller.RunDrink(); controller.RunDrink();
    Assert(old.Notices.Count == 1 && (string)old.Notices[0].Payload[1] == "Not enough MP to heal (1/10 MP)." &&
        replacement.Notices.Count == 2 && replacement.Notices.All(n => (string)n.Payload[1] == "Not enough MP to heal (4/30 MP)."),
        "Repeated attempt used stale amount/connection or reconnect replayed an old alert");
});
foreach (string mutation in new[] { "hash", "name", "write", "read", "order" })
    Check("guest alert refuses changed native contract: " + mutation, () =>
    {
        var send = HarmonyLib.PatchProcessor.GetOriginalInstructions(HarmonyLib.AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcShowDamageParticle")).ToList();
        var receive = HarmonyLib.PatchProcessor.GetOriginalInstructions(HarmonyLib.AccessTools.DeclaredMethod(typeof(UnitAvatar),
            "InvokeUserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar")).ToList();
        Assert(NativePlayerAlert.ValidateCode(send, receive), "Baseline RPC fixture invalid");
        switch (mutation)
        {
            case "hash": send.Single(i => Equals(i.operand, -1687250273)).operand = 123; break;
            case "name": send.Single(i => i.opcode == System.Reflection.Emit.OpCodes.Ldstr).operand = "other RPC"; break;
            case "write": send.RemoveAll(i => i.operand is System.Reflection.MethodInfo m && m.Name == "WriteBool"); break;
            case "read": receive.RemoveAll(i => i.operand is System.Reflection.MethodInfo m && m.Name == "ReadColor"); break;
            case "order":
                var color = send.Single(i => i.operand is System.Reflection.MethodInfo m && m.Name == "WriteColor");
                var text = send.Single(i => i.operand is System.Reflection.MethodInfo m && m.Name == "WriteString");
                (color.operand, text.operand) = (text.operand, color.operand); break;
        }
        Assert(!NativePlayerAlert.ValidateCode(send, receive), "Unsafe RPC contract accepted");
    });
SharedHealVisualTests.Run(Check, Assert, () => Setup(), Install);
L.Initialize(Path.Combine(Path.GetTempPath(), "SephiriaOne-Potion-Language-" + Guid.NewGuid().ToString("N")), _ => { });
Console.WriteLine($"Rabbit localized alert checks passed: {LocalizationAlertTests.Run(language =>
{
    if (!L.TrySetLanguage(language, out string error)) throw new Exception(error);
})}");
L.Shutdown();
Console.WriteLine($"{passed} passed, {failed} failed");
Environment.ExitCode = failed == 0 ? 0 : 1;

class ThrowingRegeneration : PotionEffect_Regeneration
{
    public override void CreateEffect_OnDrink(UnitAvatar avatar) => throw new InvalidOperationException("effect failed");
}
class OtherEffect : PotionEffect { }
class PotionEffect_Concentration : PotionEffect { }
class PotionEffect_StatusInstance : PotionEffect { }
class DerivedRegeneration : PotionEffect_Regeneration { }
class ErrorAvatar : PlayerAvatar { public override void HealPercent(float strength) => throw new InvalidOperationException("recipient failed"); }
