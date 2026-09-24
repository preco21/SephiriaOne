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
    spawner.PlayerAvatar.spawner = spawner;
    spawner.PlayerAvatar.localDataStorage = spawner.LocalDataStorage;
    spawner.PlayerAvatar.isOwned = id == 1;
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
    SaveManager.CurrentRun = new();
    PlayerSpawner.MultiplayerList.Clear();
    UnityEngine.Debug.Warnings.Clear();
    DungeonManager.Instance = new DungeonManager();
    NetworkServer.active = true;
    ChoiceFeature.Available = true;
    ResourceFeature.Available = true;
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

// Relative commands must agree with inheritance even after native multipliers
// change. Compare players with the same baseline but different arrival times.
host = Start();
Check(Stats("luck +10"), "Prepare relative offset before native multiplier change");
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 20, "Native multiplier change automatically preserves displayed native luck plus ten");
Check(Stats("luck +10"), "Second relative command succeeds after multiplier change");
guest = Add(2, luck: 5);
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == guest.PlayerAvatar.GetCustomStatUnsafe("LUCK"),
    "Existing and joining characters with the same native baseline receive the same relative adjustment");
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 30 && Value(host, "SEPHIRIAONE_STAT_LUCK") == 10,
    "Cumulative displayed +20 is planned from native displayed 10, not previously amplified addon points");
Check(Mod(PresetAction.Save, out _), "Save recomposed relative offset");
RestartHost();
host = Add(3, luck: 5);
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 30, "Saved relative policy agrees with live command planning");

host = Start();
host.PlayerAvatar.customStatsAmp["LUCK"] = -50;
Check(Stats("luck +1") && Stats("luck -1"), "Cancel a relative adjustment under fractional multiplier");
Check(Value(host) == 5 && Value(host, "SEPHIRIAONE_STAT_LUCK") == 0,
    "Canceling offsets restores exact raw baseline rather than a rounded equivalent");

host = Start();
guest = Add(2, luck: 20);
Check(Stats("luck set 100") && Stats("luck +10"), "Relative command switches from absolute mode");
Check(Value(host) == 15 && Value(guest) == 30, "Set-to-relative switch uses each character's own baseline");
Check(Stats("luck +5") && Stats("luck -3") && Value(host) == 17 && Value(guest) == 32,
    "Subsequent relative commands accumulate a net plus twelve");
Check(Mod(PresetAction.Save, out _) && File.ReadAllText(PresetPath()).Contains("stats luck offset 12"),
    "Saved setting records relative mode after switching from set");
guest.PlayerAvatar.customStats["LUCK"] += 4;
guest.PlayerAvatar.calculatedBonusStats["LUCK"] = 3;
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(guest.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 66 && Value(host) == 17,
    "Native raw, equipment bonus and amplifier changes retain per-player baseline plus offset");
Check(Stats("luck reset") && Value(host) == 5 && Value(guest) == 24 &&
    guest.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 54, "Reset after automatic maintenance restores native raw and amplified stats");
guest.PlayerAvatar.customStatsAmp["LUCK"] = 0;
SessionSettings.Synchronize();
Check(Value(guest) == 24, "Reset stops automatic maintenance");

host = Start();
Check(Stats("luck +1"), "Prepare exact offset before incompatible multiplier");
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
UnityEngine.Debug.Warnings.Clear();
SessionSettings.Synchronize();
Check(Value(host) == 5 && Value(host, "SEPHIRIAONE_STAT_LUCK") == 0 && UnityEngine.Debug.Warnings.Count == 1,
    "Unrepresentable relative offset suspends only addon contribution and warns once");
Check(Mod(PresetAction.Status, out var suspendedStatus) && suspendedStatus.Any(line => line.Contains("relative setting suspended")),
    "Status distinguishes suspended offsets from currently applied values");
Check(Mod(PresetAction.Save, out _) && File.ReadAllText(PresetPath()).Contains("stats luck offset 1"),
    "Saving a suspended offset retains desired command intent");
