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
    SessionSettings.Variants.Clear(); SessionSettings.FirstFloor = 1; SessionSettings.MaxPerRun = 0; SessionSettings.Guarantee = true;
    var actorPrefab = new GameObject("Papa");
    actorPrefab.AddComponent<Unit_BabaMerchantHard>(); actorPrefab.AddComponent<UnitAI_NewBasic>();
    var safePrefab = new GameObject("Stock"); safePrefab.AddComponent<Safe>();
    SocialIDDatabase.Template = new SocialIDEntity { avatarPrefab = actorPrefab };
    SocialIDDatabase.Variants.Clear();
    var papyrus = new GameObject("Papyrus"); papyrus.AddComponent<Unit_Soldier>(); papyrus.AddComponent<UnitAI_NewBasic>();
    var taz = new GameObject("Taz"); taz.AddComponent<Unit_TurtlePotion>(); taz.AddComponent<UnitAI_NewBasic>();
    SocialIDDatabase.Variants["Traveler_Merchant_Papyrus"] = new SocialIDEntity { avatarPrefab = papyrus };
    SocialIDDatabase.Variants["Traveler_Merchant_Taz"] = new SocialIDEntity { avatarPrefab = taz };
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
Scenario("a saved future guarantee does not force the first floor", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetInt("SephiriaOne.MerchantSchedule.v1.Target", 2);
    SessionSettings.MerchantSpawnChanceForUse = 0;
    var first = Floor(); MerchantRuntime.OnFloorReady(first.guid, "Dungeon", first);
    Check(Actors().Length == 0, "Zero chance must leave the first floor empty when the guarantee is scheduled later");
    var last = Floor("last", 100); last.DataOnServer.Progress = 2;
    Spawn(last); Check(Actors().Length == 1, "Selected later floor must receive the guarantee");
});
Scenario("a one-floor route guarantees once and misses never reroll", () =>
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

const string TargetKey = "SephiriaOne.MerchantSchedule.v1.Target";
const string EncounterKey = "SephiriaOne.MerchantEncounter";
FloorGenerator At(int progress)
{
    var floor = Floor("progress-" + progress, progress * 50);
    floor.DataOnServer.Progress = progress;
    return floor;
}
void Visit(FloorGenerator floor) => MerchantRuntime.OnFloorReady(floor.guid, "Dungeon", floor);
SaveData CopyRun()
{
    var copy = new SaveData();
    foreach (var pair in SaveManager.CurrentRun.Flags) copy.SetBool(pair.Key, pair.Value);
    foreach (var pair in SaveManager.CurrentRun.Ints) copy.SetInt(pair.Key, pair.Value);
    return copy;
}

Scenario("fresh run selection covers first, middle and last floors", () =>
{
    var selected = new HashSet<int>();
    for (int seed = 0; seed < 90; seed++)
    {
        Reset(); DungeonManager.Instance.DestinySeed = seed;
        DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
        SessionSettings.MerchantSpawnChanceForUse = 0;
        Visit(At(0));
        int target = SaveManager.CurrentRun.GetInt(TargetKey, -1);
        selected.Add(target);
        Check(target >= 0 && target <= 2, "Every fresh run selects a valid position");
        Check(Actors().Length == (target == 0 ? 1 : 0), "Only the selected floor is forced");
        Visit(At(1)); Visit(At(2));
        Check(Actors().Length == 1 && Actors()[0].SocialName.EndsWith("progress-" + target),
            "Zero chance produces exactly one merchant on the selected floor");
    }
    Check(selected.SetEquals(new[] { 0, 1, 2 }), "First, middle and final floors must all be selectable across runs");
});
Scenario("chance extras before and after the guarantee are independent", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SaveManager.CurrentRun.SetInt(TargetKey, 2);
    Visit(At(0));
    Check(Actors().Length == 1 && !SaveManager.CurrentRun.GetBool(EncounterKey),
        "An early 100-percent chance encounter must not fulfill the guarantee");
    SessionSettings.MerchantSpawnChanceForUse = 0; Visit(At(1));
    Check(Actors().Length == 1, "A chance miss before the guarantee remains a miss");
    Visit(At(2));
    Check(Actors().Length == 2 && SaveManager.CurrentRun.GetBool(EncounterKey), "Scheduled floor is still guaranteed");
    SessionSettings.MerchantSpawnChanceForUse = 100; Visit(At(3)); MerchantRuntime.Refresh();
    Check(Actors().Length == 3, "Chance extras work after the guarantee without duplicate refresh spawns");
});
Scenario("100-percent chance never doubles a guaranteed floor", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetInt(TargetKey, 1);
    Visit(At(0)); Visit(At(1)); Visit(At(2));
    Check(Actors().Length == 3, "Guaranteed and chance paths share the one-merchant-per-floor reservation");
});
Scenario("saved pending selection survives a chance spawn and reload", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetInt(TargetKey, 2); Visit(At(0));
    SaveManager.CurrentRun = CopyRun(); MerchantRuntime.Refresh();
    Check(SaveManager.CurrentRun.GetInt(TargetKey, -1) == 2 && !SaveManager.CurrentRun.GetBool(EncounterKey),
        "Reload preserves selection and distinguishes a chance extra from the guarantee");
    SessionSettings.MerchantSpawnChanceForUse = 0; Visit(At(1)); Visit(At(2));
    Check(Actors().Length == 1 && Actors()[0].SocialName.EndsWith("progress-2"), "Saved later guarantee still spawns");
    SaveManager.CurrentRun = CopyRun(); MerchantRuntime.Refresh();
    Check(Actors().Length == 0 && SaveManager.CurrentRun.GetBool(EncounterKey), "Completed saved guarantee never replenishes");
});
Scenario("late enable schedules current and future floors only regardless of list order", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SessionSettings.MerchantSpawnsForUse = false; SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0)); Visit(At(1)); Visit(At(2));
    FloorGenerator.FloorGenerators.Reverse();
    SessionSettings.MerchantSpawnsForUse = true; MerchantRuntime.Refresh();
    int selected = SaveManager.CurrentRun.GetInt(TargetKey, -1);
    Check(selected >= 2 && selected <= 3, "Late enable excludes passed floors even when loaded list order is reversed");
    Visit(At(3));
    Check(Actors().Length == 1 && Actors()[0].SocialName.EndsWith("progress-" + selected), "Late selection is reachable");
});
Scenario("toggle reset and rejoin never reroll a pending target", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetInt(TargetKey, 1); SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0)); SessionSettings.MerchantSpawnsForUse = false; Visit(At(1));
    NetworkServer.connections[2] = new(); NetworkServer.connections.Remove(2);
    NetworkServer.connections[2] = new(); SessionSettings.MerchantSpawnsForUse = true;
    Visit(At(2)); MerchantRuntime.Refresh();
    Check(SaveManager.CurrentRun.GetInt(TargetKey, -1) == 1 && Actors().Length == 1 &&
        Actors()[0].SocialName.EndsWith("progress-2"), "A target passed while off carries forward without rerolling");
});
Scenario("unsafe selected floor and failed fallback retain the guarantee", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SaveManager.CurrentRun.SetInt(TargetKey, 1); SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0)); var selected = At(1); selected.EligibleRoom = false; Visit(selected);
    Check(!SaveManager.CurrentRun.GetBool(EncounterKey) && Actors().Length == 0, "Unsafe selected room keeps guarantee pending");
    NetworkServer.FailSpawnAt = 2; Visit(At(2));
    Check(!SaveManager.CurrentRun.GetBool(EncounterKey) && Actors().Length == 0 && FixtureWorld.Created.All(value => !value),
        "Failed fallback cleans partial state without fulfilling the guarantee");
    NetworkServer.FailSpawnAt = 0; MerchantRuntime.Refresh();
    Check(Actors().Length == 0, "Failed reserved floor never retries");
    Visit(At(3)); Check(Actors().Length == 1, "Next safe floor fulfills the pending guarantee");
});
Scenario("older loaded floors cannot fulfill a missed selected floor", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetInt(TargetKey, 1); SessionSettings.MerchantSpawnChanceForUse = 0;
    var first = At(0); var middle = At(1); middle.EligibleRoom = false; Visit(middle);
    var last = At(2); last.EligibleRoom = false; Visit(last);
    middle.EligibleRoom = true; MerchantRuntime.Refresh(); Visit(first); Visit(middle);
    Check(Actors().Length == 0 && !SaveManager.CurrentRun.GetBool(EncounterKey),
        "Going backward or reordering callbacks never forces an encounter into a passed floor");
    last.EligibleRoom = true; Visit(last); Check(Actors().Length == 1, "Current floor may recover before consuming its reservation");
});
Scenario("legacy fulfilled marker stays fulfilled and legacy misses stay consumed", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SessionSettings.MerchantSpawnChanceForUse = 0; SaveManager.CurrentRun.SetBool(EncounterKey, true);
    Visit(At(0)); Visit(At(1)); Visit(At(2));
    Check(Actors().Length == 0, "An old successful encounter does not gain a second guarantee after upgrade");
    Reset(); DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetBool("SephiriaOne.MerchantFloor.progress-0", true);
    SaveManager.CurrentRun.SetInt(TargetKey, 0); SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0)); Check(Actors().Length == 0, "An old consumed floor is never reopened");
    Visit(At(1)); Check(Actors().Length == 1, "Unfulfilled old run continues on its next safe floor");
});
Scenario("unknown route has no forced first-floor fallback", () =>
{
    DungeonManager.Instance.Opportunities = Array.Empty<int>(); SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0));
    Check(Actors().Length == 0 && SaveManager.CurrentRun.GetInt(TargetKey, -1) == -1,
        "Missing route opportunities must not silently restore a first-floor guarantee");
});
Scenario("optional normal floors retain chance extras without consuming the main-route guarantee", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    SaveManager.CurrentRun.SetInt(TargetKey, 2);
    var optional = Floor("optional-side", 500); optional.DataOnServer.Progress = -1;
    Visit(optional);
    Check(Actors().Length == 1 && !SaveManager.CurrentRun.GetBool(EncounterKey),
        "An eligible optional floor outside the finite schedule must retain its independent chance roll");
    SessionSettings.MerchantSpawnChanceForUse = 0; Visit(At(0));
    var another = Floor("other-optional", 600); another.DataOnServer.Progress = -1; Visit(another);
    Check(Actors().Length == 1 && !SaveManager.CurrentRun.GetBool(EncounterKey),
        "Zero chance cannot turn an optional floor into the scheduled guarantee");
    Visit(At(2));
    Check(Actors().Length == 2 && SaveManager.CurrentRun.GetBool(EncounterKey), "Main-route guarantee remains available afterward");
});
Scenario("early settings refresh waits for native race initialization", () =>
{
    DungeonManager.Instance.Race = null;
    MerchantRuntime.Refresh();
    Check(SaveManager.CurrentRun.Ints.Count == 0, "An incomplete native run must not establish an empty schedule");
    DungeonManager.Instance.Race = new UnityEngine.Object(); SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0));
    Check(Actors().Length == 1, "A later ready route must recover automatically after an early settings refresh");
});

