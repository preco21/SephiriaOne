using Mirror;
using SephiriaOne;

int checks = 0;
void Check(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
    checks++;
}
PlayerSpawner Add(uint id, int luck = 5, int points = 4, int itemChoices = 0)
{
    var spawner = new PlayerSpawner { netId = id };
    spawner.PlayerAvatar.netId = id;
    spawner.PlayerAvatar.Inventory.netId = id;
    spawner.PlayerAvatar.Inventory.dimensionPocket = points;
    spawner.PlayerAvatar.customStats["LUCK"] = luck;
    spawner.PlayerAvatar.customStats["EXTRAITEMCHOICES"] = itemChoices;
    PlayerSpawner.MultiplayerList.Add(spawner);
    return spawner;
}
PlayerSpawner Start()
{
    SessionSettings.Stop();
    PlayerSpawner.MultiplayerList.Clear();
    UnityEngine.Debug.Warnings.Clear();
    DungeonManager.Instance = new DungeonManager();
    NetworkServer.active = true;
    ChoiceFeature.Available = true;
    SessionSettings.Start();
    return Add(1, itemChoices: 2);
}
int Value(PlayerSpawner player, string key = "LUCK") => player.PlayerAvatar.customStats.GetValueOrDefault(key);
int Points(PlayerSpawner player) => player.PlayerAvatar.Inventory.dimensionPocket;
bool Fountain(string args)
{
    if (FountainCommand.Parse("/fountain " + args, out var command, out _) != FountainParseResult.Valid) throw new Exception(args);
    return FountainPoints.TryExecute(command, out _);
}
bool Stats(string args)
{
    if (StatCommand.Parse("/stats " + args, out var command, out _) != StatParseResult.Valid) throw new Exception(args);
    return CharacterStats.TryExecute(command, out _);
}
bool Choices(string args)
{
    if (ChoiceCommand.Parse("/choices " + args, out var command, out _) != ChoiceParseResult.Valid) throw new Exception(args);
    return ChoicePoints.TryExecute(command, out _);
}

var host = Start();
Check(Fountain("+10") && Stats("luck +10") && Choices("all 5"), "Host commands succeed before first LateUpdate");
Check(Points(host) == 14 && Value(host) == 15 && Value(host, "EXTRAITEMCHOICES") == 7, "Current host receives one adjustment");
SessionSettings.Synchronize();
SessionSettings.Synchronize();
Check(Points(host) == 14 && Value(host) == 15, "Repeated updates do not reapply to current host");
var guest = Add(2, luck: 7, points: 2, itemChoices: 3);
guest.connectionToClient.isReady = false;
SessionSettings.Synchronize();
Check(Value(guest) == 7 && Points(guest) == 2, "Connection readiness blocks inheritance");
guest.connectionToClient.isReady = true;
guest.PlayerAvatar.Race = null;
SessionSettings.Synchronize();
Check(Value(guest) == 7, "Spawn ID alone is not enough before native race initialization");
guest.PlayerAvatar.Race = new UnityEngine.Object();
guest.PlayerAvatar.currentFloorGuid = "";
SessionSettings.Synchronize();
Check(Value(guest) == 7, "Wait for initialized floor");
guest.PlayerAvatar.currentFloorGuid = "town";
guest.PlayerAvatar.Inventory.canBroadcast = 0;
SessionSettings.Synchronize();
Check(Value(guest) == 7, "Wait until inventory restoration completes");
guest.PlayerAvatar.Inventory.canBroadcast = 1;
SessionSettings.Synchronize();
Check(Value(guest) == 17 && Points(guest) == 12 && Value(guest, "EXTRAITEMCHOICES") == 8,
    "Ready guest inherits all active settings against native baselines");
Check(Value(guest, "SEPHIRIAONE_STAT_LUCK") == 10 && Value(guest, "SEPHIRIAONE_FOUNTAINPOINTS") == 10,
    "Inheritance writes reset markers");
SessionSettings.Synchronize();
Check(Value(guest) == 17 && Points(guest) == 12, "Newcomer is only processed once");
var arrival = Add(3, luck: 11, points: 1);
Check(Stats("luck +3") && Value(arrival) == 24 && Value(host) == 18 && Value(guest) == 20,
    "A command in the arrival frame first inherits old policy then adds the new delta once");