SessionSettings.Synchronize();
Check(Value(host) == 5 && UnityEngine.Debug.Warnings.Count == 1, "Suspended offset does not retry or warn on unchanged frames");
host.PlayerAvatar.calculatedBonusStats["LUCK"] = 1;
SessionSettings.Synchronize();
Check(UnityEngine.Debug.Warnings.Count == 1 && Value(host) == 5, "Continuing incompatibility preserves native changes without warning spam");
host.PlayerAvatar.customStatsAmp["LUCK"] = 0;
SessionSettings.Synchronize();
Check(Value(host) == 6 && host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 7,
    "A changed compatible multiplier resumes the offset against updated native bonuses");
Check(Stats("luck set 100"), "Absolute command replaces relative maintenance");
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 200, "Absolute set remains a one-time adjustment");

host = Start();
Check(Stats("critical +1.25") && Stats("attackspeed +10"), "Prepare decimal and display-offset stats");
host.PlayerAvatar.customStatsAmp["CRITICAL"] = 25;
host.PlayerAvatar.customStatsAmp["ATTACKSPEED"] = 100;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("CRITICAL") == 125 && host.PlayerAvatar.GetCustomStatUnsafe("ATTACKSPEED") == 10,
    "Automatic maintenance respects fractional display units and attack-speed display offset");

host = Start();
Check(Stats("luck +10"), "Prepare maintenance lifecycle guards");
host.PlayerAvatar.Inventory.canBroadcast = 0;
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(Value(host) == 15, "Do not maintain relative stats during inventory initialization");
host.PlayerAvatar.Inventory.canBroadcast = 1;
SessionSettings.Synchronize();
Check(Value(host) == 10, "Resume relative maintenance once inventory is ready");
SessionSettings.Stop();
host.PlayerAvatar.customStatsAmp["LUCK"] = 0;
SessionSettings.Synchronize();
Check(Value(host) == 10, "Unload disables automatic stat writes");

host = Start();
Check(Stats("luck +1"), "Prepare relative inheritance rejection");
guest = Add(2);
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
guest.PlayerAvatar.customStatsAmp["LUCK"] = 0;
SessionSettings.Synchronize();
Check(Value(guest) == 5 && Value(host) == 6, "Initially rejected relative inheritance is not silently retried on native changes");
Check(Stats("luck +1") && Value(guest) == 7 && Value(host) == 7, "Explicit relative command enrolls previously rejected guest with full net offset");
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(Value(guest) == 6 && guest.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 12,
    "Successful explicit command enables future guest maintenance");
Check(Stats("reset"), "Reset-all cancels every relative registration");
guest.PlayerAvatar.customStatsAmp["LUCK"] = 0;
SessionSettings.Synchronize();
Check(Value(guest) == 5 && Value(host) == 5, "Reset-all cannot be undone by later multiplier changes");

host = Start();
Check(Stats("luck +10"), "Prepare relative policy before avatar native-state reset");
host.PlayerAvatar.customStats.Clear();
host.PlayerAvatar.customStats["LUCK"] = 20;
SessionSettings.Synchronize();
Check(Value(host) == 30 && Value(host, "SEPHIRIAONE_STAT_LUCK") == 10,
    "Replacing both native stats and their markers recomputes against new character baseline");
NetworkServer.active = false;
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(Value(host) == 30, "Automatic maintenance never writes without server authority");

host = Start();
guest = Add(2, points: 9);
Check(Fountain("+100"), "Prepare Fountain bonus before native preset/costume changes");
SessionSettings.Synchronize();
guest.PlayerAvatar.Inventory.dimensionPocket += 8;
SessionSettings.Synchronize();
Check(Points(guest) == 117 && Carryover(guest) == 117 && Value(guest, FountainPoints.ContributionKey) == 100,
    "Native Fountain capacity increases also raise the server carryover cap without replaying points");
Check(Fountain("reset") && Points(guest) == 17 && Points(host) == 4 &&
    DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12,
    "Reset after native capacity changes preserves the new baseline and original cap");

