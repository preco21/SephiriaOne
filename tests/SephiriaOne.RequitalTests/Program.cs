using System.Reflection;
using SephiriaOne;

if (Assembly.GetExecutingAssembly().GetType("SephiriaOne.RequitalRuntime") == null)
    throw new Exception("Requital runtime must implement native starting loadouts and manual gifts.");

int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
PlayerAvatar Player(string guid = null)
{
    var p = new PlayerAvatar();
    p.spawner = new PlayerSpawner { PlayerAvatar = p, playerGuid = guid ?? Guid.NewGuid().ToString() };
    p.Inventory = new GridInventory { UnitAvatar = p };
    PlayerSpawner.MultiplayerList.Add(p.spawner);
    return p;
}
void Reset()
{
    RequitalRuntime.Shutdown();
    PlayerSpawner.MultiplayerList.Clear(); DungeonManager.Instance = new();
    SaveManager.CurrentRun = new(); Mirror.NetworkServer.active = true;
    HorayNetworkAuthenticator.AccessDeny_InDungeon = false;
    SessionSettings.Requital = new(false, 1);
    ItemDatabase.Items[2100] = new() { id = 2100, aName = new() { key = "Item_StoneTablet_Requital_Name" },
        type = EItemType.StoneTablet, rarity = EItemRarity.Eternal, activeType = EItemActiveType.WitchHat };
    ItemDatabase.Items[2099] = new() { id = 2099, aName = new() { key = "Item_StoneTablet_Heaven_Name" },
        type = EItemType.StoneTablet, rarity = EItemRarity.Eternal, activeType = EItemActiveType.Hidden };
    RequitalRuntime.Initialize(); RequitalRuntime.Load();
}
Reset();
var player = Player();
RequitalRuntime.Tick();
Check(player.Inventory.Grants == 0, "default off");
SessionSettings.Requital = new(true, 3);
RequitalRuntime.Tick();
Check(player.Inventory.Grants == 3 && player.Inventory.startingItems.Count == 3, "lobby starting gift count");
var autoIds = player.Inventory.startingItems.Select(x => x.instanceID).ToArray();
for (int i = 0; i < 50; i++) RequitalRuntime.Tick();
Check(player.Inventory.Grants == 3, "stable frames do not replay");
HorayNetworkAuthenticator.AccessDeny_InDungeon = true;
player.Inventory.Held.Clear();
SessionSettings.ResourceGeneration++;
RequitalRuntime.Clear(); RequitalRuntime.Tick();
Check(player.Inventory.Grants == 3, "floor/reset never delivers extra copies");
SessionSettings.Requital = new(true, 4);
RequitalRuntime.Tick();
Check(player.Inventory.Grants == 3 && player.Inventory.startingItems.Count == 4, "in-run increase only registers next loadout");
DungeonManager.Instance.globalItemStatTable.Clear();
player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Grants == 7 && player.Inventory.Held.Count == 4, "fresh native restock delivers once after marker clear");
Check(DungeonManager.Instance.globalItemStatTable.Count == 4, "fresh restock restores provenance");
Check(RequitalRuntime.GiveAll(out _), "manual action succeeds");
Check(player.Inventory.Held.Count == 8 && player.Inventory.startingItems.Count == 4, "manual copies are ordinary");
SessionSettings.Requital = new(false, 4); RequitalRuntime.Tick();
Check(player.Inventory.Held.Count == 4 && player.Inventory.startingItems.Count == 0, "disable retracts only automatic copies");

Reset(); player = Player(); player.Inventory.Full = true;
SessionSettings.Requital = new(true, 2); RequitalRuntime.Tick();
Check(player.Inventory.Pending.Count == 2, "automatic full inventory uses native pending");
Check(RequitalRuntime.GiveAll(out _) && player.Inventory.temporaryInventory.Count == 2, "manual full inventory uses temporary overflow");
SessionSettings.Requital = new(false, 2); RequitalRuntime.Tick();
Check(player.Inventory.Pending.Count == 0 && player.Inventory.temporaryInventory.Count == 2, "automatic pending cleanup preserves ordinary overflow");
SessionSettings.Requital = new(true, 2); RequitalRuntime.Tick();
// Native initialization clears carried items but preserves its pending queue.
player.Inventory.ClearCarried(); DungeonManager.Instance.globalItemStatTable.Clear();
player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Pending.Count == 2 && player.Inventory.Pending.Select(x => x.instanceID).Distinct().Count() == 2,
    "fresh run still-full inventory keeps one native pending entry per automatic ID");
player.Inventory.ClearCarried(); player.Inventory.Full = false;
player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Held.Count == 2 && player.Inventory.Pending.Count == 0,
    "fresh run with space delivers pending automatic IDs once and removes old queued entries");

