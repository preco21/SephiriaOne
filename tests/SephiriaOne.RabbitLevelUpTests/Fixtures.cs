using System.Runtime.CompilerServices;
using Mirror;

namespace UnityEngine
{
    public class Object { public bool Destroyed; public static implicit operator bool(Object o) => o != null && !o.Destroyed; }
    public class GameObject : Object
    {
        public WieldingPotion Potion = new();
        public bool TryGetComponent<T>(out T value) where T : class { value = Potion as T; return value != null; }
    }
    public static class Debug { public static readonly List<object> Warnings = new(); public static void LogWarning(object o) => Warnings.Add(o); }
}
namespace Mirror
{
    public class NetworkBehaviour : UnityEngine.Object { public bool isServer = true; public uint netId = 1; public NetworkConnectionToClient connectionToClient; }
    public class NetworkConnectionToClient { public bool isReady = true; public NetworkIdentity identity; }
    public class NetworkIdentity
    {
        public PlayerSpawner Owner;
        public bool TryGetComponent<T>(out T value) where T : class { value = Owner as T; return value != null; }
    }
    public static class NetworkServer { public static bool active = true; public static readonly Dictionary<int, NetworkConnectionToClient> connections = new(); }
}
public class DungeonManager : NetworkBehaviour { public static DungeonManager Instance = new(); }
public static class SaveManager { public static object CurrentRun = new(); }
public class PlayerAvatar : NetworkBehaviour
{
    public PlayerSpawner spawner;
    public GridInventory Inventory = new();
    public string currentCostume = "HolyRabbit", currentFloorGuid = "floor";
    public bool IsDead;
}
public class PlayerSpawner : NetworkBehaviour
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public PlayerAvatar PlayerAvatar;
}
public class LevelController : NetworkBehaviour
{
    public static readonly int[] ExpTableByLevel = { 0, 10, 20, 30, 40, 50 };
    public PlayerAvatar Avatar { get; set; }
    public int currentLevel = 1, currentExp, NativeCalls, RestoredChoices;
    public int NetworkcurrentLevel { get => currentLevel; [MethodImpl(MethodImplOptions.NoInlining)] set => currentLevel = value; }
    public Action OnLevel;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddExp(int exp) => LocalAddExp(exp);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LocalAddExp(int exp)
    {
        if (!NetworkServer.active) return;
        currentExp += exp;
        while (currentLevel < ExpTableByLevel.Length && currentExp >= ExpTableByLevel[currentLevel])
        { NetworkcurrentLevel = currentLevel + 1; LevelUpOnServer(); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LevelUpOnServer() { NativeCalls++; OnLevel?.Invoke(); }
    public void Initialize(int level, int exp) { currentLevel = level; currentExp = exp; }
    public void GenerateItem(int seed) => RestoredChoices++;
}
public class ItemMetadata
{
    public int instanceID, entityID; public sbyte quantity;
    public ItemMetadata(int instance, int entity, sbyte count) { instanceID = instance; entityID = entity; quantity = count; }
}
public class GridInventory : NetworkBehaviour
{
    public readonly List<ItemMetadata> Items = new(), temporaryInventory = new();
    public Action OnPermission, OnWrite;
    public bool Reject, ThrowAfterWrite;
    public int Grants, Permissions;
    private bool writePermission;
    public bool HasPermission => writePermission;
    public sealed class Permission : IDisposable
    {
        private readonly GridInventory inventory;
        public Permission(GridInventory i)
        {
            inventory = i;
            if (i.writePermission) return; // Native scope is deliberately not reentrant.
            i.writePermission = true; i.Permissions++; i.OnPermission?.Invoke();
        }
        public void Dispose() { if (!inventory.writePermission) return; inventory.writePermission = false; inventory.Permissions--; }
    }
    public bool LocalAddItem(int instanceID, int entityID, sbyte quantity, int rotation, bool notification, bool isReward)
    {
        if (Permissions == 0 || !notification || isReward || rotation != 0 || quantity != 1) throw new Exception("Wrong native grant protocol");
        Grants++; OnWrite?.Invoke();
        if (Reject) return false;
        Items.Add(new(instanceID, entityID, quantity));
        if (ThrowAfterWrite) throw new InvalidOperationException("failure after write");
        return true;
    }
}
public enum EItemType { Potion, Charm }
public enum EItemActiveType { Normal, Disabled, Hidden }
public class LocalizedString { public string key; }
public class ItemEntity : UnityEngine.Object
{
    public int id; public LocalizedString aName = new(); public EItemType type = EItemType.Potion;
    public EItemActiveType activeType; public UnityEngine.GameObject resourcePrefab = new();
}
public class WieldingPotion : UnityEngine.Object { public PotionEffect effect = new PotionEffect_StatusInstance(); }
public class PotionEffect : UnityEngine.Object { }
public class PotionEffect_StatusInstance : PotionEffect { }
public class PotionEffect_DicePotion : PotionEffect { }
public class PotionEffect_ElementalDamageBoost : PotionEffect { }
public class PotionEffect_Enchant : PotionEffect { }
public class PotionEffect_RandomCombo : PotionEffect { }
public static class ItemDatabase
{
    public static readonly Dictionary<int, ItemEntity> Items = new();
    private static int nextId;
    public static ItemEntity FindItemById(int id) => Items.GetValueOrDefault(id);
    public static int GenerateInstanceID(Random random) => ++nextId;
}
namespace SephiriaOne
{
    internal static class HarmonyRuntime { public static void EnsureLoaded() { } }
    internal static class HostStateAdapter
    {
        public static bool IsReady(PlayerSpawner s) => s && s.isServer && s.netId != 0 && s.PlayerAvatar &&
            s.PlayerAvatar.isServer && s.PlayerAvatar.netId != 0 && s.PlayerAvatar.Inventory &&
            s.PlayerAvatar.Inventory.isServer && s.PlayerAvatar.Inventory.netId != 0;
    }
    internal static class SessionSettings
    {
        public static RabbitPotionSettings RabbitPotionsForUse;
        public static long ResourceGeneration;
    }
}