Scenario("different merchant types coexist with isolated stock on one floor", () =>
{
    SessionSettings.Variants["papyrus"] = new(true, 100); SessionSettings.Variants["taz"] = new(true, 100);
    Visit(At(0)); MerchantRuntime.Refresh();
    Check(Actors().Length == 3, "Each eligible enabled type must independently spawn on the same floor");
    Check(Actors().Select(actor => actor.NetworkMySafe).Distinct().Count() == 3 &&
        Actors().All(actor => actor.NetworkMySafe.NetworkconnectedMerchant == actor), "Each type owns a different native stock container");
    Check(Actors().Select(actor => actor.SocialName).Distinct().Count() == 3 &&
        Actors().Select(actor => actor.Avatar.RandomID).Distinct().Count() == 3, "Same-floor types have distinct social and loot RNG identities");
});
string VariantKey(string id, string key) => "SephiriaOne.MerchantVariant.v1." + id + "." + key;
void EnableVariants(int chance = 0, int first = 1, int limit = 0)
{
    SessionSettings.MerchantSpawnChanceForUse = chance; SessionSettings.FirstFloor = first; SessionSettings.MaxPerRun = limit;
    SessionSettings.Variants["papyrus"] = new(true, chance, first, limit);
    SessionSettings.Variants["taz"] = new(true, chance, first, limit);
}
Scenario("each type has its own zero-chance guarantee and persistent target", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 }; EnableVariants();
    SaveManager.CurrentRun.SetInt(TargetKey, 2);
    SaveManager.CurrentRun.SetInt(VariantKey("papyrus", "Schedule.Target"), 1);
    SaveManager.CurrentRun.SetInt(VariantKey("taz", "Schedule.Target"), 3);
    Visit(At(0)); Check(Actors().Length == 0, "First floor is not forced for any later scheduled type");
    Visit(At(1)); Check(Actors().Length == 1 && Actors()[0].Avatar is Unit_Soldier, "Papyrus receives its own first target");
    SaveManager.CurrentRun = CopyRun(); MerchantRuntime.Refresh();
    Visit(At(2)); Visit(At(3));
    Check(Actors().Length == 2 && Actors().Any(a => a.Avatar is Unit_TurtlePotion) && Actors().Any(a => a.Avatar is Unit_BabaMerchantHard),
        "Reload retains distinct pending targets and never restores Papyrus's consumed guarantee");
    foreach (string id in new[] { "papyrus", "taz" })
        Check(SaveManager.CurrentRun.GetBool(VariantKey(id, "Encounter")), "Each type completes only its own guarantee: " + id);
});
Scenario("all variants draw stable independently salted guaranteed positions", () =>
{
    int different = 0;
    for (int seed = 0; seed < 30; seed++)
    {
        Reset(); DungeonManager.Instance.DestinySeed = seed; DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
        EnableVariants(); Visit(At(0));
        var targets = new[] { SaveManager.CurrentRun.GetInt(TargetKey, -1),
            SaveManager.CurrentRun.GetInt(VariantKey("papyrus", "Schedule.Target"), -1),
            SaveManager.CurrentRun.GetInt(VariantKey("taz", "Schedule.Target"), -1) };
        if (targets.Distinct().Count() > 1) different++;
        Check(targets.All(value => value >= 0 && value < 4), "Each type selects a valid target");
        Visit(At(1)); Visit(At(2)); Visit(At(3));
        Check(Actors().Length == 3 && Actors().Select(actor => actor.Avatar.GetType()).Distinct().Count() == 3,
            "Each enabled type produces exactly one zero-chance guarantee over the full route");
    }
    Check(different > 0, "Type seeds must not correlate every guarantee to the same floor");
});
Scenario("earliest floor settings independently filter guaranteed positions", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 }; EnableVariants(0);
    SessionSettings.Variants["papyrus"] = new(true, 100, 3, 1);
    SessionSettings.Variants["taz"] = new(true, 100, 4, 1);
    Visit(At(0)); Visit(At(1));
    Check(Actors().All(actor => actor.Avatar is Unit_BabaMerchantHard), "New types cannot spawn before their own minimums");
    Check(SaveManager.CurrentRun.GetInt(VariantKey("papyrus", "Schedule.Target"), -1) >= 2 &&
        SaveManager.CurrentRun.GetInt(VariantKey("taz", "Schedule.Target"), -1) == 3, "Guarantee selection respects each type's minimum");
    Visit(At(2)); Visit(At(3)); Check(Actors().Length == 3, "Both late types retain their own guarantees");
});
Scenario("per-run caps reserve the scheduled guarantee without suppressing other types", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    EnableVariants(100); SessionSettings.MaxPerRun = 1;
    SessionSettings.Variants["papyrus"] = new(true, 100, 1, 2);
    SaveManager.CurrentRun.SetInt(TargetKey, 2);
    SaveManager.CurrentRun.SetInt(VariantKey("papyrus", "Schedule.Target"), 2);
    SaveManager.CurrentRun.SetInt(VariantKey("taz", "Schedule.Target"), 2);
    Visit(At(0)); Visit(At(1));
    Check(!Actors().Any(actor => actor.Avatar is Unit_BabaMerchantHard) && Actors().Count(actor => actor.Avatar is Unit_Soldier) == 1,
        "Chance rolls leave one cap slot for each pending guarantee");
    Visit(At(2)); Visit(At(3));
    Check(Actors().Count(actor => actor.Avatar is Unit_BabaMerchantHard) == 1 &&
        Actors().Count(actor => actor.Avatar is Unit_Soldier) == 2 && Actors().Count(actor => actor.Avatar is Unit_TurtlePotion) == 4,
        "Independent caps count both chance and guarantee; unlimited type continues each floor");
    SaveManager.CurrentRun = CopyRun(); MerchantRuntime.Refresh();
    Visit(At(4));
    Check(Actors().Length == 1 && Actors()[0].Avatar is Unit_TurtlePotion, "Saved caps survive runtime replacement");
});
Scenario("minimum changes retain a saved target while carrying it forward", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SessionSettings.MerchantSpawnsForUse = false; SessionSettings.Variants["papyrus"] = new(true, 0);
    string key = VariantKey("papyrus", "Schedule.Target"); SaveManager.CurrentRun.SetInt(key, 1);
    Visit(At(0)); SessionSettings.Variants["papyrus"] = new(true, 0, 4);
    Visit(At(1)); Visit(At(2)); Check(Actors().Length == 0, "Increased earliest floor applies to a pending guarantee");
    Visit(At(3)); Check(Actors().Length == 1 && SaveManager.CurrentRun.GetInt(key, -1) == 1,
        "Target is not rerolled and fulfills at the next floor allowed by current settings");
});
Scenario("a newly enabled type ignores another type's consumed legacy floor", () =>
{
    Visit(At(0)); SessionSettings.Variants["papyrus"] = new(true, 0);
    MerchantRuntime.Refresh(); Check(Actors().Length == 2, "Legacy Wandering reservation cannot block a new type on the same floor");
    SessionSettings.Variants["papyrus"] = new(false, 0); MerchantRuntime.Refresh();
    NetworkServer.connections[2] = new(); NetworkServer.connections.Remove(2); NetworkServer.connections[2] = new();
    SessionSettings.Variants["papyrus"] = new(true, 100); MerchantRuntime.Refresh();
    Check(Actors().Length == 2, "Re-entry, reset/toggle and chance changes do not reroll per-type reservations");
});
Scenario("invalid native template and partial initialization fail independently", () =>
{
    EnableVariants(100);
    SocialIDDatabase.Variants["Traveler_Merchant_Papyrus"].avatarPrefab = SocialIDDatabase.Template.avatarPrefab;
    Visit(At(0));
    Check(Actors().Length == 2 && Actors().All(actor => actor.Avatar is not Unit_Soldier),
        "Mismatched Papyrus controller fails closed without blocking other types");
    Reset(); EnableVariants(100);
    NetworkServer.OnSpawn = obj => { if (obj.GetComponent<UnitAvatar>() is UnitAvatar avatar) FixtureWorld.FailSocialAfterStock = avatar is Unit_Soldier; };
    var floor = At(0); Visit(floor);
    Check(Actors().Length == 2 && floor.floorRelatedNetworkObjects.Count == 4 && Safe.All.Count(safe => safe) == 2,
        "A failed Papyrus social initialization rolls back only its actor and stock");
    Check(!SaveManager.CurrentRun.GetBool(VariantKey("papyrus", "Encounter")) &&
        SaveManager.CurrentRun.GetBool(VariantKey("taz", "Encounter")), "Failure never consumes another type's guarantee");
    NetworkServer.OnSpawn = null; FixtureWorld.FailSocialAfterStock = false; EnableVariants(0);
    Visit(At(1)); Check(Actors().Count(actor => actor.Avatar is Unit_Soldier) == 1, "Failed type carries its guarantee to the next safe floor");
});
Scenario("each variant's crime exemption and teardown stay instance scoped", () =>
{
    EnableVariants(100); Visit(At(0)); MerchantNativeHooks.Install();
    var attacker = Player(); var damage = new DamageInstance { origin = attacker };
    foreach (var actor in Actors()) { actor.RunDamage(damage); actor.RunDeath(damage); }
    Check(attacker.Buffs == 0 && DungeonManager.Instance.CrimeCalls == 0 && RuntimeFactionManager.Instance.GlobalWrites == 0,
        "All addon types use exact-instance crime and faction isolation");
    var natural = Natural(); natural.RunDeath(damage);
    Check(attacker.Buffs == 1, "A natural merchant still invokes native crime");
    MerchantRuntime.Clear(); Check(Actors().Length == 1 && Actors()[0] == natural && Safe.All.All(safe => !safe),
        "Cleanup removes every addon type's stock and actor, never the natural merchant");
});
Scenario("each new run resets all independent guarantees and caps", () =>
{
    EnableVariants(0, 1, 1); Visit(At(0)); var previous = Actors();
    SaveManager.CurrentRun = new(); MerchantRuntime.Refresh();
    Check(Actors().Length == 3 && previous.All(actor => !actor), "Fresh run recreates all three guarantees despite previous caps");
});
Scenario("unsupported routes retain capped chance-only encounters", () =>
{
    DungeonManager.Instance.Opportunities = Array.Empty<int>(); EnableVariants(100, 1, 1);
    var first = At(0); first.DataOnServer.Progress = -1; Visit(first);
    Check(Actors().Length == 3, "A cap of one must not reserve an impossible guarantee and disable chance-only types");
    var second = At(1); second.DataOnServer.Progress = -1; Visit(second);
    Check(Actors().Length == 3, "Chance-only encounters still obey their successful-spawn caps");
});
Scenario("optional floor before main progression reserves still-reachable guarantees", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 }; EnableVariants(100, 1, 1);
    var optional = Floor("pre-route", 500); optional.DataOnServer.Progress = -1; Visit(optional);
    Check(Actors().Length == 0, "A reachable guarantee keeps a cap slot even before the main route establishes a saved target");
    Visit(At(0)); Visit(At(1)); Visit(At(2));
    Check(Actors().Length == 3, "Each capped type later receives its guarantee on the main route");
});
Scenario("merchant definitions compose eligibility without core-specific branches", () =>
{
    var variant = new MerchantDefinition("test", "Test", "native", "Unit", 17, true, 25,
        condition: context => context.StageName == "Library" && context.Difficulty >= 3);
    var settings = new MerchantSettings(true, 25, 2, 2);
    Check(!MerchantSpawnRules.Allows(variant, settings, new(1, 0, 3, "Library")), "Earliest floor is enforced");
    Check(!MerchantSpawnRules.Allows(variant, settings, new(2, 2, 3, "Library")), "Per-run cap is enforced");
    Check(!MerchantSpawnRules.Allows(variant, settings, new(2, 0, 2, "Library")), "Variant-specific condition is enforced");
    Check(!MerchantSpawnRules.Allows(variant, settings, new(2, 0, 3, "Desert")), "Independent region rule is composable");
    Check(MerchantSpawnRules.Allows(variant, settings, new(2, 1, 3, "Library")), "All conditions admit the variant");
    Check(!MerchantSpawnRules.Allows(variant, new(false, 25), new(2, 0, 3, "Library")), "Disabled variants never spawn");
});

