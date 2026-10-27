using UnityEngine;

// Only external Unity/native calls are simulated; lifecycle and placement run
// the production classes. Installed-game contracts test the separate adapter.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object o) => o is not null && !o.Destroyed;
    }
    public class GameObject : Object { public readonly Transform transform = new(); }
    public class Transform { public Vector3 position; }
    public readonly struct Vector2(float x, float y)
    {
        public readonly float x = x, y = y;
        public static implicit operator Vector3(Vector2 v) => new(v.x, v.y);
    }
    public readonly struct Vector3(float x, float y, float z = 0)
    {
        public readonly float x = x, y = y, z = z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size) { }
        public void Encapsulate(Bounds other) { }
        public bool Intersects(Bounds other) => true;
    }
    public static class Time { public static int frameCount; }
    public static class Random { public static int Range(int min, int max) => 12; }
    public static class Debug { public static readonly List<string> Warnings = new(); public static void LogWarning(string text) => Warnings.Add(text); }
}
namespace Mirror
{
    public static class NetworkServer { public static bool active = true; public static readonly object localConnection = new(); }
    public static class NetworkClient { public static bool active = true; }
}
public sealed class PathGrid : UnityEngine.Object
{
    public static PathGrid Current = new(); public bool IsBuilt = true;
    public int Width => 100; public int Height => 100; public float CellSize => 1; public Vector2 Center => new(50, 50);
    public int Updates; public bool Fail;
    public void UpdateRegion(Bounds b) { if (Fail) throw new Exception("path update failed"); Updates++; }
}
public sealed class FloorData { public string stageName = "Lobby"; }
public class FloorGenerator : UnityEngine.Object
{
    public static readonly Dictionary<string, FloorGenerator> Floors = new();
    public static FloorGenerator FindByGuid(string guid) => Floors.GetValueOrDefault(guid);
    public string guid; public bool GenerateSuccess = true, isServer = true;
    public FloorData DataOnServer = new(); public readonly List<GameObject> floorRelatedNetworkObjects = new();
}
public sealed class TileFloorGenerator : FloorGenerator { public GameObject ground = new(); }
public sealed class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance = new(); public bool isServer = true;
    public readonly Dictionary<string, FloorData> generatedFloors = new();
}
public static class SaveManager { public static object CurrentRun = new(); }
public sealed class Stage : UnityEngine.Object { public string name = "Lobby"; }
public sealed class Race : UnityEngine.Object { public Stage lobbyStage = new(); }
public sealed class PlayerAvatar : UnityEngine.Object { public string currentFloorGuid = "A"; public int loadingScreenType = -1; public Race Race = new(); }
public sealed class PlayerSpawner : UnityEngine.Object
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public PlayerAvatar PlayerAvatar = new(); public object connectionToClient = Mirror.NetworkServer.localConnection;
}
namespace SephiriaOne
{
    internal static class SessionSettings
    {
        internal static Settings FriendlyFireForDisplay = new(); internal static long ResourceGeneration;
        internal sealed class Settings { public bool Enabled = true; public PvpCoverSettings Cover = new(true); }
    }
    internal static class CombatMatchRuntime { internal static bool IsRunning; internal static long MatchToken; }
    internal static class PvpCoverFeature { internal static bool Available = true; }
    internal static class FriendlyFireFeature { internal static bool Available = true; }
    internal static class ReviveAllAction { internal static bool Open = true; internal static bool IsRunOpen(DungeonManager dungeon, object run) => Open; }
    internal sealed class PvpCoverRooms
    {
        internal static int Probes; internal static bool Reject;
        internal readonly PvpCoverPlacement Plan = new(new() { new(0, 0, 100, 100) }, 1);
        internal PvpCoverRooms(TileFloorGenerator floor, bool lobby, int salt) { }
        internal bool Safe(float x, float y) { Probes++; return !Reject; }
    }
    internal static class PvpCoverNative
    {
        internal static int Spawns, Removes, PrefabCalls; internal static bool FailPrefab, FailSpawn, FailRemove;
        internal static readonly HashSet<GameObject> Owned = new();
        internal static GameObject Prefab() { PrefabCalls++; if (FailPrefab) throw new Exception("asset changed"); return new(); }
        internal static GameObject Spawn(FloorGenerator floor, GameObject prefab, Vector2 point, Action<GameObject> track)
        {
            var obj = new GameObject(); obj.transform.position = point; track(obj); Owned.Add(obj); floor.floorRelatedNetworkObjects.Add(obj); Spawns++;
            if (FailSpawn) throw new Exception("spawn partly published"); return obj;
        }
        internal static void Remove(FloorGenerator floor, GameObject obj)
        {
            if (FailRemove) throw new Exception("despawn failed");
            Removes++; Owned.Remove(obj); floor.floorRelatedNetworkObjects.Remove(obj); obj.Destroyed = true;
        }
        internal static void PruneDestroyed() { }
    }
}