host = Start();
Check(Fountain("+100"), "Prepare same-frame native change before Fountain item grant");
SessionBoundaryHooks.Install();
host.PlayerAvatar.Inventory.dimensionPocket += 8;
host.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(host.LastFountainAllowance == 112, "Fountain grant sees native capacity changes before the next LateUpdate");
SessionBoundaryHooks.Uninstall();

host = Start();
guest = Add(2, points: 9);
Check(Fountain("0"), "Prepare native point removal after absolute Fountain set");
host.PlayerAvatar.Inventory.dimensionPocket -= 3;
guest.PlayerAvatar.Inventory.dimensionPocket += 30;
SessionSettings.Synchronize();
Check(Points(host) == -3 && Carryover(guest) == 30, "A native negative capacity cannot block another player's carryover repair");
Check(Fountain("reset") && Points(host) == 1 && Points(guest) == 39, "Reset preserves native point changes after a negative intermediate capacity");

// Match native costume/passive replacement: remove/apply only native effects.
host = Start();
guest = Add(2, luck: 20, points: 9, itemChoices: 3);
Check(Stats("luck +10") && Choices("all 5") && Fountain("+100"), "Prepare all families for native loadout replacement");
host.PlayerAvatar.customStats["LUCK"] -= 3;
host.PlayerAvatar.Inventory.dimensionPocket -= 2;
SessionSettings.Synchronize();
Check(Value(host) == 12 && Value(host, "SEPHIRIAONE_STAT_LUCK") == 10, "Native passive removal preserves relative offset and raw baseline");
host.PlayerAvatar.customStats["LUCK"] += 6;
host.PlayerAvatar.calculatedBonusStats["LUCK"] = 2;
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
host.PlayerAvatar.customStats["EXTRAITEMCHOICES"] += 2;
host.PlayerAvatar.Inventory.dimensionPocket += 10;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 30 && Value(guest) == 30,
    "Native costume/passive and multiplier replacement keeps independent baselines");
Check(Value(host, "EXTRAITEMCHOICES") == 9 && Value(host, "SEPHIRIAONE_EXTRAITEMCHOICES") == 5 &&
    Points(host) == 112 && Carryover(host) == 112, "Native loadout changes preserve candidate contribution and Fountain allowance");
host.PlayerAvatar.customStatsAmp["EXTRAITEMCHOICES"] = 100;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("EXTRAITEMCHOICES") == 18 && Value(host, "SEPHIRIAONE_EXTRAITEMCHOICES") == 5,
    "Candidate bonus remains raw additive and follows native amplification");
host.PlayerAvatar.maxPassivePoint += 3;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 30, "Awarding unspent hard-mode passive points does not change a stat");
host.PlayerAvatar.customStats["LUCK"] += 3;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 36 && Value(guest) == 30,
    "Spending native passive points updates only the owner's baseline");
host.PlayerAvatar.currentFloorGuid = "next-floor";
host.PlayerAvatar.customStatsAmp["LUCK"] = 0;
SessionSettings.Synchronize();
Check(host.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 23, "Buff expiration after floor/death transition keeps native value plus offset");
Check(Stats("reset") && Choices("reset") && Fountain("reset") && Value(host) == 11 && Points(host) == 12 &&
    Value(host, "EXTRAITEMCHOICES") == 4, "Reset after native loadout edits restores updated baselines");

host = Start();
Check(Stats("luck set 100"), "Prepare one-time set before native menu edit");
host.PlayerAvatar.customStats["LUCK"] += 3;
SessionSettings.Synchronize();
Check(Value(host) == 103 && Stats("luck reset") && Value(host) == 8, "Absolute set stays one-time; reset retains later native edits");

SessionBoundaryHooks.Install();
SessionBoundaryHooks.Install();
host = Start();
Check(Fountain("+100") && Stats("luck +10") && Choices("all 5"), "Prepare settings before a guest's first grant");
guest = Add(2, luck: 20, points: 9);
guest.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(guest.LastFountainAllowance == 109 && Value(guest) == 30 && Value(guest, "EXTRAWEAPONCHOICES") == 5,
    "Pre-grant synchronization inherits all settings for a ready guest once");