Reset(); player = Player(); var other = Player();
SessionSettings.Requital = new(true, 2); RequitalRuntime.Tick();
int transferred = player.Inventory.startingItems[1].instanceID;
other.Inventory.Engraved[transferred] = player.Inventory.Held[transferred]; player.Inventory.Held.Remove(transferred);
int native = ItemDatabase.GenerateInstanceID(new()); player.Inventory.Held[native] = new(native, 2100, 1);
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
Check(!other.Inventory.Engraved.ContainsKey(transferred), "count reduction retracts transferred engraving");
Check(player.Inventory.Held.ContainsKey(native), "native Requital survives");

Reset(); player = Player(); SessionSettings.Requital = new(true, 2); RequitalRuntime.Tick();
var restored = player.Inventory.Held.Values.ToArray(); string owner = player.spawner.playerGuid;
PlayerSpawner.MultiplayerList.Remove(player.spawner);
var rejoin = Player(owner); foreach (var item in restored) rejoin.Inventory.Held[item.instanceID] = item;
HorayNetworkAuthenticator.AccessDeny_InDungeon = true; RequitalRuntime.Clear(); RequitalRuntime.Tick();
Check(rejoin.Inventory.startingItems.Count == 2 && rejoin.Inventory.Grants == 0, "saved markers reconstruct registration without regrant");
SessionSettings.Requital = new(false, 2); RequitalRuntime.Tick();
Check(rejoin.Inventory.Held.Count == 0, "rejoin preserves exact ownership cleanup");

Reset(); player = Player(); player.spawner.Ready = false;
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
Check(player.Inventory.Grants == 0, "loading waits");
player.spawner.Ready = true; player.Inventory.ThrowAfterWrite = true; RequitalRuntime.Tick();
Check(player.Inventory.Grants == 1, "ambiguous callback writes once");
player.Inventory.ThrowAfterWrite = false; player.spawner.Ready = false; RequitalRuntime.Tick();
player.spawner.Ready = true;
for (int i = 0; i < 10; i++) RequitalRuntime.Tick();
SessionSettings.Requital = new(true, 2); RequitalRuntime.Tick();
Check(player.Inventory.Grants == 1, "fault is not replayed by readiness or settings changes");
SessionSettings.Requital = new(false, 2); RequitalRuntime.Tick();
Check(player.Inventory.Held.Count == 0, "explicit disable safely cleans faulted exact automatic registration");
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
Check(player.Inventory.Grants == 2, "verified off cleanup permits explicit fresh enable after fault");
RequitalRuntime.Shutdown();
Check(player.Inventory.Held.Count == 0, "shutdown retracts faulted tracked automatic registration");

Reset(); player = Player(); SessionSettings.Requital = new(false, 2);
player.Inventory.ThrowAfterWrite = true;
Check(!RequitalRuntime.GiveAll(out _) && player.Inventory.Grants == 1 && player.Inventory.temporaryInventory.Count == 0,
    "manual callback error does not add overflow or continue remaining count");
player.Inventory.ThrowAfterWrite = false;
ItemDatabase.Items[2100].rarity = EItemRarity.Common;
Check(!RequitalRuntime.GiveAll(out _) && player.Inventory.Grants == 1, "database replacement rejected");
RequitalRuntime.Shutdown();

Reset(); player = Player(); int unrelated = ItemDatabase.GenerateInstanceID(new());
player.Inventory.OnWrite = () =>
{
    player.Inventory.startingItems.Add(new(unrelated, 2100, 1));
    player.Inventory.Held[unrelated] = new(unrelated, 2100, 1);
};
player.Inventory.ThrowAfterWrite = true; SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
player.Inventory.OnWrite = null; player.Inventory.ThrowAfterWrite = false;
SessionSettings.Requital = new(false, 1); RequitalRuntime.Tick();
Check(player.Inventory.Held.Count == 1 && player.Inventory.Held.ContainsKey(unrelated),
    "callback native Requital never becomes automatic provenance");
RequitalRuntime.Shutdown();
Reset(); player = Player(); other = Player();
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
transferred = player.Inventory.startingItems[0].instanceID;
var transferredItem = player.Inventory.Held[transferred];
other.Inventory.Held[transferred] = transferredItem; player.Inventory.Held.Remove(transferred);
PlayerSpawner.MultiplayerList.Remove(other.spawner);
SessionSettings.Requital = new(false, 1); RequitalRuntime.Tick();
var savedRecipient = Player(other.spawner.playerGuid); savedRecipient.Inventory.Held[transferred] = transferredItem;
RequitalRuntime.Tick();
Check(!savedRecipient.Inventory.Held.ContainsKey(transferred), "retired transferred auto ID retracts from offline recipient on restore");
RequitalRuntime.Shutdown();

Reset(); player = Player(); other = Player();
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
transferred = player.Inventory.startingItems[0].instanceID;
other.Inventory.Held[transferred] = player.Inventory.Held[transferred];
PlayerSpawner.MultiplayerList.Remove(player.spawner);
SessionSettings.Requital = new(false, 1); RequitalRuntime.Tick();
Check(!other.Inventory.Held.ContainsKey(transferred), "disable retracts transferred copy even when its original owner is absent");
RequitalRuntime.Shutdown();

