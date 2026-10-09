// Minimal game API boundary for exercising the actual runtime command services.
// These are data fixtures, not a Unity/Mirror transport or lifecycle simulation.
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => !ReferenceEquals(value, null);
    }
    public class MonoBehaviour : Object { }
    public static class Application { public static string persistentDataPath; }
    public static class Time { public static float unscaledTime; }
    public readonly struct Color
    {
        public static readonly Color green = new();
        public static readonly Color yellow = new();
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static readonly List<string> Errors = new();
        public static void Log(object message) { }
        public static void LogWarning(object message) => Warnings.Add(message.ToString());
        public static void LogError(object message) => Errors.Add(message.ToString());
    }
}

namespace Mirror
{
    public static class NetworkServer { public static bool active; }
    public static class NetworkClient { public static bool active = true, ready = true; }
    public sealed class NetworkConnectionToClient { public bool isReady = true; }
}

public sealed class PlayerSpawner : UnityEngine.Object
{
    public ulong steamID;
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public bool isServer = true;
    public uint netId;
    public Mirror.NetworkConnectionToClient connectionToClient = new();
    public PlayerAvatar PlayerAvatar = new();
    public PlayerLocalDataStorage LocalDataStorage = new();
    public string playerGuid = "fixture-player";
    public int currentPlayerIdxForSave;
    public int LastFountainAllowance;
    public int RestoredSlots;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private bool Initialize(int weaponId, string costumeName, string skinID, int loadingScreen)
    {
        RestoredSlots = 0;
        for (int i = 0; i < PlayerAvatar.Inventory.CurrentInventoryStorage; i++) RestoredSlots++;
        return true;
    }
    public void RestoreInventory() => Initialize(0, "", "", 0);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void SaveCurrentSessionData() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void AddDimensionPocketItemsOnServer(int[] items) => LastFountainAllowance =
        Math.Min(PlayerAvatar.Inventory.dimensionPocket, DungeonManager.Instance.constValueDictionary["DIMENSIONPOCKETLIMIT"]);
}

public sealed partial class PlayerAvatar : UnitAvatar
{
    public bool IsDead;
    public bool isClient = true;
    public readonly List<string> NameRequests = new();
    public void SetPlayerName(string value) => NameRequests.Add(value);
    public bool isServer = true;
    public uint netId;
    public UnityEngine.Object Race = new();
    public string playerNameSource = "Player";
    public string currentFloorGuid = "town";
    public GridInventory Inventory = new();
    public int maxPassivePoint = 5;
    public int NetworkmaxPassivePoint { set => maxPassivePoint = value; }
    public readonly Dictionary<ulong, int> passiveStats = new();
    public struct PassiveStatSaveData { public ulong id; public int point; }
    public PlayerSpawner spawner;
    public PlayerLocalDataStorage localDataStorage;
    public bool isOwned;
    public int maxRerollDice = 3, rerollDice = 3, currentMoney, StartingLeaves = 100;
    public readonly FixtureStats customStats = new();
    public readonly Dictionary<string, int> calculatedBonusStats = new();
    public readonly Dictionary<string, int> customStatsAmp = new();
    public Action<string> BeforeRead;
    public int GetCustomStatUnsafe(string key)
    {
        BeforeRead?.Invoke(key);
        return (int)((float)((customStats.GetValueOrDefault(key) + calculatedBonusStats.GetValueOrDefault(key)) *
            (100 + customStatsAmp.GetValueOrDefault(key))) / 100f);
    }
}

public sealed class FixtureStats : Dictionary<string, int>, IDictionary<string, int>
{
    public Action<string, int> BeforeWrite;
    public Action<string, int> AfterWrite;
    public Action<string> BeforeRemove;
    public int Writes;
    public new int this[string key]
    {
        get => base[key];
        set { BeforeWrite?.Invoke(key, value); Writes++; base[key] = value; AfterWrite?.Invoke(key, value); }
    }
    int IDictionary<string, int>.this[string key] { get => this[key]; set => this[key] = value; }
    public new bool Remove(string key) { BeforeRemove?.Invoke(key); return base.Remove(key); }
    bool IDictionary<string, int>.Remove(string key) => Remove(key);
}