guest.PlayerAvatar.Inventory.canBroadcast = 0;
guest.PlayerAvatar.Inventory.dimensionPocket += 8;
guest.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(guest.LastFountainAllowance == 109, "Grant hook respects inventory initialization guard");
guest.PlayerAvatar.Inventory.canBroadcast = 1;
guest.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(guest.LastFountainAllowance == 117 && Value(guest) == 30, "Grant hook resumes after initialization without stacking stats");
DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] = 50;
guest.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(guest.LastFountainAllowance == 50, "Unchanged inputs preserve an independent cap replacement");
RestartLobby();
guest.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(guest.LastFountainAllowance == 117, "Run restart is reconciled before grant without LateUpdate");
SessionSettings.Stop();
DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] = 12;
guest.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(guest.LastFountainAllowance == 12, "Grant hook is inert after controller unload");
host = Start();
Check(Fountain("+100"), "Prepare guard authority check");
NetworkServer.active = false;
host.PlayerAvatar.Inventory.dimensionPocket += 8;
host.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(host.LastFountainAllowance == 104, "Grant hook never adjusts client-side state");
SessionBoundaryHooks.Uninstall();
SessionBoundaryHooks.Uninstall();
host = Start();
Check(Fountain("+100"), "Prepare hook removal check");
host.PlayerAvatar.Inventory.dimensionPocket += 8;
host.AddDimensionPocketItemsOnServer(Array.Empty<int>());
Check(host.LastFountainAllowance == 104, "Uninstall removes the grant hook");

host = Start();
Check(Fountain("20") && Stats("luck 11"), "Prepare rejected newcomer eligibility check");
guest = Add(2, points: 25);
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(Points(guest) == 25 && Value(guest, FountainPoints.ContributionKey) == 0 &&
    DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 20,
    "Rejected inheritance does not enroll a newcomer for Fountain cap maintenance");
host.PlayerAvatar.Inventory.dimensionPocket += 2;
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 22,
    "An enrolled player's native change excludes rejected players from cap planning");
Check(Fountain("25"), "Explicit Fountain command enrolls a previously rejected guest even with zero contribution");
guest.PlayerAvatar.Inventory.dimensionPocket += 3;
SessionSettings.Synchronize();
Check(DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 28,
    "Successful zero-contribution set remains eligible for later native capacity changes");

host = Start();
Check(Fountain("+10") && Stats("luck +10") && Choices("all 5"), "Configure all features before avatar replacement");
PlayerSpawner.MultiplayerList.Remove(host);
var reusedId = Add(1, luck: 20, points: 7, itemChoices: 2);
SessionSettings.Synchronize();
Check(Value(reusedId) == 30 && Points(reusedId) == 17 && Value(reusedId, "EXTRAITEMCHOICES") == 7,
    "A replaced avatar reusing a network ID inherits each family once against its own baseline");
SessionSettings.Synchronize();
Check(Value(reusedId) == 30 && Points(reusedId) == 17, "Replacement does not duplicate inheritance on later frames");

host = Start();
Check(Stats("luck +10"), "Prepare relative state before reentrant command callback");
bool nestedFlush = true;
host.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == "LUCK") nestedFlush = SessionSettings.Synchronize(); };
Check(Stats("luck +5") && !nestedFlush && Value(host) == 20, "Command mutation excludes recursive reconciliation");
host.PlayerAvatar.customStats.BeforeWrite = null;
SessionSettings.Synchronize();
Check(Value(host) == 20 && Stats("luck reset") && Value(host) == 5, "Reentrant callback cannot corrupt native baseline");

