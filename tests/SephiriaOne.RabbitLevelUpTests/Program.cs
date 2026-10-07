using SephiriaOne;
using Mirror;
using HarmonyLib;

int checks = 0;
void Check(bool condition, string text) { if (!condition) throw new Exception(text); checks++; }
var identities = new (int Id, string Key)[] {
    (28,"NebbiolosStubbornness"),(29,"UgniBlancsMist"),(30,"TempranillosPassion"),(31,"MalbecsDepth"),
    (33,"LargeDicePotion"),(34,"SmallDicePotion"),(35,"FinalDamagePotion"),(38,"PotionOfEnchant"),
    (39,"MushroomSoup"),(40,"PowerPotionMinor"),(41,"FlamePotionMinor"),(42,"FrostPotionMinor"),
    (43,"LightningPotionMinor"),(46,"RandomComboPotion"),(47,"DefensePotion_Big"),(48,"EvasionPotion_Big"),
    (49,"DefensePotion"),(50,"EvasionPotion"),(51,"ElementPotion") };
void LoadCatalog()
{
    ItemDatabase.Items.Clear();
    foreach (var (id,key) in identities)
    {
        var item = new ItemEntity { id = id, aName = new() { key = "Item_" + key + "_Name" } };
        item.resourcePrefab.Potion.effect = id == 33 || id == 34 ? new PotionEffect_DicePotion() :
            id == 38 ? new PotionEffect_Enchant() : id == 39 ? new PotionEffect_ElementalDamageBoost() :
            id == 46 ? new PotionEffect_RandomCombo() : new PotionEffect_StatusInstance();
        ItemDatabase.Items[id] = item;
    }
    RabbitLevelUpFeature.OnDatabasesReady();
}
LevelController NewPlayer()
{
    var p = new PlayerAvatar(); var s = new PlayerSpawner { PlayerAvatar = p }; p.spawner = s;
    var c = new NetworkConnectionToClient { identity = new() { Owner = s } };
    s.connectionToClient = p.connectionToClient = c;
    PlayerSpawner.MultiplayerList.Add(s); NetworkServer.connections[PlayerSpawner.MultiplayerList.Count] = c;
    return new LevelController { Avatar = p, connectionToClient = c };
}
void Reset()
{
    RabbitLevelUpFeature.Shutdown(); NetworkServer.active = true;
    PlayerSpawner.MultiplayerList.Clear(); NetworkServer.connections.Clear();
    SaveManager.CurrentRun = new(); DungeonManager.Instance = new(); SessionSettings.ResourceGeneration++;
    SessionSettings.RabbitPotionsForUse = new(false, false, levelUpPotion:true);
    RabbitLevelUpFeature.Initialize(); LoadCatalog();
    Check(RabbitLevelUpFeature.Available, "Fixture contract must install");
}
Reset();
var level = NewPlayer(); var inventory = level.Avatar.Inventory;
level.AddExp(30);
Check(level.NativeCalls == 3 && inventory.Items.Count == 3, "Each earned level, including batched XP, grants once");
Check(inventory.Items.All(i => identities.Any(x => x.Id == i.entityID)) && inventory.Items.Select(i=>i.instanceID).Distinct().Count()==3,
    "Only approved native potions with unique IDs");
level.Initialize(3,20); level.GenerateItem(1); level.LevelUpOnServer();
Check(inventory.Items.Count == 3, "Initialization, restored choices and standalone callbacks never replay grants");
SessionSettings.RabbitPotionsForUse = default; level.AddExp(10);
Check(inventory.Items.Count == 3, "Off leaves native levels alone");
SessionSettings.RabbitPotionsForUse = new(false,false,levelUpPotion:true);
level.Avatar.currentCostume = "Other"; level.AddExp(10);
Check(inventory.Items.Count == 3, "Other costumes excluded");
Reset(); level=NewPlayer(); level.Avatar.Inventory.Reject=true; level.AddExp(10);
Check(level.Avatar.Inventory.Grants==1 && level.Avatar.Inventory.temporaryInventory.Count==1 && level.Avatar.Inventory.Permissions==0,
    "Explicit capacity rejection gets one native temporary item under balanced permission");
