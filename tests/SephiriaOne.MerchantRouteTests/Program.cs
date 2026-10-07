using System.Reflection;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
FloorGenerator Normal(EFloorThreatType threat = EFloorThreatType.Battle) => new EnhancedProceduralFloorGenerator { floorThreatType = threat };
StageEntity_Choice.EachStep Step(EFloorThreatType threat) => new() { possibleThreats = [threat], possibleMainEvents = [EFloorMainEventType.EXP] };
StageEntity_Choice Choice(string name) => new()
{
    name = name, firstFloor = Normal(), steps = [Step(EFloorThreatType.Battle), Step(EFloorThreatType.Boss)],
    randomEventFloorPrefabs = [Normal().gameObject]
};
FloorData Data(string stage, int depth, string guid = "", string mission = "") => new() { stageName = stage, nodeProgress = depth, guid = guid, questBoardEventId = mission };
DungeonManager Dungeon(params StageEntity[] stages) => new() { Race = new() { stages = stages } };

var first = Choice("Moleland");
var later = Choice("Library"); later.firstFloor.isSafeFloor = true;
var dungeon = Dungeon(first, later); var save = new SaveData(); var route = new Route(dungeon, save);
Check(route.LatestPosition == -1, "Route planning itself does not claim any floor was visited");
Check(route.Opportunities.Count == 3, "Playable first floor and normal steps across stages are scheduled; safe entrances and bosses are excluded");
Check(route.Opportunities.SequenceEqual(route.Opportunities.Order()), "Opportunity positions are sorted");
Check(route.Opportunities.Contains(route.Position(Data("Moleland", 0))), "First normal floor remains eligible for the random draw");
Check(route.Position(Data("Moleland", 1, "branch-a")) == route.Position(Data("Moleland", 1, "branch-b")), "Alternative GUIDs at one depth share a route position");
Check(route.Position(Data("Moleland", 2)) > route.Position(Data("Moleland", 1)) &&
      route.Position(Data("Moleland", 2)) < route.Position(Data("Library", 0)), "Boss and safe positions retain progression between stages");
Check(route.Position(Data("side", 0)) == -1 && route.Position(Data("Moleland", -1)) == -1 && route.Position(Data("Moleland", 99)) == -1,
    "Unknown stages and depths cannot advance the schedule");
var hidden = Data("Moleland", 1); hidden.isHidden = true;
var pocket = Data("Moleland", 1); pocket.pocketDimension = true;
Check(route.Position(hidden) == -1 && route.Position(pocket) == -1, "Hidden and pocket floors are excluded");
Check(route.LatestPosition == route.Position(Data("Library", 0)), "Older callbacks and excluded floors cannot lower the observed progress watermark");
Check(route.FloorNumber(route.Position(Data("Moleland", 0))) == 1 &&
    route.FloorNumber(route.Position(Data("Moleland", 2))) == 1 &&
    route.FloorNumber(route.Position(Data("Library", 0))) == 2 &&
    route.FloorNumber(route.Position(Data("Library", 1))) == 2 && route.FloorNumber(-1) == 0,
    "Several maps in stage 1 must not advance the minimum-floor or HP multiplier to stage 2 or 3");

dungeon.Race.stages = [first, first, later]; dungeon.Race.lobbyStage = first;
route = new Route(dungeon, save);
Check(route.Opportunities.Count == 3, "Playable lobby and repeated main-stage references are counted once");
Check(route.FloorNumber(route.Position(Data("Library", 1))) == 1,
    "A lobby repeated in the main-stage lookup array cannot shift the first newly loaded main stage");
dungeon.Race.lobbyStage = new StageEntity_Lobby { name = "Town", firstFloor = new FixedFloorGenerator { isSafeFloor = true } };
dungeon.Race.sideStages = [Choice("Side")];
route = new Route(dungeon, save);
Check(route.Opportunities.Count == 3 && route.Position(Data("Side", 0)) == -1 && route.Position(Data("Town", 0)) == -1,
    "Safe lobby and optional side stages do not enter the finite plan");
Check(route.FloorNumber(route.Position(Data("Library", 1))) == 2,
    "Repeated main-stage references must not advance native stage numbering twice");
dungeon.Race.stages = [first, null, later];
route = new Route(dungeon, save);
Check(route.FloorNumber(route.Position(Data("Library", 1))) == 2, "Missing stage entries do not add phantom stage numbers");
route = new Route(Dungeon(new UnknownStage { name = "Custom", firstFloor = Normal() }), new());
Check(route.Opportunities.Count == 0 && route.Position(Data("Custom", 0)) == -1, "Unknown stage generators fail closed");
Check(new Route(Dungeon(new CustomChoiceStage { name = "CustomChoice", firstFloor = Normal() }), new()).Opportunities.Count == 0,
    "Unknown subclasses cannot inherit scheduling assumptions from a native generator");
route = new Route(new DungeonManager(), new());
Check(route.Opportunities.Count == 0, "Missing race data does not force a first-floor guarantee");