Scenario("disabled guarantees do not force zero-chance encounters or select targets", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    EnableVariants(); SessionSettings.Guarantee = false;
    SessionSettings.Variants["papyrus"] = new(true, 0, guarantee: false);
    SessionSettings.Variants["taz"] = new(true, 0, guarantee: false);
    Visit(At(0)); Visit(At(1)); Visit(At(2));
    Check(Actors().Length == 0, "Zero chance with guarantee off spawns no merchants of any type");
    Check(SaveManager.CurrentRun.GetInt(TargetKey, -1) == -1 &&
        SaveManager.CurrentRun.GetInt(VariantKey("papyrus", "Schedule.Target"), -1) == -1 &&
        SaveManager.CurrentRun.GetInt(VariantKey("taz", "Schedule.Target"), -1) == -1, "No disabled guarantee creates a target");
    Check(SaveManager.CurrentRun.GetInt(VariantKey("papyrus", "Schedule.Progress"), -1) == 2,
        "Route progress still advances with guarantee off");
});
Scenario("guarantee off releases reserved cap slots while other types remain guaranteed", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2 };
    EnableVariants(100, 1, 1);
    SaveManager.CurrentRun.SetInt(TargetKey, 2);
    SaveManager.CurrentRun.SetInt(VariantKey("papyrus", "Schedule.Target"), 2);
    SaveManager.CurrentRun.SetInt(VariantKey("taz", "Schedule.Target"), 2);
    SessionSettings.Guarantee = false;
    SessionSettings.Variants["papyrus"] = new(true, 100, 1, 1, false);
    Visit(At(0));
    Check(Actors().Length == 2 && Actors().All(actor => actor.Avatar is not Unit_TurtlePotion),
        "Chance rolls may use the full cap for guarantee-off types, while Taz keeps its reserved slot");
    Check(!SaveManager.CurrentRun.GetBool(EncounterKey) && !SaveManager.CurrentRun.GetBool(VariantKey("papyrus", "Encounter")),
        "Chance-only spawns do not fulfill paused guarantees");
    SessionSettings.Guarantee = true; SessionSettings.Variants["papyrus"] = new(true, 100, 1, 1);
    Visit(At(1)); Visit(At(2));
    Check(Actors().Length == 3 && SaveManager.CurrentRun.GetBool(VariantKey("taz", "Encounter")) && !SaveManager.CurrentRun.GetBool(EncounterKey),
        "Re-enabling guarantees cannot exceed caps consumed by chance spawns");
});
Scenario("pausing a saved target preserves rolls and carries the pending guarantee forward", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SessionSettings.MerchantSpawnsForUse = false;
    SessionSettings.Variants["papyrus"] = new(true, 0);
    string target = VariantKey("papyrus", "Schedule.Target"); SaveManager.CurrentRun.SetInt(target, 1);
    Visit(At(0)); SessionSettings.Variants["papyrus"] = new(true, 0, guarantee: false); Visit(At(1));
    Check(Actors().Length == 0 && SaveManager.CurrentRun.GetInt(target, -1) == 1, "Turning off pauses the saved target without erasing it");
    SaveManager.CurrentRun = CopyRun(); MerchantRuntime.Refresh();
    SessionSettings.Variants["papyrus"] = new(true, 0); MerchantRuntime.Refresh();
    Check(Actors().Length == 0, "Re-enabling cannot reopen the target floor's already-consumed chance roll");
    Visit(At(2));
    Check(Actors().Length == 1 && Actors()[0].Avatar is Unit_Soldier && SaveManager.CurrentRun.GetInt(target, -1) == 1,
        "Pending target survives reload and fulfills on the next unused eligible floor");
    SessionSettings.Variants["papyrus"] = new(true, 0, guarantee: false); MerchantRuntime.Refresh();
    SessionSettings.Variants["papyrus"] = new(true, 0); Visit(At(3));
    NetworkServer.connections[2] = new(); NetworkServer.connections.Remove(2); NetworkServer.connections[2] = new(); MerchantRuntime.Refresh();
    Check(Actors().Length == 1, "Completed guarantees are never replenished by toggles or guest re-entry");
});
Scenario("first enabling a guarantee selects only remaining positions", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SessionSettings.Guarantee = false; SessionSettings.MerchantSpawnChanceForUse = 0;
    Visit(At(0)); Visit(At(1));
    Check(SaveManager.CurrentRun.GetInt(TargetKey, -1) == -1, "An initially disabled guarantee is not preselected");
    SessionSettings.Guarantee = true; Visit(At(2)); Visit(At(3));
    Check(SaveManager.CurrentRun.GetInt(TargetKey, -1) >= 2 && Actors().Length == 1,
        "First enabling the guarantee cannot select a passed floor");
});
Scenario("chance-only state survives reload and owned actors retain their crime exemption", () =>
{
    DungeonManager.Instance.Opportunities = new[] { 0, 1, 2, 3 };
    SessionSettings.MaxPerRun = 1; SaveManager.CurrentRun.SetInt(TargetKey, 2);
    SessionSettings.Guarantee = false; Visit(At(0)); MerchantNativeHooks.Install();
    var actor = Actors().Single(); var attacker = Player(); var damage = new DamageInstance { origin = attacker };
    actor.RunDamage(damage); actor.RunDeath(damage);
    Check(attacker.Buffs == 0 && DungeonManager.Instance.CrimeCalls == 0,
        "Chance-only added actors receive the same isolated crime exemption");
    SaveManager.CurrentRun = CopyRun(); MerchantRuntime.Refresh(); Visit(At(1));
    Check(Actors().Length == 0 && SaveManager.CurrentRun.GetInt(VariantKey("wandering", "Count"), 0) == 1,
        "Reload never replenishes a chance-only cap");
    SessionSettings.Guarantee = true; Visit(At(2)); Check(Actors().Length == 0, "A resumed guarantee respects a saved exhausted cap");
    SessionSettings.MaxPerRun = 2; SessionSettings.MerchantSpawnChanceForUse = 0; Visit(At(3));
    Check(Actors().Length == 1 && SaveManager.CurrentRun.GetBool(EncounterKey),
        "Raising the cap allows the still-pending target without rerolling it");
});

Reset();
Console.WriteLine($"Passed {checks} merchant native checks across {scenarios} scenarios.");
