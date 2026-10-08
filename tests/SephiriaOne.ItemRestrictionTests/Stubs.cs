using System.Runtime.CompilerServices;
using Mirror;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public static class Debug { public static void LogWarning(object message) => Console.WriteLine(message); }
}
namespace Mirror
{
    public class NetworkBehaviour : UnityEngine.Object
    {
        public uint netId = 1;
        public bool isServer = true;
        public NetworkIdentity netIdentity = new();
        public NetworkConnectionToClient connectionToClient => netIdentity.connectionToClient;
    }
    public class NetworkIdentity
    {
        public NetworkConnectionToClient connectionToClient;
        public bool RejectAuthority;
        public void RemoveClientAuthority() => connectionToClient = null;
        public bool AssignClientAuthority(NetworkConnectionToClient owner)
        { if (RejectAuthority) return false; connectionToClient = owner; return true; }
    }
    public class NetworkConnectionToClient { public int connectionId; }
    public static class NetworkServer
    {
        public static bool active = true;
        public static Dictionary<int, NetworkConnectionToClient> connections = new();
    }
    public class SyncIDictionary<K, V> : Dictionary<K, V>, IDictionary<K, V>
    {
        public enum Operation : byte { OP_ADD, OP_SET, OP_REMOVE, OP_CLEAR }
        public Action<Operation, K, V> OnChange;
        public Action<K, V> AfterWrite;
        public int Writes;
        public new V this[K key]
        {
            get => base[key];
            set { bool existed = TryGetValue(key, out var old); base[key] = value; Writes++; OnChange?.Invoke(existed ? Operation.OP_SET : Operation.OP_ADD, key, old); AfterWrite?.Invoke(key, value); }
        }
        V IDictionary<K, V>.this[K key] { get => this[key]; set => this[key] = value; }
        public new void Add(K key, V value) { base.Add(key, value); Writes++; OnChange?.Invoke(Operation.OP_ADD, key, value); }
        public new bool Remove(K key)
        { if (!TryGetValue(key, out var old) || !base.Remove(key)) return false; Writes++; OnChange?.Invoke(Operation.OP_REMOVE, key, old); return true; }
        public new void Clear() { OnChange?.Invoke(Operation.OP_CLEAR, default, default); base.Clear(); Writes++; }
    }
    public class SyncDictionary<K, V> : SyncIDictionary<K, V> { }
}
public enum ERestrictedOwnType { None, StartingItem }
public sealed class DungeonManager : NetworkBehaviour
{
    public static DungeonManager Instance;
    public readonly SyncDictionary<string, string> globalItemStatTable = new();
    public string GetGlobalItemStatValue(int id, string key) => globalItemStatTable.TryGetValue(id + "/" + key, out var value) ? value : "";
    public string BuildGlobalItemStatKey(int id, string purpose) => id + "/" + purpose;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BoundOnServer(int instanceId, int owner) { globalItemStatTable[BuildGlobalItemStatKey(instanceId, "Bound")] = owner.ToString(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OwnRestrictionOnServer(int instanceId, ERestrictedOwnType type) { globalItemStatTable[BuildGlobalItemStatKey(instanceId, "OwnRestriction")] = ((int)type).ToString(); }
    public void UnboundOnServer(int instanceId) => globalItemStatTable.Remove(instanceId + "/Bound");
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SaveCurrentSessionData(string floor)
    {
        SaveManager.CurrentRun.SetInt("GlobalItemStatCount", globalItemStatTable.Count);
        int index = 0;
        foreach (var entry in globalItemStatTable)
        { SaveManager.CurrentRun.SetString(string.Format("GlobalItemStatCount{0}_Key", index), entry.Key); SaveManager.CurrentRun.SetString(string.Format("GlobalItemStatCount{0}_Value", index), entry.Value); index++; }
    }
}
public class Item : NetworkBehaviour
{
    public static List<Item> managedItemInstances = new();
    public int itemInstanceID;
    public bool isBound;
    public bool NetworkisBound { set => isBound = value; }
}
public class PlayerSpawner : NetworkBehaviour
{
    public static List<PlayerSpawner> MultiplayerList = new();
    public int currentPlayerIdx;
}
public static class SaveManager { public static SaveData CurrentRun = new(); }
public class SaveData
{
    public Dictionary<string, object> Values = new();
    public void SetInt(string key, int value) => Values[key] = value;
    public void SetString(string key, string value) => Values[key] = value;
}
namespace SephiriaOne
{
    internal static class HarmonyRuntime { internal static void EnsureLoaded() { } }
    internal static class SessionSettings
    {
        internal static bool Intent;
        internal static bool EnsureResourceScope() { ItemRestrictionFeature.Bind(DungeonManager.Instance, Intent); return DungeonManager.Instance != null; }
    }
}
