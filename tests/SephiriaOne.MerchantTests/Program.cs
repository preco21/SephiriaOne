using System.Reflection;
using Mirror;
using SephiriaOne;
using UnityEngine;

Type runtime = Assembly.GetExecutingAssembly().GetType("SephiriaOne.MerchantRuntime");
if (runtime == null) throw new Exception("MerchantRuntime is missing; merchant spawn and ownership behavior is not implemented.");
Type hooks = Assembly.GetExecutingAssembly().GetType("SephiriaOne.MerchantNativeHooks");
if (hooks == null) throw new Exception("MerchantNativeHooks is missing; actor-specific penalty exemption is not implemented.");

int checks = 0, scenarios = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Reset()
{
    NetworkServer.FailDestroy = null;
    MerchantNativeHooks.Uninstall(); MerchantRuntime.Clear();
    NetworkServer.active = true; NetworkServer.SpawnCalls = NetworkServer.FailSpawnAt = 0; NetworkServer.OnSpawn = null;
    NetworkServer.Spawned.Clear(); NetworkServer.Destroyed.Clear(); NetworkServer.connections.Clear(); NetworkServer.connections[1] = new();
    FloorGenerator.FloorGenerators.Clear(); Safe.All.Clear(); FixtureWorld.Created.Clear();
    FixtureWorld.FailSocialAfterStock = false; FixtureWorld.BeforeInstantiate = null; Debug.Warnings.Clear();
    SaveManager.CurrentRun = new(); DungeonManager.Instance = new(); RuntimeFactionManager.Instance = new();
    SessionSettings.MerchantSpawnsForUse = true; SessionSettings.MerchantSpawnChanceForUse = 100; MerchantFeature.Available = true;
    var actorPrefab = new GameObject("Papa");
    actorPrefab.AddComponent<Unit_BabaMerchantHard>(); actorPrefab.AddComponent<UnitAI_NewBasic>();
    var safePrefab = new GameObject("Stock"); safePrefab.AddComponent<Safe>();
    SocialIDDatabase.Template = new SocialIDEntity { avatarPrefab = actorPrefab };
    PropDatabase.Stock = new PropEntity { propPrefab = safePrefab };
}
void Scenario(string name, Action test)
{
    Reset();
    try { test(); scenarios++; }
    catch (Exception error) { throw new Exception(name + ": " + error.Message, error); }
}
FloorGenerator Floor(string guid = "floor-a", float x = 0)
{
    var floor = new GameObject(guid).AddComponent<FloorGenerator>();
    floor.guid = guid; floor.SpawnPosition = new Vector2(x, 0); floor.DataOnServer = new FloorData { guid = guid };
    FloorGenerator.FloorGenerators.Add(floor); DungeonManager.Instance.generatedFloors[guid] = floor.DataOnServer;
    return floor;
}
UnitAI_NewBasic[] Actors() => NetworkServer.Spawned.Where(value => value).Select(value => value.GetComponent<UnitAI_NewBasic>()).Where(value => value).ToArray();
UnitAI_NewBasic Spawn(FloorGenerator floor)
{ MerchantRuntime.OnFloorReady(floor.guid, "Dungeon", floor); return Actors().Last(); }
UnitAI_NewBasic Natural(float x = 500)
{
    var actor = SocialIDDatabase.Template.avatarPrefab.Clone(); actor.transform.position = new Vector3(x, 0);
    NetworkServer.Spawn(actor); return actor.GetComponent<UnitAI_NewBasic>();
}
UnitAvatar Player() { var actor = new GameObject("Player").AddComponent<UnitAvatar>(); actor.faction = "Player"; return actor; }

