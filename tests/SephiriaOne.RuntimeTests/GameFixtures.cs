// Minimal game API boundary for exercising the actual runtime command services.
// These are data fixtures, not a Unity/Mirror transport or lifecycle simulation.
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => !ReferenceEquals(value, null);
    }
    public class MonoBehaviour : Object { }
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
}

public sealed class PlayerAvatar : UnityEngine.Object
{
    public bool isServer = true;
    public uint netId;
    public UnityEngine.Object Race = new();
    public string playerNameSource = "Player";
    public string currentFloorGuid = "town";
    public GridInventory Inventory = new();
    public readonly Dictionary<string, int> customStats = new();
    public readonly Dictionary<string, int> calculatedBonusStats = new();
    public readonly Dictionary<string, int> customStatsAmp = new();
    public int GetCustomStatUnsafe(string key) =>
        (int)((float)((customStats.GetValueOrDefault(key) + calculatedBonusStats.GetValueOrDefault(key)) *
            (100 + customStatsAmp.GetValueOrDefault(key))) / 100f);
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
    public readonly Dictionary<string, int> constValueDictionary = new() { ["DIMENSIONPOCKETLIMIT"] = 12 };
}

public sealed class GameLogWriter : UnityEngine.Object
{
    public static GameLogWriter Instance;
    public void WriteLog(string message, UnityEngine.Color color) { }
}

namespace SephiriaOne
{
    internal static class ChoiceFeature { public static bool Available = true; }
}
