using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public static class Debug { public static readonly List<object> Warnings = new(); public static void LogWarning(object value) => Warnings.Add(value); }
}
namespace Mirror { public static class NetworkServer { public static bool active = true; } }
namespace SephiriaOne
{
    internal static class SessionSettings { public static bool CollinForUse; }
    internal static class CollinFeature { public static bool Available = true; }
}
public static class HorayNetworkAuthenticator { public static bool AccessDeny_InDungeon; }
public sealed class LocalizedString { public string key; }
public sealed class ItemEntity : UnityEngine.Object { public int id; public LocalizedString aName = new(); }
public sealed class CostumeEntity : UnityEngine.Object { public string id; public ItemEntity[] startingItems = Array.Empty<ItemEntity>(); }
public static class CostumeDatabase
{
    public static readonly Dictionary<string, CostumeEntity> Items = new();
    public static CostumeEntity FindCostumeByID(string id) => Items.GetValueOrDefault(id);
}
public static class ItemDatabase
{
    private static int next;
    public static readonly Dictionary<int, ItemEntity> Items = new();
    public static ItemEntity FindItemById(int id) => Items.GetValueOrDefault(id);
    public static int GenerateInstanceID(Random random) => ++next;
}
public struct ItemMetadata
{
    public int instanceID, entityID;
    public sbyte quantity;
    public ItemMetadata(int instance, int entity, sbyte count) { instanceID = instance; entityID = entity; quantity = count; }
}
public sealed class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance = new();
    public readonly Dictionary<string, string> globalItemStatTable = new();
    public string BuildGlobalItemStatKey(int id, string purpose) => id + ":" + purpose;
    public string GetGlobalItemStatValue(int id, string purpose) => globalItemStatTable.GetValueOrDefault(BuildGlobalItemStatKey(id, purpose));
}
public sealed class PlayerSpawner : UnityEngine.Object
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public int currentPlayerIdx;
    public PlayerAvatar PlayerAvatar;
}
public class UnitAvatar : UnityEngine.Object { }
public sealed class PlayerAvatar : UnitAvatar
{
    public bool isServer = true;
    public string currentCostume = "PinkRabbit";
    public GridInventory Inventory;
    public PlayerSpawner spawner;
    private readonly List<int> costumeStartingItemInstanceIDs = new();
    public IReadOnlyList<int> Owned => costumeStartingItemInstanceIDs;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UpdateCostumeData(string costumeID, bool fromRuntime)
    {
        if (!isServer) return;
        using (new GridInventory.Permission(Inventory))
            foreach (int id in costumeStartingItemInstanceIDs) Inventory.RemoveStartingItem(id);
        costumeStartingItemInstanceIDs.Clear(); currentCostume = costumeID;
        foreach (var item in CostumeDatabase.FindCostumeByID(costumeID).startingItems)
            costumeStartingItemInstanceIDs.Add(Inventory.AddStartingItem(new(-1, item.id, 1), spawner.currentPlayerIdx));
    }
}
public sealed class GridInventory : UnityEngine.Object
{
    public bool isServer = true;
    public UnitAvatar UnitAvatar;
    public readonly List<ItemMetadata> startingItems = new();
    public readonly Dictionary<int, ItemMetadata> Held = new(), Pending = new();
    public bool Full, ThrowAfterRegister;
    public int Grants;
    public sealed class Permission : IDisposable { public Permission(GridInventory inventory) { } public void Dispose() { } }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int AddStartingItem(ItemMetadata item, int playerIdx)
    {
        item.instanceID = ItemDatabase.GenerateInstanceID(new()); startingItems.Add(item);
        if (ThrowAfterRegister) { ThrowAfterRegister = false; throw new Exception("fixture interrupted native grant"); }
        if (!HorayNetworkAuthenticator.AccessDeny_InDungeon) { if (Full) Pending.Add(item.instanceID, item); else { Held.Add(item.instanceID, item); Grants++; } }
        return item.instanceID;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RemoveStartingItem(int id) { Held.Remove(id); Pending.Remove(id); startingItems.RemoveAll(i => i.instanceID == id); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RestockStartingItem(int playerIdx) { foreach (var item in startingItems) { Held.Add(item.instanceID, item); Grants++; } }
}
