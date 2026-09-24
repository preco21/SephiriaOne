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
    public readonly struct Color
    {
        public static readonly Color green = new();
        public static readonly Color yellow = new();
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static void Log(object message) { }
        public static void LogWarning(object message) => Warnings.Add(message.ToString());
        public static void LogError(object message) => throw new Exception(message.ToString());
    }
}

namespace Mirror
{
    public static class NetworkServer { public static bool active; }
    public sealed class NetworkConnectionToClient { public bool isReady = true; }
}

public sealed class PlayerSpawner : UnityEngine.Object
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public bool isServer = true;
    public uint netId;
    public Mirror.NetworkConnectionToClient connectionToClient = new();
    public PlayerAvatar PlayerAvatar = new();
    public int LastFountainAllowance;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void AddDimensionPocketItemsOnServer(int[] items) => LastFountainAllowance =
        Math.Min(PlayerAvatar.Inventory.dimensionPocket, DungeonManager.Instance.constValueDictionary["DIMENSIONPOCKETLIMIT"]);
}

public sealed class PlayerAvatar : UnityEngine.Object
{
    public bool isServer = true;
    public uint netId;
    public UnityEngine.Object Race = new();
    public string playerNameSource = "Player";
    public string currentFloorGuid = "town";
    public GridInventory Inventory = new();
    public int maxPassivePoint = 5;
    public readonly FixtureStats customStats = new();
    public readonly Dictionary<string, int> calculatedBonusStats = new();
    public readonly Dictionary<string, int> customStatsAmp = new();
    public int GetCustomStatUnsafe(string key) =>
        (int)((float)((customStats.GetValueOrDefault(key) + calculatedBonusStats.GetValueOrDefault(key)) *
            (100 + customStatsAmp.GetValueOrDefault(key))) / 100f);
}

public sealed class FixtureStats : Dictionary<string, int>, IDictionary<string, int>
{
    public Action<string, int> BeforeWrite;
    public Action<string> BeforeRemove;
    public int Writes;
    public new int this[string key]
    {
        get => base[key];
        set { BeforeWrite?.Invoke(key, value); Writes++; base[key] = value; }
    }
    int IDictionary<string, int>.this[string key] { get => this[key]; set => this[key] = value; }
    public new bool Remove(string key) { BeforeRemove?.Invoke(key); return base.Remove(key); }
    bool IDictionary<string, int>.Remove(string key) => Remove(key);
}

public sealed class GridInventory : UnityEngine.Object
{
    public bool isServer = true;
    public uint netId;
    public int canBroadcast = 1;
    public int dimensionPocket;
    public int NetworkdimensionPocket { set => dimensionPocket = value; }
}

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
    internal static class MultiplayerNameController { public static string Diagnostics => "fixture: rendering unverified"; }
}