SessionSettings.Synchronize();
Check(Value(arrival) == 24, "Arrival-frame command is not duplicated by later polling");
Check(Stats("luck reset") && Fountain("reset") && Choices("item reset"), "Reset commands succeed after inheritance");
Check(Value(host) == 5 && Value(guest) == 7 && Value(arrival) == 11 && Points(guest) == 2 &&
    Value(guest, "EXTRAITEMCHOICES") == 3, "Reset restores each player's distinct native baseline");
var afterReset = Add(4, luck: 20, points: 7, itemChoices: 1);
SessionSettings.Synchronize();
Check(Value(afterReset) == 20 && Points(afterReset) == 7 && Value(afterReset, "EXTRAITEMCHOICES") == 1 &&
    Value(afterReset, "EXTRAWEAPONCHOICES") == 5, "Future joins exclude reset selections and inherit other categories");
Check(Choices("reset"), "Reset all choices succeeds");
var untouched = Add(5, luck: 23, points: 8);
SessionSettings.Synchronize();
Check(Value(untouched) == 23 && Points(untouched) == 8 && Value(untouched, "EXTRAWEAPONCHOICES") == 0,
    "Reset all active settings leaves later arrivals untouched");

host = Start();
Check(Stats("luck +10") && !Stats("luck -100"), "Failed current-player batch rejects command");
guest = Add(2, luck: 7);
SessionSettings.Synchronize();
Check(Value(guest) == 17, "Failed command is not remembered for future players");
Check(Fountain("100"), "Fountain absolute target succeeds");
arrival = Add(3, points: 1);
arrival.PlayerAvatar.Inventory.canBroadcast = 0;
Check(!Fountain("+5"), "Initializing player rejects manual command");
arrival.PlayerAvatar.Inventory.canBroadcast = 1;
SessionSettings.Synchronize();
Check(Points(arrival) == 100, "Unready-player rejection did not alter retained target");
PlayerSpawner.MultiplayerList.Remove(guest);
var rejoined = Add(6, luck: 7, points: 2);
SessionSettings.Synchronize();
Check(Value(rejoined) == 17 && Points(rejoined) == 100, "New avatar after reconnect inherits once");
SessionSettings.Synchronize();
Check(Value(rejoined) == 17, "Reconnect does not stack on following frames");

host = Start();
Check(Fountain("100") && Choices("all 5") && Stats("luck 11"), "Prepare mixed active policy");
guest = Add(2, luck: 5, points: 2);
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(Points(guest) == 2 && Value(guest) == 5 && Value(guest, "EXTRAITEMCHOICES") == 0,
    "Incompatible guest receives no partial inherited writes");
Check(UnityEngine.Debug.Warnings.Count == 1, "Host receives one inheritance warning");
SessionSettings.Synchronize();
Check(UnityEngine.Debug.Warnings.Count == 1 && Points(guest) == 2, "Invalid inheritance is not repeatedly retried");
Check(Stats("luck 12") && Value(guest) == 6, "Explicit command can resolve guest incompatibility");
Check(Stats("reset") && Choices("reset") && Fountain("reset"), "Reset is possible after rejected inheritance");
Check(Value(host) == 5 && Value(guest) == 5 && Points(guest) == 2, "Failed inheritance does not corrupt reset baselines");

host = Start();
Check(Stats("luck +10"), "Prepare setting before session boundary");
NetworkServer.active = false;
Check(!Stats("luck 100") && !Fountain("100") && !Choices("all 5"), "Non-host callers cannot mutate any family");
SessionSettings.Synchronize();
NetworkServer.active = true;
PlayerSpawner.MultiplayerList.Clear();
guest = Add(2, luck: 7);
SessionSettings.Synchronize();
Check(Value(guest) == 7, "Stopping the server clears retained settings");
Check(Stats("luck +10"), "Prepare setting before dungeon replacement");
DungeonManager.Instance = new DungeonManager();
arrival = Add(3, luck: 11);
SessionSettings.Synchronize();
Check(Value(arrival) == 11, "Dungeon replacement clears settings even while server is active");
Check(Stats("luck +10"), "Prepare setting before addon unload");
SessionSettings.Stop();
var afterUnload = Add(4, luck: 15);
SessionSettings.Synchronize();
Check(Value(afterUnload) == 15 && !Stats("luck 100"), "Unloaded controller cannot apply or retain commands");

Console.WriteLine($"Passed {checks} runtime command/session integration checks using game API fixtures.");
