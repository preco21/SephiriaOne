using Mirror;
using SephiriaOne;

int checks = 0;
string testDataRoot = Path.Combine(Path.GetTempPath(), "SephiriaOne-runtime-" + Guid.NewGuid().ToString("N"));
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
    UnityEngine.Application.persistentDataPath = Path.Combine(testDataRoot, Guid.NewGuid().ToString("N"));
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

// Native NewGame clears/reloads dungeon constants while keeping these avatars.
// Its SDK event fires before synchronous avatar reinitialization; LateUpdate
// runs afterwards, before the players enter the next run and items are granted.
void RestartLobby(int nativeLimit = 12)
{
    DungeonManager.Instance.constValueDictionary.Clear();
    DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] = nativeLimit;
    HorayModAPI.StartSession();
}
int Carryover(PlayerSpawner player) => Math.Min(Points(player), DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey]);

host = Start();
guest = Add(2, points: 9);
Check(Fountain("+100") && Stats("luck +10") && Choices("all 5"), "Prepare first-run settings for distinct players");
Check(Carryover(host) == 104 && Carryover(guest) == 109, "First run honors increased Fountain capacity");
RestartLobby();
SessionSettings.Synchronize();
Check(Carryover(host) == 104 && Carryover(guest) == 109, "Second run restores carryover cap without another command");
Check(Points(host) == 104 && Points(guest) == 109 && Value(host, FountainPoints.ContributionKey) == 100 &&
    Value(host) == 15 && Value(host, "EXTRAITEMCHOICES") == 7, "Restart repairs the cap without replaying any player adjustment");
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.OriginalLimitKey] == 12 &&
    DungeonManager.Instance.constValueDictionary[FountainPoints.AppliedLimitKey] == 109, "Restart records the new lobby limit for reset");
SessionSettings.Synchronize();
Check(Points(guest) == 109, "Later frames do not stack Fountain points");
RestartLobby(15);
SessionSettings.Synchronize();
Check(Carryover(guest) == 109 && DungeonManager.Instance.constValueDictionary[FountainPoints.OriginalLimitKey] == 15,
    "Third run preserves allowance and tracks the latest native cap");
Check(Fountain("reset") && Points(host) == 4 && Points(guest) == 9 &&
    DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 15, "Reset after multiple runs restores points and latest native cap");
RestartLobby();
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "Reset prevents cap restoration on future runs");

host = Start();
Check(Fountain("100"), "Prepare absolute target before restart");
RestartLobby();
host.PlayerAvatar.Inventory.canBroadcast = 0;
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "Wait for inventory reinitialization before repairing cap");
host.PlayerAvatar.Inventory.canBroadcast = 1;
host.PlayerAvatar.Inventory.dimensionPocket += 2;
SessionSettings.Synchronize();
Check(Carryover(host) == 102 && Value(host, FountainPoints.ContributionKey) == 96,
    "Deferred restoration respects native point changes without re-enforcing the old target");
DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] = 50;
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 50,
    "Completed restart repair does not continuously overwrite independent cap changes");
RestartLobby(150);
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 150 &&
    !DungeonManager.Instance.constValueDictionary.ContainsKey(FountainPoints.OriginalLimitKey), "Higher native cap stays unclaimed");
Check(Fountain("reset") && Points(host) == 6 && DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 150,
    "Reset preserves native additive points and an independently higher cap");

host = Start();
host.PlayerAvatar.Inventory.dimensionPocket = 20;
Check(Fountain("20"), "Set equal to native points still raises the carryover cap");
RestartLobby();
SessionSettings.Synchronize();
Check(Carryover(host) == 20 && Value(host, FountainPoints.ContributionKey) == 0,
    "Retained set with zero contribution still repairs its carryover cap");
RestartLobby();
SessionSettings.Stop();
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "Unload cancels pending restart repair");
HorayModAPI.StartSession();
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "SDK event after unload cannot repair the cap");

host = Start();
Check(Fountain("100"), "Prepare restart authority check");
NetworkServer.active = false;
RestartLobby();
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "Client cannot restore host carryover limit");
NetworkServer.active = true;
PlayerSpawner.MultiplayerList.Clear();
host = Add(7);
HorayModAPI.StartSession();
SessionSettings.Synchronize();
Check(Carryover(host) == 4 && DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12,
    "Stopped server does not retain restart settings into the next hosted session");

host = Start();
guest = Add(2, points: 9);
Check(Fountain("+100"), "Prepare players with different restart readiness");
RestartLobby();
guest.PlayerAvatar.Inventory.canBroadcast = 0;
SessionSettings.Synchronize();
Check(Carryover(host) == 104, "An initializing guest does not block the ready host's cap repair");
guest.PlayerAvatar.Inventory.canBroadcast = 1;
guest.PlayerAvatar.Inventory.dimensionPocket += 3;
SessionSettings.Synchronize();
Check(Carryover(guest) == 112, "Repair includes the guest's final native points once ready");
Check(Fountain("reset") && Points(host) == 4 && Points(guest) == 12 &&
    DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "Staged repair preserves both reset baselines");

host = Start();
host.PlayerAvatar.Inventory.dimensionPocket = 25;
Check(Stats("luck +10"), "Prepare session without Fountain commands");
RestartLobby();
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12,
    "Unmodified Fountain points do not raise the native cap without an addon setting or marker");

host = Start();
Check(Fountain("100"), "Prepare reset during restart frame");
RestartLobby();
Check(Fountain("reset") && Points(host) == 4 && DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12,
    "Reset before first LateUpdate clears the restarted cap and retained setting");
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12, "No delayed repair overrides the restart-frame reset");

