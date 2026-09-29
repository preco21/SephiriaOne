namespace UnityEngine
{
    public class Object { public string name; public static implicit operator bool(Object value) => value != null; }
    public class GameObject : Object
    {
        public FloorGenerator Floor;
        public T GetComponent<T>() where T : class => Floor as T;
    }
}
namespace Mirror
{
    public class SyncList<T> : List<T> { }
    public class SyncDictionary<K, V> : Dictionary<K, V> { }
}
public enum EFloorThreatType { Unknown, Peace, UnknownBattle, Battle, HardBattle, MiniBoss, Boss, QliphothScenario, BattleFloor }
public enum EFloorMainEventType { None, Unknown, Money, EXP, HP, Merchant, Miracle, Charm, StoneTablet, Enchant, RandomEncounter, Anvil }
public class FloorGenerator : UnityEngine.Object
{
    public bool isSafeFloor, isTrainingFloor;
    public EFloorThreatType floorThreatType;
    public EFloorMainEventType floorMainEventType;
    public UnityEngine.GameObject gameObject => new() { Floor = this };
}
public class EnhancedProceduralFloorGenerator : FloorGenerator { }
public class LibraryFloorGenerator : FloorGenerator { }
public class FixedFloorGenerator : FloorGenerator { }
public class SingleRoomFloorGenerator : FloorGenerator { }
public class FullyDesignedFloorGenerator : FloorGenerator { }
public class StageEntity : UnityEngine.Object { public FloorGenerator firstFloor; }
public class UnknownStage : StageEntity { }
public class StageEntity_Choice : StageEntity
{
    public struct EFloorNodeType { public EFloorThreatType threat; public EFloorMainEventType ev; public bool fixedPool, essential; }
    public class EachStep
    {
        public int choiceMin = 1, choiceMax = 3;
        public EFloorNodeType[] matchEvents = [];
        public EFloorThreatType[] possibleThreats = [];
        public EFloorMainEventType[] possibleMainEvents = [];
    }
    public EachStep[] steps = [];
    public UnityEngine.GameObject[] randomEventFloorPrefabs = [], fixedEventFloorPrefabs = [];
    public float battleZoneFloorChance;
}
public class CustomChoiceStage : StageEntity_Choice { }
public class StageEntity_Infinity : StageEntity
{
    public struct EFloorNodeType { public EFloorThreatType threat; public EFloorMainEventType ev; public bool fixedPool, essential; }
    public class EachStep
    {
        public int choiceMin = 1, choiceMax = 3;
        public EFloorNodeType[] matchEvents = [];
        public EFloorThreatType[] possibleThreats = [];
        public EFloorMainEventType[] possibleMainEvents = [];
    }
    public EachStep[] steps = [];
    public UnityEngine.GameObject[] randomEventFloorPrefabs = [], fixedEventFloorPrefabs = [];
}
public class GrasslandTownEventEntity : UnityEngine.Object { public FloorGenerator floorPrefab; }
public class StageEntity_GrasslandTown : StageEntity
{
    public int eventCount = 6, eventCountToClear = 3;
    public GrasslandTownEventEntity[] nodeEvents = [];
}
public class StageEntity_Lobby : StageEntity { }
public class RaceEntity : UnityEngine.Object { public StageEntity lobbyStage; public StageEntity[] stages = [], sideStages = []; public bool enhancedDisturbance; }
public class DungeonManager : UnityEngine.Object
{
    public RaceEntity Race;
    public readonly Mirror.SyncDictionary<string, FloorData> generatedFloors = new();
}
public class FloorData
{
    public string guid, stageName, questBoardEventId;
    public int nodeProgress;
    public bool isHidden, pocketDimension;
}
public class PlayerAvatar : UnityEngine.Object
{
    public bool isLocalPlayer;
    public readonly Mirror.SyncList<string> floorTravelHistory = new();
}
public class PlayerSpawner : UnityEngine.Object
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public PlayerAvatar PlayerAvatar;
}
public class SaveData
{
    private readonly Dictionary<string, int> values = new();
    public int GetInt(string key, int fallback = 0) => values.TryGetValue(key, out int value) ? value : fallback;
    public void SetInt(string key, int value) => values[key] = value;
}