foreach (string family in new[] { "stats", "choices", "fountain" })
{
    host = Start();
    string marker = family == "stats" ? "SEPHIRIAONE_STAT_LUCK" : family == "choices" ? "SEPHIRIAONE_EXTRAITEMCHOICES" : FountainPoints.ContributionKey;
    host.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == marker) throw new InvalidOperationException("simulated marker failure"); };
    bool succeeded = family == "stats" ? Stats("luck +10") : family == "choices" ? Choices("item 5") : Fountain("+10");
    Check(!succeeded, family + " reports partial failure rather than throwing or recording success");
    Check(!Stats("luck +1"), family + " fault prevents another command from consuming partial state");
    host.PlayerAvatar.customStats.BeforeWrite = null;
    if (family == "stats") Check(!Stats("defense reset"), "An unrelated stat reset cannot finalize uncommitted luck intent");
    if (family == "choices") Check(!Choices("weapon reset"), "An unrelated category reset cannot finalize uncommitted item intent");
    bool recovered = family == "stats" ? Stats("reset") : family == "choices" ? Choices("reset") : Fountain("reset");
    Check(recovered && Value(host) == 5 && Points(host) == 4 && Value(host, "EXTRAITEMCHOICES") == 2,
        family + " explicit reset finishes tracked partial values then restores original baseline");
    var afterFault = Add(2, luck: 9, points: 7, itemChoices: 3);
    SessionSettings.Synchronize();
    Check(Value(afterFault) == 9 && Points(afterFault) == 7 && Value(afterFault, "EXTRAITEMCHOICES") == 3,
        family + " failed intent is not inherited or persisted");
}

host = Start();
guest = Add(2, luck: 7);
Check(Stats("luck +10") && Fountain("20"), "Prepare enrollment across multiple players before maintenance fault");
guest.PlayerAvatar.Inventory.dimensionPocket += 7;
SessionSettings.Synchronize();
host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
host.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == "SEPHIRIAONE_STAT_LUCK") throw new InvalidOperationException("maintenance marker failure"); };
SessionSettings.Synchronize();
host.PlayerAvatar.customStats.BeforeWrite = null;
Check(Stats("reset") && Points(guest) == 27, "Interrupted reconciliation does not prune connected guests or replay one-time settings");

host = Start();
Check(Fountain("+10") && Stats("luck +10") && Choices("all 5"), "Prepare cross-family inheritance failure");
guest = Add(2, luck: 9);
guest.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == "SEPHIRIAONE_STAT_LUCK") throw new InvalidOperationException("inheritance marker failure"); };
SessionSettings.Synchronize();
guest.PlayerAvatar.customStats.BeforeWrite = null;
Check(Choices("reset"), "Explicit reset recovers cross-family inheritance");
guest.PlayerAvatar.customStatsAmp["LUCK"] = 100;
SessionSettings.Synchronize();
Check(guest.PlayerAvatar.GetCustomStatUnsafe("LUCK") == 28, "Recovered inheritance enrolls relative stats even when a different family is reset");

host = Start();
host.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == FountainPoints.ContributionKey) throw new InvalidOperationException("inventory swap failure"); };
Check(!Fountain("+10"), "Prepare partial Fountain write before inventory replacement");
GridInventory previousInventory = host.PlayerAvatar.Inventory;
host.PlayerAvatar.Inventory = new GridInventory { netId = 1, dimensionPocket = 30 };
host.PlayerAvatar.customStats.BeforeWrite = null;
Check(!Fountain("reset") && Points(host) == 30 && previousInventory.dimensionPocket == 14,
    "Recovery cannot write a replaced inventory or apply its marker to a different lifetime");

host = Start();
Check(Fountain("+100"), "Prepare cap-only reconciliation failure");
host.PlayerAvatar.Inventory.dimensionPocket += 8;
DungeonManager.Instance.constValueDictionary.BeforeWrite = (key, value) =>
{ if (key == FountainPoints.AppliedLimitKey) throw new InvalidOperationException("cap marker failure"); };
bool capReady = false;
try { capReady = SessionSettings.EnsureFresh(); } catch { }
Check(!capReady && !Stats("luck +10"), "Partial cap writes share fault containment across command families");
Check(Mod(PresetAction.Status, out messages) && messages.Any(line => line.Contains("Write journal:")) &&
    messages.Any(line => line.Contains("Faulted")), "Status exposes coordinator outcome and before/target/readback journal");