host = Start();
Check(Fountain("100"), "Prepare existing tracked adjustment before addon reload");
SessionSettings.Stop();
SessionSettings.Start();
SessionSettings.Synchronize();
RestartLobby();
SessionSettings.Synchronize();
Check(Carryover(host) == 100 && Points(host) == 100, "Existing contribution marker can restore carryover after policy memory is cleared");

bool Mod(PresetAction action, out string[] messages) => SessionSettings.TryExecutePreset(action, out messages);
string PresetPath() => Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt");
void RestartHost()
{
    SessionSettings.Stop();
    PlayerSpawner.MultiplayerList.Clear();
    DungeonManager.Instance = new DungeonManager();
    SessionSettings.Start();
}

host = Start();
Check(Fountain("100") && Stats("luck +10") && Choices("item 5"), "Prepare all families for a saved preset");
Check(Mod(PresetAction.Status, out var messages) && string.Join("\n", messages).Contains("stats luck offset 10") &&
    string.Join("\n", messages).Contains("luck=15") && string.Join("\n", messages).Contains("Fountain=100"),
    "Status reports retained intent and actual current player values");
Check(!File.Exists(PresetPath()), "Status does not implicitly save a preset");
Check(Mod(PresetAction.Save, out _) && File.Exists(PresetPath()), "Explicit save stores all active settings");
Check(Stats("luck +5") && Stats("luck reset"), "Current-session edits and resets remain possible after saving");
Check(File.ReadAllText(PresetPath()).Contains("stats luck offset 10"), "Commands and resets do not silently overwrite saved snapshot");
RestartHost();
host = Add(8, luck: 20, points: 2);
host.PlayerAvatar.Inventory.canBroadcast = 0;
SessionSettings.Synchronize();
Check(Value(host) == 20 && Points(host) == 2, "Saved settings wait for host initialization");
host.PlayerAvatar.Inventory.canBroadcast = 1;
SessionSettings.Synchronize();
Check(Value(host) == 30 && Points(host) == 100 && Value(host, "EXTRAITEMCHOICES") == 5,
    "New hosted session automatically restores saved policy against new native baseline");
SessionSettings.Synchronize();
Check(Value(host) == 30, "Saved relative settings do not stack on following frames");
guest = Add(9, luck: 7, points: 9);
Check(Mod(PresetAction.Status, out messages) && Value(guest) == 7 && Points(guest) == 9,
    "Status stays read-only even with a pending joining player");
SessionSettings.Synchronize();
Check(Value(guest) == 17 && Points(guest) == 100, "Late guest inherits automatically loaded preset");
Check(Mod(PresetAction.Status, out messages) && messages.Any(x => x.Contains("Player #9")), "Status covers each ready player");
Check(Stats("luck +3"), "Change active policy after loading saved copy");
RestartLobby();
SessionSettings.Synchronize();
Check(Value(host) == 33 && Value(guest) == 20 && Carryover(guest) == 100,
    "Same-host lobby restart neither reloads saved copy nor reapplies offsets");
SessionSettings.Stop();
SessionSettings.Start();
SessionSettings.Synchronize();
Check(Value(host) == 30 && Value(guest) == 17, "Reloaded preset replaces existing contributions without stacking");
Check(Mod(PresetAction.Forget, out _) && !File.Exists(PresetPath()) && Value(host) == 30, "Forget removes disk preset only");
RestartHost();
host = Add(10, luck: 21);
SessionSettings.Synchronize();
Check(Value(host) == 21 && Points(host) == 4, "Forgotten preset does not apply on next host start");

Check(Stats("luck +10") && Mod(PresetAction.Save, out _), "Prepare authority and overwrite checks");
NetworkServer.active = false;
string savedBeforeGuest = File.ReadAllText(PresetPath());
Check(!Mod(PresetAction.Save, out _) && !Mod(PresetAction.Forget, out _) && !Mod(PresetAction.Status, out _) &&
    File.ReadAllText(PresetPath()) == savedBeforeGuest, "Guests cannot edit host settings or present a misleading host status");
SessionSettings.Synchronize();
NetworkServer.active = true;
RestartHost();
host = Add(11, luck: 25);
SessionSettings.Synchronize();
Check(Value(host) == 35, "Host can load the preset after leaving a client session");
Check(Stats("reset") && Mod(PresetAction.Save, out _), "Saving after reset explicitly replaces the saved settings");
RestartHost();
host = Add(12, luck: 22);
SessionSettings.Synchronize();
Check(Value(host) == 22, "Saved empty preset makes no automatic adjustments");

File.WriteAllText(PresetPath(), "SephiriaOne preset v1\nstats luck set 100\nbroken");
UnityEngine.Debug.Warnings.Clear();
RestartHost();
host = Add(13, luck: 6);
SessionSettings.Synchronize();
SessionSettings.Synchronize();
Check(Value(host) == 6 && UnityEngine.Debug.Warnings.Count == 1, "Bad saved file applies nothing and warns once per hosted session");
Check(Mod(PresetAction.Forget, out _), "Malformed preset can be removed through the command");
Check(Stats("luck set 11") && Mod(PresetAction.Save, out _), "Save valid but multiplier-dependent target");
RestartHost();
host = Add(14, luck: 5);
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(Value(host) == 5, "Incompatible saved preset uses existing atomic player rejection");
SessionSettings.Stop();
Check(!Mod(PresetAction.Save, out _), "Unloaded controller cannot write a preset");

if (Directory.Exists(testDataRoot)) Directory.Delete(testDataRoot, true);
Console.WriteLine($"Passed {checks} runtime command/session integration checks using game API fixtures.");
