using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnityEngine
{
    public class Object { public bool Destroyed; public static implicit operator bool(Object value) => value != null && !value.Destroyed; }
    public sealed class Transform { public Vector3 position; }
    public readonly record struct Vector3(float x, float y, float z = 0)
    {
        public static implicit operator Vector2(Vector3 value) => new(value.x, value.y);
        public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
    }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => default;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static implicit operator Vector3(Vector2 value) => new(value.x, value.y);
    }
    public readonly record struct Vector2Int(int x, int y)
    { public static implicit operator Vector2(Vector2Int value) => new(value.x, value.y); }
    public readonly record struct Vector3Int(int x, int y, int z = 0);
    public sealed class Collider2D : Object { }
    public static class Physics2D
    {
        public static Func<Vector2, bool> Blocked = _ => false;
        public static int Queries;
        public static Object OverlapCircle(Vector2 point, float radius, int mask)
        { Queries++; return Blocked(point) ? new Collider2D() : null; }
    }
}
namespace UnityEngine.Tilemaps
{
    public class TileBase : UnityEngine.Object { }
    public class Tilemap : UnityEngine.Object
    {
        public Func<Vector3Int, TileBase> Tiles = _ => null;
        public Vector2 Origin;
        public int Queries;
        public readonly List<Vector2> WorldSamples = new();
        public Vector3Int WorldToCell(Vector3 position)
        { WorldSamples.Add(position); return new((int)Math.Floor(position.x - Origin.x), (int)Math.Floor(position.y - Origin.y)); }
        public TileBase GetTile(Vector3Int cell) { Queries++; return Tiles(cell); }
        public bool HasTile(Vector3Int cell) => GetTile(cell);
    }
}
public enum EFloorThreatType { None, Boss }
public class FloorData { public EFloorThreatType threatType; }
public class FloorGenerator : UnityEngine.Object
{
    public Transform transform = new();
    public int seed = 123;
    public EFloorThreatType floorThreatType;
    public FloorData DataOnServer = new();
}
public class TileFloorGenerator : FloorGenerator { public Tilemap ground, upperGround, wall, water, cliffCollider; }
public class SingleRoomFloorGenerator : TileFloorGenerator { }
public class EnhancedProceduralFloorGenerator : TileFloorGenerator
{
    private readonly List<TileBasedRoomInstance> roomList = new();
    public void Add(TileBasedRoomInstance room) => roomList.Add(room);
}
public class LibraryFloorGenerator : TileFloorGenerator
{
    private readonly List<LibraryFloorRoomInstance> roomList = new();
    public void Add(LibraryFloorRoomInstance room) => roomList.Add(room);
}
public class FixedFloorGenerator : TileFloorGenerator
{
    private readonly Dictionary<Vector2Int, TileBasedRoomInstance> roomInstances = new();
    public void Add(Vector2Int key, TileBasedRoomInstance room) => roomInstances.Add(key, room);
}
public class RoomMetadata
{
    public bool spawnMonster = true, isCustomSpawnAreaEnabled;
    public Vector2 customSpawnArea_LB, customSpawnArea_RT;
}
public class TileBasedRoomInstance
{
    public RoomMetadata Metadata = new();
    public Vector2 bottomLeft, topRight;
}
public class LibraryFloorRoomInstance
{
    public RoomMetadata Metadata = new();
    public Vector2Int pos, Size;
}
public class GroundTileEntity : UnityEngine.Object
{
    public enum Type { Floor, Pit, Water }
    public Type type;
}
public static class TileDatabase
{
    public static readonly Dictionary<TileBase, GroundTileEntity> Ground = new();
    public static GroundTileEntity FindGroundTile(TileBase tile) => tile != null ? Ground.GetValueOrDefault(tile) : null;
}
public static class CombatManager { public const int BlockCharacterLayerMask = 17; }
public class Safe : UnityEngine.Object
{
    private static List<Safe> safeList = new();
    public readonly Transform transform = new();
    public static int Queries, Logs;
    public static List<Safe> All => safeList;
    public static void Initialize() { safeList = new(); Queries = Logs = 0; }
    public static void Destroy() => safeList = null;
    public static Safe Add(Vector3 position, bool destroyed = false)
    { var safe = new Safe { Destroyed = destroyed }; safe.transform.position = position; safeList.Add(safe); return safe; }
    public static Safe Find(Vector3 position)
    {
        Queries++;
        Safe found = null;
        foreach (var safe in safeList)
        {
            if (!safe || Vector3.Distance(safe.transform.position, position) > 10f) continue;
            Logs++;
            if (!found || Vector3.Distance(safe.transform.position, position) < Vector3.Distance(found.transform.position, position)) found = safe;
        }
        return found;
    }
}