Reset(); level=NewPlayer(); inventory=level.Avatar.Inventory; inventory.ThrowAfterWrite=true; level.AddExp(30);
Check(level.NativeCalls==3 && inventory.Items.Count==1 && inventory.temporaryInventory.Count==0 && !RabbitLevelUpFeature.Available,
    "Ambiguous native write fails closed, never retries or duplicates, native XP continues");
foreach (string transition in new[]{"costume","dead","run","generation","dungeon","floor","inventory","connection","unready","removed","disabled","avatar"})
{
    Reset(); level=NewPlayer(); inventory=level.Avatar.Inventory;
    Action change = transition switch {
        "costume" => () => level.Avatar.currentCostume="Other", "dead" => () => level.Avatar.IsDead=true,
        "run" => () => SaveManager.CurrentRun=new(), "generation" => () => SessionSettings.ResourceGeneration++,
        "dungeon" => () => DungeonManager.Instance=new(), "floor" => () => level.Avatar.currentFloorGuid="next",
        "inventory" => () => level.Avatar.Inventory=new(), "connection" => () => level.Avatar.spawner.connectionToClient=new(),
        "unready" => () => level.connectionToClient.isReady=false, "removed" => () => NetworkServer.connections.Clear(),
        "disabled" => () => SessionSettings.RabbitPotionsForUse=default,
        _ => () => level.Avatar.spawner.PlayerAvatar=new()
    };
    level.OnLevel=change; level.AddExp(10);
    Check(inventory.Grants==0 && inventory.temporaryInventory.Count==0, "Native callback stale context rejected: "+transition);
    Reset(); level=NewPlayer(); inventory=level.Avatar.Inventory; inventory.OnPermission=change; level.AddExp(10);
    Check(inventory.Grants==0 && inventory.Permissions==0, "Permission callback stale context rejected: "+transition);
}
Reset(); level=NewPlayer(); inventory=level.Avatar.Inventory;
using (new GridInventory.Permission(inventory))
{
    level.AddExp(10);
    Check(inventory.Items.Count==1 && inventory.HasPermission && inventory.Permissions==1,
        "Borrow a native permission without prematurely releasing its caller's scope");
}
Check(!inventory.HasPermission && inventory.Permissions==0, "Caller alone closes its native inventory scope");
Reset(); level=NewPlayer(); inventory=level.Avatar.Inventory;
level.OnLevel=()=>{level.OnLevel=null; level.AddExp(10);}; level.AddExp(10);
Check(inventory.Items.Count==2 && level.NativeCalls==2, "Nested legitimate XP earns both rewards");
Reset(); level=NewPlayer(); level.OnLevel=()=>throw new ApplicationException("native error");
try { level.AddExp(10); throw new Exception("Native error swallowed"); } catch(ApplicationException) { checks++; }
Check(level.Avatar.Inventory.Grants==0 && RabbitLevelUpFeature.Available, "Failed native level does not grant or disable addon");
Reset(); level=NewPlayer(); level.AddExp(10); var old = level;
NetworkServer.connections.Remove(1); PlayerSpawner.MultiplayerList.Remove(old.Avatar.spawner);
level=NewPlayer(); level.Initialize(2,10); level.GenerateItem(99); level.AddExp(10);
Check(old.Avatar.Inventory.Items.Count==1 && level.Avatar.Inventory.Items.Count==1, "Reconnect restored levels are not replayed; new levels still grant");
Reset(); level=NewPlayer(); NetworkServer.active=false; level.AddExp(20);
Check(level.Avatar.Inventory.Grants==0, "Unmodified client path never writes"); NetworkServer.active=true;
Reset(); ItemDatabase.Items[28].aName.key="Unexpected"; RabbitLevelUpFeature.OnDatabasesReady();
Check(!RabbitLevelUpFeature.Available, "Changed catalog identity fails closed");
LoadCatalog(); Check(RabbitLevelUpFeature.Available, "Valid ready catalog restores compatibility");
foreach(int id in identities.Select(i=>i.Id))
{
    LoadCatalog(); ItemDatabase.Items.Remove(id); RabbitLevelUpFeature.OnDatabasesReady();
    Check(!RabbitLevelUpFeature.Available, "Missing pool member fails closed: "+id);
}
Reset(); level=NewPlayer(); RabbitLevelUpFeature.Shutdown(); level.AddExp(10);
Check(level.NativeCalls==1 && level.Avatar.Inventory.Grants==0, "Unload restores native method");
Console.WriteLine($"Passed {checks} Rabbit earned-level reward checks.");