public sealed class GridInventory : UnityEngine.Object
{
    public byte Width = 6;
    public short CurrentInventoryStorage = 24;
    public readonly Dictionary<ItemPosition, NewItemOwnInstance> inventoryMatrix = new();
    public readonly List<ItemPosition> mysticPositions = new();
    public readonly List<object> engravings = new(), fixedEngravingsOnServer = new();
    public void AddStorage(short amount) => CurrentInventoryStorage += amount;
    public struct ItemDropBonusData { public string categoryName; public float weight; }
    public bool isServer = true;
    public uint netId;
    public int canBroadcast = 1;
    public int dimensionPocket;
    public int NetworkdimensionPocket { set => dimensionPocket = value; }
}

public struct ItemPosition { public sbyte x, y; }
public sealed class NewItemOwnInstance { }
public sealed class UIManager : UnityEngine.Object
{
    public static UIManager Instance;
    public T GetElement<T>() where T : new() => new T();
}
public sealed class UI_NewItemPicker : UnityEngine.Object { public bool CurrentAny; }
public sealed class UI_NewItemPicker_Controller : UnityEngine.Object { public bool CurrentAny; }

public sealed class PlayerLocalDataStorage : UnityEngine.Object
{
    public bool preparingUIThings, doingSomeUIThings;
    public int adaptiveItemDropBonus;
    public readonly List<GridInventory.ItemDropBonusData> fruitSkewerBonus = new();
}
public sealed class PassiveEntity : UnityEngine.Object { public ulong id; public int maxLevel = 100; }
public static class PassiveDatabase
{
    public static readonly List<PassiveEntity> All = new();
    public static IEnumerable<PassiveEntity> GetAll() => All;
}
public static class KeywordDatabase
{
    public static int FruitDefault = 6;
    public static int GetConstValue(string key) => key == "fruitSkewerDefaultCount" ? FruitDefault : 0;
}
public sealed class SaveData
{
    public readonly Dictionary<string, object> Values = new();
    public bool ContainsKey(string key) => Values.ContainsKey(key);
    public int GetInt(string key, int fallback) => Values.TryGetValue(key, out var value) ? (int)value : fallback;
    public string GetString(string key, string fallback) => Values.TryGetValue(key, out var value) ? (string)value : fallback;
    public void SetInt(string key, int value) => Values[key] = value;
    public void SetString(string key, string value) => Values[key] = value;
}
public static class SaveManager { public static SaveData CurrentRun = new(), Current = new(); }

public sealed class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance;
    public bool isServer = true;
    public uint netId = 100;
    public readonly FixtureStats constValueDictionary = new() { ["DIMENSIONPOCKETLIMIT"] = 12 };
}

public sealed class GameLogWriter : UnityEngine.Object
{
    public static GameLogWriter Instance;
    public void WriteLog(string message, UnityEngine.Color color) { }
}

public static class HorayModAPI
{
    public static event Action<bool> OnStartSessionServerside;
    public static void StartSession(bool isSaved = false) => OnStartSessionServerside?.Invoke(isSaved);
}

namespace SephiriaOne
{
    internal static class ChoiceFeature { public static bool Available = true; }
    internal static class SessionBoundaryFeature { public static bool Available = true; }
    internal static class ResourceFeature
    {
        public static bool Available = true;
        public static bool IsAvailable(ResourceKind kind) => Available;
        public static string UnavailableReason(ResourceKind kind) => "Fixture guard unavailable.";
    }
    // Grant-hook behavior is exercised separately with the actual hook code.
    internal static class StartingResourceHooks
    {
        public static int NativeDice(PlayerAvatar player) => player.maxRerollDice - player.customStats.GetValueOrDefault(ResourceCatalog.Get(ResourceKind.Dice).Marker);
        public static int NativeLeaves(PlayerAvatar player) => player.StartingLeaves;
    }
    internal static class MultiplayerNameController { public static string Diagnostics => "fixture: rendering unverified"; }
}