var mixed = Choice("Mixed"); mixed.firstFloor = new SingleRoomFloorGenerator();
mixed.steps = [Step(EFloorThreatType.HardBattle), new() { matchEvents = [new() { threat = EFloorThreatType.Peace, ev = EFloorMainEventType.Anvil, fixedPool = true }] }, Step(EFloorThreatType.Battle)];
mixed.fixedEventFloorPrefabs = [new FixedFloorGenerator { floorThreatType = EFloorThreatType.Peace, floorMainEventType = EFloorMainEventType.Anvil }.gameObject];
route = new Route(Dungeon(mixed), new());
Check(route.Opportunities.Count == 2 && !route.Opportunities.Contains(route.Position(Data("Mixed", 1))),
    "Step threats require a matching native prefab pool; supported fixed event pools remain eligible");
mixed.fixedEventFloorPrefabs[0].Floor.isTrainingFloor = true;
mixed.randomEventFloorPrefabs = [new FullyDesignedFloorGenerator { floorThreatType = EFloorThreatType.Battle }.gameObject];
route = new Route(Dungeon(mixed), new());
Check(route.Opportunities.Count == 0, "Training and unsupported generators cannot create phantom opportunities");
var enhanced = Choice("Enhanced"); enhanced.firstFloor.isSafeFloor = true;
enhanced.randomEventFloorPrefabs = [Normal(EFloorThreatType.HardBattle).gameObject];
dungeon = Dungeon(enhanced); dungeon.Race.enhancedDisturbance = true;
Check(new Route(dungeon, new()).Opportunities.Count == 1, "Enhanced disturbance resolves battle slots through the hard-battle pool");
enhanced.randomEventFloorPrefabs = [Normal().gameObject, new FullyDesignedFloorGenerator { floorThreatType = EFloorThreatType.BattleFloor }.gameObject];
enhanced.battleZoneFloorChance = 1; dungeon.Race.enhancedDisturbance = false;
Check(new Route(dungeon, new()).Opportunities.Count == 0, "A mandatory unsupported battle-floor replacement cannot leave a phantom normal-depth candidate");
enhanced.randomEventFloorPrefabs = [Normal().gameObject];
Check(new Route(dungeon, new()).Opportunities.Count == 1, "Battle-floor replacement requires an available native replacement pool");
var infinity = new StageEntity_Infinity { name = "FiniteTemplate", firstFloor = Normal(), steps = [new() { possibleThreats = [EFloorThreatType.Battle], possibleMainEvents = [EFloorMainEventType.EXP] }], randomEventFloorPrefabs = [Normal().gameObject] };
Check(new Route(Dungeon(infinity), new()).Opportunities.Count == 2, "Infinity's installed finite step template is counted without generating native floors");

var board = new StageEntity_GrasslandTown { name = "Grassland", firstFloor = new FullyDesignedFloorGenerator { isSafeFloor = true }, nodeEvents = [new() { floorPrefab = Normal() }] };
dungeon = Dungeon(board, Choice("AfterBoard")); save = new(); route = new(dungeon, save);
Check(route.Opportunities.Count == 5, "Three required board visits are scheduled, not all six offered missions");
var missionA = Data("Grassland", 6, "mission-a", "FindWood");
var missionB = Data("Grassland", 1, "mission-b", "Defense");
var missionC = Data("Grassland", 4, "mission-c", "Miniboss");
int a = route.Position(missionA), b = route.Position(missionB), c = route.Position(missionC);
Check(a < b && b < c, "Board missions follow arrival order instead of their randomly assigned nodeProgress");
Check(route.FloorNumber(a) == 1 && route.FloorNumber(b) == 1 && route.FloorNumber(c) == 1,
    "Board mission visits have separate schedule positions but share one stage HP/eligibility number");
Check(route.Position(missionA) == a && new Route(dungeon, save).Position(missionB) == b, "Revisits and run reloads preserve saved board ordinals");
Check(route.Position(Data("Grassland", 1, "board-boss")) > c, "Board boss position follows all required mission visits");
Check(!route.Opportunities.Contains(route.Position(Data("Grassland", 2, "extra", "Extra"))), "Unexpected extra board missions cannot introduce new scheduled opportunities");
Check(route.Position(Data("Grassland", 3, "", "Mission")) == -1, "A board mission without a stable GUID cannot consume an ordinal");

save = new(); PlayerSpawner.MultiplayerList.Clear();
dungeon.generatedFloors[missionA.guid] = missionA; dungeon.generatedFloors[missionB.guid] = missionB; dungeon.generatedFloors[missionC.guid] = missionC;
var host = new PlayerAvatar { isLocalPlayer = true }; host.floorTravelHistory.AddRange([missionA.guid, missionB.guid, missionA.guid]);
var guest = new PlayerAvatar(); guest.floorTravelHistory.AddRange([missionB.guid, missionC.guid]);
PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = guest }); PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = host });
route = new(dungeon, save); route.ObserveHistory();
Check(route.Position(missionA) < route.Position(missionB) && route.Position(missionB) < route.Position(missionC),
    "History recovery respects host chronology before recovering missing guest visits and ignores duplicates");