Check(!Mod(PresetAction.Save, out _), "Partial writes cannot be saved as an apparently healthy preset");
DungeonManager.Instance.constValueDictionary.BeforeWrite = null;
Check(Fountain("reset") && Points(host) == 12 && DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] == 12,
    "Explicit Fountain reset safely recovers a partial cap-only write");
Check(SessionSettings.EnsureFresh(), "Successful cap recovery clears the coordinator fault and permits a fresh boundary");

host = Start();
host.PlayerAvatar.customStats.BeforeWrite = (key, value) =>
{ if (key == "LUCK") host.PlayerAvatar.customStatsAmp["LUCK"] = 100; };
Check(!Stats("luck +10"), "Native multiplier mutation during a command fails post-write validation");
host.PlayerAvatar.customStats.BeforeWrite = null;
Check(!Choices("item 5"), "Unverified stat write blocks other families until explicit recovery");
host.PlayerAvatar.customStatsAmp.Remove("LUCK");
Check(Stats("reset") && Value(host) == 5, "Recovering validated inputs restores a partial stat command baseline");

host = Start();
Check(Stats("luck +10") && Choices("all 5"), "Prepare shared candidate generation boundary");
guest = Add(2, luck: 9, itemChoices: 4);
SessionSettings.BeforeNativeRead("Candidate generation");
Check(Value(guest) == 19 && Value(guest, "EXTRAITEMCHOICES") == 9, "Candidate generation flushes newcomer inheritance before the frame tick");
SessionSettings.BeforeNativeRead("Candidate generation");
Check(Value(guest) == 19 && Value(guest, "EXTRAITEMCHOICES") == 9, "Repeated generation boundaries do not stack settings");
ChoicePoints.RemoveContributions();
Check(Value(guest, "EXTRAITEMCHOICES") == 4 && Value(host, "EXTRAITEMCHOICES") == 2,
    "Verified candidate unload cleanup preserves each native baseline");
ChoicePoints.RemoveContributions();
Check(Value(guest, "EXTRAITEMCHOICES") == 4, "Candidate cleanup is idempotent");

foreach (bool useCommand in new[] { false, true })
{
    host = Start();
    Check(Choices("all 5"), "Prepare candidate cleanup fault");
    host.PlayerAvatar.customStats.BeforeRemove = key => { if (key == "SEPHIRIAONE_EXTRAITEMCHOICES") throw new InvalidOperationException("cleanup marker failure"); };
    bool failedCleanup = false;
    try { ChoicePoints.RemoveContributions(); } catch (InvalidOperationException) { failedCleanup = true; }
    Check(failedCleanup && !Stats("luck +10"), "Partial cleanup retains journal and blocks gameplay commands");
    host.PlayerAvatar.customStats.BeforeRemove = null;
    if (useCommand) Check(Choices("reset") && Choices("item 3"), "Reset recovers cleanup and permits fresh intent");
    ChoicePoints.RemoveContributions();
    Check(Value(host, "EXTRAITEMCHOICES") == 2 && Value(host, "SEPHIRIAONE_EXTRAITEMCHOICES") == 0,
        "Cleanup retry/reset uses original journal and never subtracts the addon twice");
}

SettingsControlsTests.Run(Check, Start, Add);
MultiplierRuntimeTests.Run(Check, Start, Add);
ResourceRuntimeTests.Run(Check, Start, Add);
ReentryRuntimeTests.Run(Check, Start, Add);
Check(SettingsActions.IsCommand("/resources slots +6"), "Resource command family is recognized by shared controls");

if (Directory.Exists(testDataRoot)) Directory.Delete(testDataRoot, true);
Console.WriteLine($"Passed {checks} runtime command/session integration checks using game API fixtures.");
