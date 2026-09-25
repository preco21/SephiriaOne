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
    SessionSettings.RabbitPotionsForUse = default;
    try { action(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception error) { Console.WriteLine("FAIL " + name + ": " + error); failed++; }
    finally { RabbitPotionFeature.Shutdown(); }
}
void Assert(bool ok, string reason) { if (!ok) throw new Exception(reason); }
(PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item) Setup(int id = 0)
{
    var player = new PlayerAvatar(); _ = new PlayerSpawner(player);
    var controller = new ItemController { Avatar = player, connectionToClient = player.spawner.connectionToClient };
    var potion = new WieldingPotion { entityID = id, effect = new PotionEffect_Regeneration(), NetworkController = controller };
    controller.currentWieldingItem = potion;
    var item = new NewItemOwnInstance { EntityID = id, InstanceID = 52, Quantity = 3 };
    player.Inventory.items[new(0, 0)] = item;
    return (player, controller, potion, item);
}
void Install() { RabbitPotionFeature.Initialize(); Assert(RabbitPotionFeature.Available, "Hook unavailable"); }

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
    try { controller.RunDrink(); throw new Exception("Expected effect error"); } catch (InvalidOperationException) { }
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
Check("stock ids one and thirty seven are eligible while empty item is native", () =>
{
    SessionSettings.RabbitPotionsForUse = new(true, false); Install();
    foreach (int id in new[] { 1, 37 })
    {
        var (_, controller, _, item) = Setup(id); controller.RunDrink();
        Assert(item.Quantity == 3, "Stock id excluded: " + id);
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
Console.WriteLine($"{passed} passed, {failed} failed");
Environment.ExitCode = failed == 0 ? 0 : 1;

class ThrowingRegeneration : PotionEffect_Regeneration
{
    public override void CreateEffect_OnDrink(UnitAvatar avatar) => throw new InvalidOperationException("effect failed");
}
class OtherEffect : PotionEffect { }
class ErrorAvatar : PlayerAvatar { public override void HealPercent(float strength) => throw new InvalidOperationException("recipient failed"); }
