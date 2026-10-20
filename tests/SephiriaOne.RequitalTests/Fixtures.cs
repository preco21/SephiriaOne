using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class GameObject : Object { }
    public static class Debug { public static void LogWarning(object value) { } }
}
namespace Mirror { public static class NetworkServer { public static bool active = true; } }
namespace SephiriaOne
{
    internal static class SessionSettings
    {
        internal static RequitalSettings Requital = new(false, 1);
        internal static long ResourceGeneration;
        internal static bool EnsureResourceScope() => Mirror.NetworkServer.active;
    }
    internal static class HostStateAdapter { internal static bool IsReady(PlayerSpawner value) => value.Ready; }
    internal static class HarmonyRuntime { internal static void EnsureLoaded() { } }
    internal static class ReviveAllAction { internal static bool IsRunOpen(DungeonManager dungeon, SaveData run) => dungeon && run != null; }
    internal static class FriendlyFireParticipants
    {
        internal sealed class Receipt
        {
            internal PlayerAvatar Player;
            internal PlayerSpawner Spawner;
            internal uint NetId => Player.netId;
        }
        private static readonly ConditionalWeakTable<PlayerAvatar, Receipt> values = new();
        internal static Receipt Capture(PlayerAvatar player) => player && player.spawner && PlayerSpawner.MultiplayerList.Contains(player.spawner)
            ? values.GetValue(player, p => new() { Player = p, Spawner = p.spawner }) : null;
        internal static bool IsCurrent(Receipt value) => value != null && ReferenceEquals(value.Spawner.PlayerAvatar, value.Player) &&
            PlayerSpawner.MultiplayerList.Contains(value.Spawner);
    }
}
public sealed class SaveData { }
public static class SaveManager { public static SaveData CurrentRun = new(); }
public static class HorayNetworkAuthenticator { public static bool AccessDeny_InDungeon; }
public enum EItemType { StoneTablet, Potion }
public enum EItemRarity { Common, Eternal }
public enum EItemActiveType { Disabled, WitchHat }
public enum ERestrictedOwnType { StartingItem }
public sealed class LocalizedString { public string key; }
public sealed class ItemEntity : UnityEngine.Object
{
    public int id;
    public LocalizedString aName = new();
    public EItemType type;
    public EItemRarity rarity;
    public EItemActiveType activeType;
    public UnityEngine.GameObject resourcePrefab = new();
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
    public bool isServer = true;
    public readonly Dictionary<string, string> globalItemStatTable = new();
    public string BuildGlobalItemStatKey(int id, string purpose) => id + "/" + purpose;
    public string GetGlobalItemStatValue(int id, string purpose) => globalItemStatTable.GetValueOrDefault(BuildGlobalItemStatKey(id, purpose));
    public void BoundOnServer(int id, int player) { }
    public void OwnRestrictionOnServer(int id, ERestrictedOwnType kind) { }
}
public sealed class PlayerSpawner : UnityEngine.Object
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public bool Ready = true;
    public int currentPlayerIdx;
    public string playerGuid = Guid.NewGuid().ToString();
    public PlayerAvatar PlayerAvatar;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RestartNewGame(int screen) => PlayerAvatar.Inventory.RestockStartingItem(currentPlayerIdx);
}
public class UnitAvatar : UnityEngine.Object { }
public sealed class PlayerAvatar : UnitAvatar
{
    public bool isServer = true;
    public uint netId = 1;
    public GridInventory Inventory;
    public PlayerSpawner spawner;
}
public sealed class GridInventory : UnityEngine.Object
{
    public struct StartingItemAdditionFailedInfo { public int instanceID, entityID, playerIdx; public sbyte quantity; }
    public bool isServer = true;
    public UnitAvatar UnitAvatar;
    public readonly List<ItemMetadata> startingItems = new(), temporaryInventory = new();
    public readonly Dictionary<int, ItemMetadata> Held = new(), Bag = new(), Engraved = new(), Ground = new();
    private readonly List<StartingItemAdditionFailedInfo> additionFailedStartingItems = new();
    public IReadOnlyList<StartingItemAdditionFailedInfo> Pending => additionFailedStartingItems;
    // Native ForceRemoveAll leaves additionFailedStartingItems intact.
    public void ClearCarried() { Held.Clear(); temporaryInventory.Clear(); }
    public readonly List<ItemMetadata> fixedEngravingsOnServer = new();
    public object FindItemByInstanceID(int id) => Held.ContainsKey(id) ? new object() : null;
    public object FindEngravingByInstanceID(int id) => Engraved.ContainsKey(id) ? new object() : null;
    public bool TryGetSubBagItemByInstanceID(int id, out sbyte x, out ItemMetadata item)
    { x = 0; return Bag.TryGetValue(id, out item); }
    public void RetryPending()
    {
        using (new Permission(this))
            for (int i = additionFailedStartingItems.Count - 1; i >= 0; i--)
            {
                var item = additionFailedStartingItems[i];
                if (LocalAddItem(item.instanceID, item.entityID, item.quantity, 0, true, false))
                    additionFailedStartingItems.RemoveAt(i);
            }
    }
    public bool Full, ThrowAfterWrite;
    public Action OnWrite;
    private bool writePermission;
    public int Grants, Removes;
    public sealed class Permission : IDisposable
    {
        private readonly GridInventory inventory;
        public Permission(GridInventory inventory) { if (inventory.writePermission) throw new Exception("Nested native permission"); this.inventory = inventory; inventory.writePermission = true; }
        public void Dispose() => inventory.writePermission = false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int AddStartingItem(ItemMetadata item, int playerIdx)
    {
        item.instanceID = ItemDatabase.GenerateInstanceID(new()); startingItems.Add(item);
        if (!HorayNetworkAuthenticator.AccessDeny_InDungeon)
            using (new Permission(this))
                if (!LocalAddItem(item.instanceID, item.entityID, 1, 0, true, false))
                    additionFailedStartingItems.Add(new() { instanceID = item.instanceID, entityID = item.entityID, quantity = 1, playerIdx = playerIdx });
        return item.instanceID;
    }
    public bool LocalAddItem(int instance, int entity, sbyte count, int level, bool notification, bool isReward)
    {
        if (!writePermission) throw new Exception("No native permission");
        if (Full) return false;
        // Native LocalAddItem has no duplicate-ID veto. Count every delivery,
        // even though this fixture uses one lookup entry for each ID.
        Held[instance] = new(instance, entity, count); Grants++; OnWrite?.Invoke();
        if (ThrowAfterWrite) throw new Exception("Native callback failed after writing");
        return true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RemoveStartingItem(int id)
    {
        if (!writePermission) throw new Exception("No native permission");
        Held.Remove(id); additionFailedStartingItems.RemoveAll(x => x.instanceID == id); Bag.Remove(id); Engraved.Remove(id); Ground.Remove(id);
        temporaryInventory.RemoveAll(i => i.instanceID == id);
        startingItems.RemoveAll(i => i.instanceID == id); Removes++;
        OnWrite?.Invoke();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RestockStartingItem(int playerIdx)
    {
        foreach (var item in startingItems) AddItem(item, 0, false, false);
    }
    public void AddItem(ItemMetadata item, int rotation = 0, bool notification = true, bool isReward = false)
    { using (new Permission(this)) LocalAddItem(item.instanceID, item.entityID, item.quantity, rotation, notification, isReward); }
}