route.ObserveHistory();
Check(route.Position(missionC) == new Route(dungeon, save).Position(missionC), "Repeated history recovery never rerolls board positions");
var laterHistory = Data("AfterBoard", 1, "later-depth"); dungeon.generatedFloors[laterHistory.guid] = laterHistory;
host.floorTravelHistory.Add(laterHistory.guid); route.ObserveHistory();
Check(route.LatestPosition == route.Position(laterHistory) && route.LatestPosition > route.Position(missionA),
    "History recovery also advances ordinary-stage progress without allowing older board callbacks to move it backward");
PlayerSpawner.MultiplayerList.Clear();
// Exercise the real route together with scheduling and spawn conditions. The
// isolated runtime fixture's one-map-per-stage shortcut cannot catch this bug.
foreach (var definition in SephiriaOne.MerchantCatalog.All)
for (int seed = 0; seed < 40; seed++)
{
    var multiMap = Choice("Stage1"); multiMap.steps = [Step(EFloorThreatType.Battle), Step(EFloorThreatType.Battle), Step(EFloorThreatType.Boss)];
    var mainRoute = new SephiriaOne.MerchantRoute(Dungeon(multiMap, Choice("Stage2")), new SaveData());
    var runState = new SaveData();
    var schedule = new SephiriaOne.MerchantSchedule(runState, mainRoute, seed, definition);
    schedule.Advance(mainRoute.Position(Data("Stage1", 0)), firstFloor: 2);
    int target = runState.GetInt(schedule.State.SchedulePrefix + "Target", -1);
    Check(mainRoute.FloorNumber(target) == 2, "A guarantee with from 2 must never select a later map in stage 1");
    Check(!schedule.HasPendingOpportunity(3), "A nonexistent third main stage must not reserve a cap slot");
    var settings = new SephiriaOne.MerchantSettings(true, 100, 2);
    for (int depth = 0; depth < 3; depth++)
    {
        int position = mainRoute.Position(Data("Stage1", depth)); schedule.Advance(position, 2);
        var context = new SephiriaOne.MerchantSpawnContext(mainRoute.FloorNumber(position), 0, 0, "Stage1");
        Check(!schedule.IsDue(position) && !SephiriaOne.MerchantSpawnRules.Allows(definition, settings, context),
            "Neither guaranteed nor chance merchants may pass from 2 in any stage-1 map");
    }
    schedule.Advance(mainRoute.Position(Data("Stage2", 0)), 2);
    Check(runState.GetInt(schedule.State.SchedulePrefix + "Target", -1) == target,
        "Progress and setting refreshes preserve an existing selected map");
    schedule.Advance(mainRoute.LatestPosition, 3, false);
    var reloaded = new SephiriaOne.MerchantSchedule(runState, mainRoute, seed, definition);
    reloaded.Advance(mainRoute.LatestPosition, 2);
    Check(runState.GetInt(schedule.State.SchedulePrefix + "Target", -1) == target,
        "Minimum edits, guarantee toggles and runtime reconstruction cannot reroll the saved target");
    int last = mainRoute.Position(Data("Stage2", 1)); reloaded.Advance(last, 2);
    Check(reloaded.IsDue(last), "A target delayed by settings remains due on an unused eligible map");
    reloaded.State.RecordSpawn(true);
    Check(!new SephiriaOne.MerchantSchedule(runState, mainRoute, seed, definition).IsDue(last),
        "Completed guarantees remain completed after reload");
}
var mainAfterUnknown = new SephiriaOne.MerchantRoute(Dungeon(
    new UnknownStage { name = "Unknown", firstFloor = Normal() }, Choice("Known")), new SaveData());
Check(mainAfterUnknown.FloorNumber(mainAfterUnknown.Position(Data("Known", 0))) == 2,
    "Unsupported main stages still occupy their native stage ordinal");
Console.WriteLine($"Passed {checks} native merchant route checks.");

// Reflection keeps the initial missing-adapter test executable before production exists.
sealed class Route
{
    private readonly object value;
    private readonly Type type;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal Route(DungeonManager dungeon, SaveData save)
    {
        type = Assembly.GetExecutingAssembly().GetType("SephiriaOne.MerchantRoute") ?? throw new Exception("MerchantRoute adapter is not implemented.");
        value = Activator.CreateInstance(type, Flags, null, [dungeon, save], null);
    }
    internal IReadOnlyList<int> Opportunities => (IReadOnlyList<int>)type.GetProperty("Opportunities", Flags).GetValue(value);
    internal int LatestPosition => (int)type.GetProperty("LatestPosition", Flags).GetValue(value);
    internal int Position(FloorData data) => (int)type.GetMethod("Position", Flags).Invoke(value, [data]);
    internal int FloorNumber(int position) => (int)type.GetMethod("FloorNumber", Flags).Invoke(value, [position]);
    internal void ObserveHistory() => type.GetMethod("ObserveHistory", Flags).Invoke(value, null);
}
