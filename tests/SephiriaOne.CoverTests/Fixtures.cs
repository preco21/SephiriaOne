using UnityEngine;
using UnityEngine.Tilemaps;

// Production runtime, room adapter and placement policy are source-linked.
// Only Unity physics/tile access, Mirror and native spawn/despawn are simulated.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public string name = "Fixture";
        public static implicit operator bool(Object o) => o is not null && !o.Destroyed;
    }
    public class GameObject : Object
    {
        public int layer;
        public readonly Transform transform = new();
        public readonly List<Interactable> Interactions = new();
        public T[] GetComponentsInChildren<T>(bool inactive) => Interactions.OfType<T>().ToArray();
    }
    public class Transform : Object
    {
        public Vector3 position, lossyScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity;
        public object Component;
        public readonly Dictionary<string, Transform> Paths = new();
        public Transform Find(string path) => Paths.GetValueOrDefault(path);
        public T GetComponent<T>() where T : class => Component as T;
    }
    public readonly record struct Quaternion(int Value) { public static Quaternion identity => new(1); }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => new(0, 0);
        public float sqrMagnitude => x * x + y * y;
        public static implicit operator Vector3(Vector2 v) => new(v.x, v.y);
        public static implicit operator Vector2(Vector2Int v) => new(v.x, v.y);
        public static implicit operator Vector2(Vector3 v) => new(v.x, v.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    }
    public readonly record struct Vector2Int(int x, int y);
    public readonly record struct Vector3Int(int x, int y, int z = 0);
    public readonly record struct Vector3(float x, float y, float z = 0)
    {
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size) { }
        public void Encapsulate(Bounds other) { }
        public bool Intersects(Bounds other) => true;
    }
    public class Collider2D : Object
    {
        public readonly GameObject gameObject = new();
        public Vector2 Position, Size = new(1, 1);
    }
    public static class Physics2D
    {
        public static int Queries;
        public static bool BlockAll;
        public static Func<Vector2, bool> BlockPoint;
        public static readonly List<Collider2D> Scene = new();
        private static readonly Collider2D collider = new();
        public static Collider2D OverlapBox(Vector2 point, Vector2 size, float rotation, int mask)
        {
            Queries++;
            if (BlockAll || BlockPoint?.Invoke(point) == true) return collider;
            return Scene.Find(c => (mask & (1 << c.gameObject.layer)) != 0 &&
                Math.Abs(c.Position.x - point.x) < (c.Size.x + size.x) / 2 &&
                Math.Abs(c.Position.y - point.y) < (c.Size.y + size.y) / 2);
        }
    }
    public static class LayerMask { public static int NameToLayer(string layer) => layer == "Blockable" ? 3 : 6; }
    public static class Time { public static int frameCount; }
    public static class Random { public static int Range(int min, int max) => 12; }
    public static class Debug
    {
        public static readonly List<string> Warnings = new(), Logs = new();
        public static void LogWarning(string text) => Warnings.Add(text);
        public static void Log(string text) => Logs.Add(text);
    }
}
namespace UnityEngine.Tilemaps
{
    public class TileBase : UnityEngine.Object { public GroundTileEntity Entity = new(); }
    public class Tilemap : UnityEngine.Object
    {
        public readonly Transform transform = new();
        public Vector3 cellSize = Vector3.one;
        public Func<Vector3Int, bool> Contains = p => p.x >= 0 && p.y >= 0 && p.x < 100 && p.y < 100;
        public readonly TileBase Tile = new();
        public Vector3Int WorldToCell(Vector2 p) => new((int)Math.Floor(p.x - transform.position.x), (int)Math.Floor(p.y - transform.position.y));
        public TileBase GetTile(Vector3Int p) => Contains(p) ? Tile : null;
        public bool HasTile(Vector3Int p) => GetTile(p);
    }
}
namespace Mirror
{
    public static class NetworkServer { public static bool active = true; public static readonly object localConnection = new(); }
    public static class NetworkClient { public static bool active = true; }
}
public static class CombatManager { public const int BlockCharacterLayerMask = 1 << 12; }
public sealed class GroundTileEntity : UnityEngine.Object { public enum Type { Normal, Pit, Water } public Type type; }
public static class TileDatabase { public static GroundTileEntity FindGroundTile(TileBase tile) => tile.Entity; }
public sealed class PathGrid : UnityEngine.Object
{
    public static PathGrid Current = new(); public bool IsBuilt = true;
    public int Width => 100; public int Height => 100; public float CellSize => 1; public Vector2 Center => new(50, 50);
    public int Updates; public bool Fail;
    public void UpdateRegion(Bounds b) { if (Fail) throw new Exception("path update failed"); Updates++; }
}
public enum EFloorThreatType { Normal, Boss }
public sealed class FloorData { public string stageName = "Lobby"; public EFloorThreatType threatType; }
public class FloorGenerator : UnityEngine.Object
{
    public static readonly Dictionary<string, FloorGenerator> Floors = new();
    public static FloorGenerator FindByGuid(string guid) => Floors.GetValueOrDefault(guid);
    public string guid; public bool GenerateSuccess = true, isServer = true;
    public int seed;
    public EFloorThreatType floorThreatType;
    public readonly Transform transform = new();
    public readonly List<GameObject> NativeProps = new();
    public readonly List<AreaSpawnPointProp> spawnPoints = new();
    public FloorData DataOnServer = new(); public readonly List<GameObject> floorRelatedNetworkObjects = new();
}
public class TileFloorGenerator : FloorGenerator { public Tilemap ground = new(), upperGround, wall, water, cliffCollider; }
public sealed class RoomMetadata { public bool spawnMonster = true; }
public sealed class TileBasedRoomInstance
{
    public RoomMetadata Metadata = new(); public Vector2 bottomLeft, topRight = new(100, 100), TeleportPoint;
    public Vector2Int size = new(100, 100);
}
public sealed class LibraryFloorRoomInstance
{
    public RoomMetadata Metadata = new(); public Vector2Int pos, Size = new(100, 100); public Vector2 TeleportPoint;
}
public sealed class EnhancedProceduralFloorGenerator : TileFloorGenerator
{ private List<TileBasedRoomInstance> roomList = new() { new() }; public TileBasedRoomInstance TestRoom => roomList[0]; }
public sealed class FixedFloorGenerator : TileFloorGenerator
{ private Dictionary<Vector2Int, TileBasedRoomInstance> roomInstances = new() { [new(0, 0)] = new() }; }
public sealed class LibraryFloorGenerator : TileFloorGenerator
{ private List<LibraryFloorRoomInstance> roomList = new() { new() }; }
public sealed class SingleRoomFloorGenerator : TileFloorGenerator
{ private TileBasedRoomInstance floorInstance = new(); }
public sealed class TeleportPoint : UnityEngine.Object
{
    public readonly Transform transform = new(); public Vector3 offset;
    public Vector3 GetTeleportTargetPosition() => transform.position + offset;
}
public sealed class FullyDesignedFloorGenerator : FloorGenerator
{
    public sealed class MapTeleportConnection { public TeleportPoint point; }
    public Vector2 bottomLeft, topRight = new(100, 100);
    public readonly List<GameObject> props = new();
    public readonly List<MapTeleportConnection> mapTeleportConnections = new();
    public readonly Tilemap TestGround = new();
    public FullyDesignedFloorGenerator()
    { TestGround.transform.Component = TestGround; transform.Paths["Grid/Ground"] = TestGround.transform; }
}
public sealed class AreaSpawnPointProp : UnityEngine.Object { public Vector3 SpawnPoint; }
public sealed class Interactable : UnityEngine.Object { public readonly Transform transform = new(); }
public sealed class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance = new(); public bool isServer = true;
    public readonly Dictionary<string, FloorData> generatedFloors = new();
}
public static class SaveManager { public static object CurrentRun = new(); }
public sealed class Stage : UnityEngine.Object { public Stage() { name = "Lobby"; } }
public sealed class Race : UnityEngine.Object { public Stage lobbyStage = new(); }
public sealed class PlayerAvatar : UnityEngine.Object
{ public string currentFloorGuid = "A"; public int loadingScreenType = -1; public Race Race = new(); public readonly Transform transform = new(); }
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
    internal static class PvpCoverNative
    {
        internal static int Spawns, Removes, PrefabCalls; internal static bool FailPrefab, FailSpawn, FailRemove;
        internal static readonly HashSet<GameObject> Owned = new();
        internal static List<GameObject> Props(FloorGenerator floor) => floor.NativeProps;
        internal static GameObject Prefab() { PrefabCalls++; if (FailPrefab) throw new Exception("asset changed"); return new(); }
        internal static GameObject Spawn(FloorGenerator floor, GameObject prefab, Vector2 point, Action<GameObject> track)
        {
            var obj = new GameObject(); obj.transform.position = point; track(obj); Owned.Add(obj); floor.NativeProps.Add(obj); floor.floorRelatedNetworkObjects.Add(obj); Spawns++;
            if (FailSpawn) throw new Exception("spawn partly published"); return obj;
        }
        internal static void Remove(FloorGenerator floor, GameObject obj)
        {
            if (FailRemove) throw new Exception("despawn failed");
            Removes++; Owned.Remove(obj); floor.NativeProps.Remove(obj); floor.floorRelatedNetworkObjects.Remove(obj); obj.Destroyed = true;
        }
        internal static void PruneDestroyed() { }
    }
}