Scenario("HP stays at the complete native scaling result", () =>
{
    NetworkServer.connections[2] = new(); NetworkServer.connections[3] = new();
    var added = Spawn(Floor());
    Check(added.Avatar.MaxHp == 8250 && added.Avatar.maxHp == 2500,
        "Expected native 8250 final HP and unchanged 2500 base HP, got " + added.Avatar.MaxHp);
    Check(added.Avatar.Healed == 100 && added.Avatar.Stats[ECustomStat.AllDamageBonus] == 35 &&
        added.Avatar.Stats[ECustomStat.DamageReduction] == 6, "Native healing, attack, and defense scaling are retained");
    Check(SocialIDDatabase.Template.avatarPrefab.GetComponent<UnitAvatar>().MaxHp == 2500, "Prefab HP stays unchanged");
});
Scenario("disabled policy and authority gates", () =>
{
    var floor = Floor(); SessionSettings.MerchantSpawnsForUse = false; MerchantRuntime.Refresh();
    Check(Actors().Length == 0 && SaveManager.CurrentRun.Flags.Count == 0, "Off never rolls or spawns");
    SessionSettings.MerchantSpawnsForUse = true; NetworkServer.active = false; MerchantRuntime.Refresh();
    Check(Actors().Length == 0, "Guests cannot spawn");
    NetworkServer.active = true; MerchantFeature.Available = false; MerchantRuntime.Refresh();
    Check(Actors().Length == 0, "Unavailable compatibility prevents spawn");
    MerchantFeature.Available = true; MerchantRuntime.Refresh(); Check(Actors().Length == 1, "A ready host can spawn after gates recover");
});
Scenario("callbacks, refresh, rejoin, and toggle changes cannot duplicate", () =>
{
    var floor = Floor(); var added = Spawn(floor); var stock = added.NetworkMySafe;
    MerchantRuntime.Refresh(); MerchantRuntime.OnFloorReady(floor.guid, "Dungeon", floor);
    SessionSettings.MerchantSpawnsForUse = false; MerchantRuntime.Refresh();
    Check(MerchantRuntime.Owns(added) && added && stock, "Off preserves the actor and its penalty exemption");
    SessionSettings.MerchantSpawnsForUse = true; NetworkServer.connections[2] = new(); MerchantRuntime.Refresh();
    Check(Actors().Length == 1 && stock.StockGenerations == 1 && floor.floorRelatedNetworkObjects.Count == 2,
        "Repeated floor events and joining guests never duplicate actors or loot");
});
Scenario("hostile encounter uses only instance settings", () =>
{
    var natural = Natural(); var added = Spawn(Floor());
    Check(added.Avatar.faction == "Undead" && added.Avatar.attackableTargetSelector == EPersonality.Aggressive &&
        !added.CanTalk && added.Role == "", "Actor is immediately hostile with no dialogue role");
    Check(added.SocialName != natural.SocialName && added.NetworkMySafe.NetworkconnectedMerchant == added,
        "Added actor has unique identity and its own stock");
    Check(natural.Avatar.faction == "Merchant" && natural.Avatar.MaxHp == 2500 && natural.CanTalk && !MerchantRuntime.Owns(natural),
        "Natural actor state stays unchanged");
    Check(RuntimeFactionManager.Instance.GlobalWrites == 0 && RuntimeFactionManager.Instance.tempDynamicEnemies.Count == 0 &&
        RuntimeFactionManager.Instance.relationValues["Merchant_Player"] == 0, "Spawn never writes global faction relations");
});
Scenario("first encounter is guaranteed at zero percent and misses never reroll", () =>
{
    SessionSettings.MerchantSpawnChanceForUse = 0;
    Spawn(Floor()); var second = Floor("floor-b", 50); MerchantRuntime.Refresh();
    Check(Actors().Length == 1, "First eligible floor is guaranteed, next floor respects zero chance");
    SessionSettings.MerchantSpawnChanceForUse = 100; MerchantRuntime.Refresh();
    MerchantRuntime.OnFloorReady(second.guid, "Dungeon", second);
    Check(Actors().Length == 1, "Increasing chance does not reroll a previously missed floor");
    Spawn(Floor("floor-c", 100)); Check(Actors().Length == 2, "A fresh later floor respects 100 percent chance");
});
Scenario("recreated floor and preserved save markers prevent duplicates", () =>
{
    var floor = Floor(); var added = Spawn(floor); NetworkServer.Destroy(added.gameObject); NetworkServer.Destroy(added.NetworkMySafe.gameObject);
    FloorGenerator.FloorGenerators.Remove(floor); UnityEngine.Object.Destroy(floor.gameObject);
    Floor("floor-a", 50); MerchantRuntime.Refresh(); Check(Actors().Length == 0, "Same run and GUID does not spawn after floor reallocation");
    MerchantRuntime.Clear(); MerchantRuntime.Refresh(); Check(Actors().Length == 0, "Runtime clear does not erase the run's completed floor marker");
});
Scenario("each fresh run gets a new guaranteed encounter", () =>
{
    SessionSettings.MerchantSpawnChanceForUse = 0;
    var old = Spawn(Floor());
    SaveManager.CurrentRun = new(); MerchantRuntime.Refresh();
    Check(!old && Actors().Length == 1 && !ReferenceEquals(old, Actors()[0]), "New run identity retires old actors and guarantees a new one");
});
foreach (string excluded in new[] { "not-server", "not-ready", "safe", "training", "hidden", "pocket", "no-room", "no-data", "stale-data", "unknown-guid" })
Scenario("excluded floor: " + excluded, () =>
{
    var floor = Floor();
    switch (excluded)
    {
        case "not-server": floor.isServer = false; break;
        case "not-ready": floor.GenerateSuccess = false; break;
        case "safe": floor.isSafeFloor = true; break;
        case "training": floor.isTrainingFloor = true; break;
        case "hidden": floor.DataOnServer.isHidden = true; break;
        case "pocket": floor.DataOnServer.pocketDimension = true; break;
        case "no-room": floor.EligibleRoom = false; break;
        case "no-data": floor.DataOnServer = null; break;
        case "stale-data": DungeonManager.Instance.generatedFloors[floor.guid] = new FloorData(); break;
        case "unknown-guid": DungeonManager.Instance.generatedFloors.Clear(); break;
    }
    MerchantRuntime.Refresh(); Check(Actors().Length == 0 && SaveManager.CurrentRun.Flags.Count == 0, "Excluded floor does not consume the guarantee or a roll");
});
Scenario("preexisting safe and its merchant are never claimed", () =>
{
    var natural = Natural(0); var stock = new GameObject("Natural stock").AddComponent<Safe>();
    stock.NetworkconnectedMerchant = natural; stock.StockGenerations = 7; Safe.All.Add(stock); Floor(); MerchantRuntime.Refresh();
    Check(Actors().Length == 1 && stock.NetworkconnectedMerchant == natural && stock.StockGenerations == 7,
        "Near a natural safe the extra spawn is skipped without rebinding stock");
    MerchantRuntime.Clear(); Check(natural && stock, "Cleanup does not destroy natural merchants or safes");
});
Scenario("known hostile faction fallback and absence", () =>
{
    RuntimeFactionManager.Instance.NonHostile.Add("Undead");
    var added = Spawn(Floor()); Check(added.Avatar.faction == "Pillagers", "Falls back to a known hostile native faction");
    RuntimeFactionManager.Instance.NonHostile.Add("Pillagers"); Floor("floor-b", 50); MerchantRuntime.Refresh();
    Check(Actors().Length == 1 && RuntimeFactionManager.Instance.GlobalWrites == 0, "Missing hostile faction skips instead of editing global relations");
});
foreach (int failedSpawn in new[] { 1, 2 })
Scenario("partial network spawn cleanup: " + failedSpawn, () =>
{
    var floor = Floor(); NetworkServer.FailSpawnAt = failedSpawn; MerchantRuntime.Refresh();
    Check(Actors().Length == 0 && FixtureWorld.Created.All(value => !value) && floor.floorRelatedNetworkObjects.Count == 0,
        "Partial actor or stock spawn is fully removed");
    NetworkServer.FailSpawnAt = 0; MerchantRuntime.Refresh(); Check(Actors().Length == 0, "A failed floor never retries and duplicates stock");
    SessionSettings.MerchantSpawnChanceForUse = 0; Spawn(Floor("next-floor", 50));
    Check(Actors().Length == 1, "A failed spawn does not consume the run's guarantee");
});
Scenario("native initialization failure cleans both owned objects", () =>
{
    var floor = Floor(); FixtureWorld.FailSocialAfterStock = true; MerchantRuntime.Refresh();
    Check(FixtureWorld.Created.Count == 2 && FixtureWorld.Created.All(value => !value) && floor.floorRelatedNetworkObjects.Count == 0,
        "Native social initialization failure leaves no actor, safe, or floor reference");
});
foreach (string invalid in new[] { "cursed", "zero", "negative", "nan", "infinity" })
Scenario("invalid HP fails safely: " + invalid, () =>
{
    var natural = Natural(); var template = SocialIDDatabase.Template.avatarPrefab.GetComponent<UnitAvatar>();
    if (invalid == "cursed") template.isHPCursed = 1;
    else template.maxHp = invalid == "zero" ? 0 : invalid == "negative" ? -5 : invalid == "nan" ? float.NaN : float.PositiveInfinity;
    var floor = Floor(); MerchantRuntime.Refresh();
    Check(Actors().Length == 1 && Actors()[0] == natural && FixtureWorld.Created.All(value => !value) && floor.floorRelatedNetworkObjects.Count == 0,
        "Invalid health leaves natural merchant unchanged and removes attempted actor");
    template.maxHp = 2500; template.isHPCursed = 0; SessionSettings.MerchantSpawnChanceForUse = 0;
    Spawn(Floor("next-floor", 50)); Check(Actors().Length == 2, "Failed invalid-HP attempt leaves the guarantee for the next eligible floor");
});
Scenario("reentrant callbacks cannot duplicate a reserved floor", () =>
{
    var floor = Floor(); FixtureWorld.BeforeInstantiate = () => MerchantRuntime.OnFloorReady(floor.guid, "Dungeon", floor);
    MerchantRuntime.Refresh(); Check(Actors().Length == 1 && FixtureWorld.Created.Count == 2, "Reentrant callback observes the in-progress refresh guard");
});
Scenario("actor-specific crime and damage hooks preserve natural behavior", () =>
{
    var added = Spawn(Floor()); var natural = Natural(); var attacker = Player(); var damage = new DamageInstance { origin = attacker };
    MerchantNativeHooks.Install(); Check(MerchantNativeHooks.Available, "Native-shaped hook fixtures satisfy contract validation");
    added.RunDeath(damage); Check(attacker.Buffs == 0 && DungeonManager.Instance.CrimeCalls == 0, "Owned merchant death never enters global crime path");
    natural.RunDeath(damage); Check(attacker.Buffs == 1 && DungeonManager.Instance.CrimeCalls == 1, "Natural merchant retains the native crime callback");
    added.RunDamage(damage);
    Check(added.CurrentTarget == attacker && RuntimeFactionManager.Instance.GlobalWrites == 0, "Owned actor targets a hostile attacker without global writes");
    SessionSettings.MerchantSpawnsForUse = false; added.RunDeath(damage);
    Check(attacker.Buffs == 1, "Disabling spawns preserves existing actor's crime exemption");
    var friendly = Natural(700).Avatar; added.CurrentTarget = null; added.RunDamage(new DamageInstance { origin = friendly });
    Check(added.CurrentTarget == null && RuntimeFactionManager.Instance.GlobalWrites == 0, "Nonhostile attacker does not become a global enemy");
    RuntimeFactionManager.Instance.ThrowRelation = true; added.RunDamage(damage); RuntimeFactionManager.Instance.ThrowRelation = false;
    Check(RuntimeFactionManager.Instance.GlobalWrites == 0, "Relation lookup failure still suppresses the owned actor's global-write path");
    natural.RunDamage(damage); Check(RuntimeFactionManager.Instance.GlobalWrites == 1, "Natural actor retains native damage response");
});
Scenario("teardown retires owned actors and stock without kills", () =>
{
    var floor = Floor(); var added = Spawn(floor); var stock = added.NetworkMySafe; var natural = Natural();
    MerchantRuntime.Clear(); MerchantRuntime.Clear();
    Check(!added && !stock && natural && !MerchantRuntime.Owns(added) && floor.floorRelatedNetworkObjects.Count == 0,
        "Repeated cleanup removes only owned actor, safe, and floor references");
    Check(added.Avatar.DeathCalls == 0, "Cleanup never uses a kill/loot-producing death method");
});
Scenario("failed network destruction preserves ownership and native cleanup references", () =>
{
    var floor = Floor(); var added = Spawn(floor); NetworkServer.FailDestroy = added.gameObject;
    try { MerchantRuntime.Clear(); } catch (InvalidOperationException) { }
    Check(added && MerchantRuntime.Owns(added) && floor.floorRelatedNetworkObjects.Contains(added.gameObject),
        "A failed destroy must retain the live actor's ownership and native floor cleanup reference");
    NetworkServer.FailDestroy = null; MerchantRuntime.Clear();
    Check(!added && Actors().Length == 0 && floor.floorRelatedNetworkObjects.Count == 0,
        "A later clear retries retained objects and removes references only after successful destruction");
});

Reset();
Console.WriteLine($"Passed {checks} merchant native checks across {scenarios} scenarios.");