Reset(); player = Player(); ItemDatabase.Items[2100].rarity = EItemRarity.Common; RequitalRuntime.Load();
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
Check(player.Inventory.Grants == 0, "invalid catalog suspends automatic addition");
ItemDatabase.Items[2100].rarity = EItemRarity.Eternal; RequitalRuntime.Load(); RequitalRuntime.Tick();
Check(player.Inventory.Grants == 1, "database readiness resumes suspended addition");
bool cleanupFailed = false;
player.Inventory.OnWrite = () => throw new Exception("cleanup callback failed");
try { RequitalRuntime.Shutdown(); } catch { cleanupFailed = true; }
Check(cleanupFailed && RequitalRuntime.Available, "failed unload preserves hooks and reports cleanup failure");
player.Inventory.OnWrite = null; RequitalRuntime.Shutdown();
Check(!RequitalRuntime.Available && player.Inventory.Held.Count == 0, "explicit unload retry finishes exact cleanup");
RequitalRuntime.Initialize(); RequitalRuntime.Load(); RequitalRuntime.Tick();
Check(player.Inventory.Held.Count == 1, "reload on same native inventory resumes after verified cleanup");
RequitalRuntime.Shutdown();
Reset(); player = Player(); SessionSettings.Requital = new(true, 2);
player.Inventory.OnWrite = () => ItemDatabase.Items[2100].rarity = EItemRarity.Common;
RequitalRuntime.Tick();
Check(player.Inventory.Grants == 1, "automatic batch revalidates item identity after native callback");
player.Inventory.OnWrite = null; RequitalRuntime.Shutdown();

Reset(); player = Player(); SessionSettings.Requital = new(true, 3);
player.Inventory.OnWrite = () => player.spawner.PlayerAvatar = new PlayerAvatar();
RequitalRuntime.Tick();
Check(player.Inventory.Grants == 1, "automatic batch rejects replaced owner after first native callback");
player.spawner.PlayerAvatar = player; player.Inventory.OnWrite = null; RequitalRuntime.Shutdown();

Reset(); player = Player(); SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
var ordinaryId = ItemDatabase.GenerateInstanceID(new());
player.Inventory.startingItems.Add(new(ordinaryId, 2100, 1));
player.Inventory.ClearCarried(); player.Inventory.Full = true; player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Pending.Count == 1 && player.Inventory.Pending[0].instanceID != ordinaryId,
    "native unowned Requital keeps original restock overflow behavior");
RequitalRuntime.Shutdown();

Reset(); for (int i = 0; i < 9; i++) Player();
SessionSettings.Requital = new(true, 2); RequitalRuntime.Tick();
// Warm tiered compilation through the same length as the measurement.
// The first long Release loop can include one-time runtime allocations.
for (int i = 0; i < 10000; i++) RequitalRuntime.Tick();
long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++) RequitalRuntime.Tick();
long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
Check(allocated == 0, $"unchanged nine-player ticks allocate zero bytes, observed {allocated}");
Check(PlayerSpawner.MultiplayerList.Sum(p => p.PlayerAvatar.Inventory.Grants) == 18, "stable nine-player ticks do not refill");
RequitalRuntime.Shutdown();
Reset(); player = Player(); player.Inventory.Full = true;
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
player.Inventory.ClearCarried(); player.Inventory.Full = false;
player.Inventory.RetryPending(); // Native Update can run during restart's waits.
Check(player.Inventory.Grants == 1 && player.Inventory.Pending.Count == 0, "restart gap can deliver native pending automatic ID");
player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Grants == 1 && player.Inventory.Held.Count == 1,
    "restock skips automatic ID already delivered during restart gap");
RequitalRuntime.Shutdown();

Reset(); player = Player(); player.Inventory.Full = true;
SessionSettings.Requital = new(true, 1); RequitalRuntime.Tick();
ordinaryId = player.Inventory.AddStartingItem(new(-1, 2100, 1), 0);
player.Inventory.ClearCarried(); player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Pending.Count == 2 && player.Inventory.Pending.Count(x => x.instanceID == ordinaryId) == 1,
    "restock pending cleanup preserves unrelated native pending ID");
RequitalRuntime.Shutdown();

Reset(); player = Player(); HorayNetworkAuthenticator.AccessDeny_InDungeon = true;
SessionSettings.Requital = new(true, 3); RequitalRuntime.Tick();
player.Inventory.ThrowAfterWrite = true; player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Grants == 1, "first ambiguous restock write blocks later automatic IDs");
player.Inventory.ThrowAfterWrite = false; player.Inventory.RestockStartingItem(0);
Check(player.Inventory.Grants == 1, "later restock cannot replay a latched ambiguous automatic write");
RequitalRuntime.Shutdown();
SlateSelectionTests.Run(Reset, Player, Check);
Console.WriteLine($"Slate runtime: {checks} assertions passed.");
