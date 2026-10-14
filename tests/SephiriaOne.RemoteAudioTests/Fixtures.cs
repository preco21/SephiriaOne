namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class Transform { public Vector3 position = new(3, 4, 0); }
    public record struct Vector3(float x, float y, float z);
    public static class Debug { public static void LogWarning(object value) { } }
}
namespace Mirror
{
    public class NetworkConnection { }
    public class NetworkConnectionToClient : NetworkConnection
    {
        public int connectionId;
        public bool isReady = true, Fail;
        public NetworkIdentity identity;
        public List<object[]> Sent = new();
    }
    public class LocalConnectionToClient : NetworkConnectionToClient { }
    public class NetworkIdentity : UnityEngine.Object
    {
        public PlayerSpawner Owner;
        public Dictionary<int, NetworkConnectionToClient> observers = new();
        public bool TryGetComponent<T>(out T value) where T : class { value = Owner as T; return value != null; }
    }
    public static class NetworkServer
    {
        public static bool active = true;
        public static Dictionary<int, NetworkConnectionToClient> connections = new();
    }
    public class NetworkBehaviour : UnityEngine.Object
    {
        public uint netId = 1;
        public bool isServer = true;
        public NetworkIdentity netIdentity = new();
        public NetworkConnectionToClient connectionToClient;
        public UnityEngine.Transform transform = new();
        protected void SendTargetRPCInternal(NetworkConnection connection, string name, int hash, NetworkWriter writer, int channel)
        {
            var c = (NetworkConnectionToClient)connection;
            if (name != "System.Void DungeonManager::RpcBulletDestroyed(System.UInt32,System.Boolean,System.String,UnityEngine.Vector3,System.Single,System.Single,System.Single)" ||
                hash != -1283831235 || channel != 0) throw new Exception("Invalid cosmetic RPC");
            c.Sent.Add(writer.Values.ToArray());
            if (c.Fail) throw new Exception("Send may already have completed");
        }
    }
    public class NetworkWriter { public List<object> Values = new(); }
    public static class NetworkWriterPool
    {
        public static int Outstanding;
        public static NetworkWriter Get() { Outstanding++; return new(); }
        public static void Return(NetworkWriter writer) { Outstanding--; }
    }
    public static class NetworkWriterExtensions
    {
        public static void WriteVarUInt(this NetworkWriter w, uint v) => w.Values.Add(v);
        public static void WriteBool(this NetworkWriter w, bool v) => w.Values.Add(v);
        public static void WriteString(this NetworkWriter w, string v) => w.Values.Add(v);
        public static void WriteVector3(this NetworkWriter w, UnityEngine.Vector3 v) => w.Values.Add(v);
        public static void WriteFloat(this NetworkWriter w, float v) => w.Values.Add(v);
    }
}
public class PlayerAvatar : Mirror.NetworkBehaviour
{
    public PlayerSpawner spawner;
    public bool IsDead = true;
    public string currentFloorGuid = "lobby";
}
public class PlayerSpawner : Mirror.NetworkBehaviour { public PlayerAvatar PlayerAvatar; }
public class DungeonManager : Mirror.NetworkBehaviour { public static DungeonManager Instance = new(); }
public class Bullet : UnityEngine.Object
{
    public static BulletPool Pool = new();
    public BulletDestroyModule DestroyModule = new();
}
public class BulletPool
{
    public Bullet Prefab = new();
    public Bullet GetPrefab(string name) => name == "FlameBall" ? Prefab : throw new Exception("Unexpected prefab");
}
public class BulletDestroyModule : UnityEngine.Object
{
    public NativeEvent destroySound = new();
    public UnityEngine.Object destroyFxPrefab;
    public List<object> destroyFxPrefabList = new(), breakFxFragments = new();
}
public class DerivedDestroyModule : BulletDestroyModule { }
public class NativeEvent
{
    public bool IsNull;
    public NativeGuid Guid = new();
}
public class NativeGuid
{
    public int Data1 = -33958705, Data2 = 1201911055, Data3 = 338028947, Data4 = 494537983;
}
namespace SephiriaOne
{
    internal static class FriendlyFireDeathVisuals { internal static bool Compatible = true; internal static bool Validate() => Compatible; }
    internal static class HostStateAdapter { internal static bool IsReady(PlayerSpawner spawner) => spawner && spawner.isServer && spawner.PlayerAvatar; }
}
