using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace UnityEngine
{
    public class Object { public bool Destroyed; public static implicit operator bool(Object value) => value != null && !value.Destroyed; }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public float sqrMagnitude => x * x + y * y + z * z;
    }
    public class Transform : Object { public Vector3 position; }
    public readonly record struct Vector2(float x, float y)
    {
        public static implicit operator Vector2(Vector3 value) => new(value.x, value.y);
    }
    public readonly record struct Color(float r, float g, float b, float a = 1f);
    public static class Debug { public static void LogWarning(object message) { } }
}
namespace Mirror
{
    public class NetworkBehaviour : UnityEngine.Object
    {
        public bool isServer = true, isLocalPlayer;
        public uint netId = 1;
        public NetworkConnectionToClient connectionToClient;
        public Transform transform = new();
        protected void SendTargetRPCInternal(NetworkConnection connection, string name, int hash, NetworkWriter writer, int channel)
        {
            var target = (NetworkConnectionToClient)connection;
            if (target.FailNotice) throw new InvalidOperationException("notice unavailable");
            target.Notices.Add((this, name, hash, writer.Values.ToArray(), channel));
        }
        protected void SendRPCInternal(string name, int hash, NetworkWriter writer, int channel, bool includeOwner) =>
            throw new Exception("Feedback must never broadcast");
    }
    public class NetworkIdentity
    {
        public PlayerSpawner Owner;
        public bool TryGetComponent<T>(out T component) where T : class { component = Owner as T; return component != null; }
    }
    public class NetworkConnection { }
    public class NetworkConnectionToClient : NetworkConnection
    {
        public bool isReady = true, FailNotice;
        public NetworkIdentity identity;
        public readonly List<(NetworkBehaviour Owner, string Name, int Hash, object[] Payload, int Channel)> Notices = new();
    }
    public class LocalConnectionToClient : NetworkConnectionToClient { }
    public class NetworkWriter { public readonly List<object> Values = new(); }
    public class NetworkWriterPooled : NetworkWriter { }
    public static class NetworkWriterPool
    {
        public static int Outstanding;
        public static NetworkWriterPooled Get() { Outstanding++; return new(); }
        public static void Return(NetworkWriterPooled writer) { Outstanding--; }
    }
    public static class NetworkWriterExtensions
    {
        public static void WriteVector2(this NetworkWriter w, Vector2 v) => w.Values.Add(v);
        public static void WriteString(this NetworkWriter w, string v) => w.Values.Add(v);
        public static void WriteColor(this NetworkWriter w, Color v) => w.Values.Add(v);
        public static void WriteVarInt(this NetworkWriter w, int v) => w.Values.Add(v);
        public static void WriteBool(this NetworkWriter w, bool v) => w.Values.Add(v);
        public static void WriteNetworkBehaviour(this NetworkWriter w, NetworkBehaviour v) => w.Values.Add(v);
    }
    public class NetworkReader { }
    public static class NetworkReaderExtensions
    {
        public static Vector2 ReadVector2(this NetworkReader r) => default;
        public static string ReadString(this NetworkReader r) => null;
        public static Color ReadColor(this NetworkReader r) => default;
        public static int ReadVarInt(this NetworkReader r) => 0;
        public static bool ReadBool(this NetworkReader r) => false;
        public static T ReadNetworkBehaviour<T>(this NetworkReader r) where T : NetworkBehaviour => null;
    }
    public static class NetworkServer { public static bool active = true; public static readonly Dictionary<int, NetworkConnectionToClient> connections = new(); }
}
public sealed class NewItemOwnInstance { public int EntityID, InstanceID; public sbyte Quantity = 1; }
public readonly record struct ItemPosition(sbyte x, sbyte y);
public class GridInventory : NetworkBehaviour
{
    public int canBroadcast = 1;
    public readonly Dictionary<ItemPosition, NewItemOwnInstance> items = new();
    public ItemPosition IdxToPos(int idx) => new((sbyte)idx, 0);
    public NewItemOwnInstance FindItem(ItemPosition pos) => items.GetValueOrDefault(pos);
    public void DecreaseItemQuantity(sbyte x, sbyte y, int amount) => items[new(x, y)].Quantity -= (sbyte)amount;
}
public enum ECustomStat { HpPotionBonus }
public class UnitAvatar : NetworkBehaviour
{
    public readonly List<(string Message, float Duration, bool Timer)> SystemMessages = new();
    public bool FailSystemMessage;
    public void WriteSystemMessage(string message, float time, bool showTimer)
    {
        if (FailSystemMessage) throw new InvalidOperationException("UI unavailable");
        SystemMessages.Add((message, time, showTimer));
    }
    protected virtual void RpcShowDamageParticle(Vector2 position, string msg, Color color, int fontSize, bool isPrivate, UnitAvatar self, UnitAvatar attacker)
    {
        var writer = NetworkWriterPool.Get();
        writer.WriteVector2(position); writer.WriteString(msg); writer.WriteColor(color); writer.WriteVarInt(fontSize);
        writer.WriteBool(isPrivate); writer.WriteNetworkBehaviour(self); writer.WriteNetworkBehaviour(attacker);
        SendRPCInternal("System.Void UnitAvatar::RpcShowDamageParticle(UnityEngine.Vector2,System.String,UnityEngine.Color,System.Int32,System.Boolean,UnitAvatar,UnitAvatar)", -1687250273, writer, 1, true);
        NetworkWriterPool.Return(writer);
    }
    protected virtual void UserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar(Vector2 position, string msg, Color color, int fontSize, bool isPrivate, UnitAvatar self, UnitAvatar attacker) { }
    protected static void InvokeUserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        ((UnitAvatar)obj).UserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar(reader.ReadVector2(), reader.ReadString(), reader.ReadColor(), reader.ReadVarInt(), reader.ReadBool(), reader.ReadNetworkBehaviour<UnitAvatar>(), reader.ReadNetworkBehaviour<UnitAvatar>());
    }
    public bool IsDead;
    public GridInventory Inventory = new();
    public int PotionBonus, DrinkEvents;
    public int mp = 30, MpWrites, MpUseEvents;
    public bool FailMpWrite, InfinityMp;
    public int Networkmp { get => mp; set => GeneratedSyncVarSetter(value, ref mp, 32UL, null); }
    private void GeneratedSyncVarSetter(int value, ref int field, ulong bit, Action callback)
    {
        if (FailMpWrite) throw new InvalidOperationException("MP write failed");
        field = value; MpWrites++; callback?.Invoke();
    }
    public void UseMp(int value) { MpUseEvents++; if (!InfinityMp) Networkmp = mp - value; }
    public float Hp = 20f, MaxHp = 100f, HealingPenalty;
    public Action OnPotionEvent;
    public event Action<PotionEffect> OnDrinkPotion;
    public Action OnHealed;
    public readonly List<float> Heals = new();
    public virtual int GetCustomStat(ECustomStat stat) => PotionBonus;
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void HealPercent(float strength)
    {
        Heals.Add(strength);
        if (!IsDead) Hp = Math.Min(MaxHp, Hp + MaxHp * strength * (1f - HealingPenalty) / 100f);
        OnHealed?.Invoke();
    }
    public void ReceivePotionDrinkEvent(PotionEffect effect) { DrinkEvents++; OnPotionEvent?.Invoke(); OnDrinkPotion?.Invoke(effect); }
}
public class PlayerAvatar : UnitAvatar
{
    public PlayerSpawner spawner;
    public string currentCostume = "HolyRabbit", currentFloorGuid = "floor", playerNameSource = "player";
    public object Race = new();
}
public class PlayerSpawner : NetworkBehaviour
{
    public static readonly List<PlayerSpawner> MultiplayerList = new();
    public PlayerAvatar PlayerAvatar;
    public PlayerSpawner(PlayerAvatar player, bool local = false)
    {
        PlayerAvatar = player; player.spawner = this;
        connectionToClient = local ? new LocalConnectionToClient() : new NetworkConnectionToClient();
        connectionToClient.identity = new NetworkIdentity { Owner = this };
        MultiplayerList.Add(this); NetworkServer.connections[MultiplayerList.Count] = connectionToClient;
    }
}
public class ItemController : NetworkBehaviour
{
    public struct QuickSlot { public int idx; }
    public UnitAvatar Avatar;
    public WieldingItem currentWieldingItem;
    public WieldingItem NetworkcurrentWieldingItem { get => currentWieldingItem; set => currentWieldingItem = value; }
    public int SelectedQuickSlotIdx;
    public readonly List<QuickSlot> quickSlotTable = new() { new QuickSlot { idx = 0 } };
    public event Action<WieldingPotion> OnDrinkPotionServerside;
    public int CleanupCalls;
    private void RpcWieldItem(int id) { CleanupCalls++; }
    public void CancelAction()
    {
        if (currentWieldingItem) { currentWieldingItem.Destroyed = true; currentWieldingItem.netId = 0; currentWieldingItem.isServer = false; }
        NetworkcurrentWieldingItem = null;
    }
    public void RunDrink() => DrinkPotionAnimation();
    [MethodImpl(MethodImplOptions.NoInlining)] public void DrinkPotionAnimation()
    {
        var potion = currentWieldingItem as WieldingPotion;
        if (potion == null || SelectedQuickSlotIdx < 0 || SelectedQuickSlotIdx >= quickSlotTable.Count) { RpcWieldItem(-1); return; }
        var pos = Avatar.Inventory.IdxToPos(quickSlotTable[SelectedQuickSlotIdx].idx);
        var item = Avatar.Inventory.FindItem(pos);
        if (item != null && item.EntityID == potion.entityID)
        {
            try
            {
                potion.Drink(out var decreased, item.InstanceID);
                OnDrinkPotionServerside?.Invoke(potion);
                if (decreased) Avatar.Inventory.DecreaseItemQuantity(pos.x, pos.y, 1);
            }
            catch { }
        }
        RpcWieldItem(-1);
    }
}
public class WieldingItem : NetworkBehaviour { public ItemController NetworkController; public int entityID; }
public class WieldingPotion : WieldingItem
{
    public PotionEffect effect;
    public int itemInstanceID;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Drink(out bool itemDecreased, int instanceID)
    {
        itemInstanceID = instanceID;
        bool decreaseItemOnDrink = effect.DecreaseItemOnDrink;
        effect.CreateEffect_OnDrink(NetworkController.Avatar);
        itemDecreased = decreaseItemOnDrink;
    }
}
public class PotionEffect : NetworkBehaviour
{
    public virtual bool DecreaseItemOnDrink => true;
    public virtual void CreateEffect_OnDrink(UnitAvatar avatar) { if (DecreaseItemOnDrink) avatar.ReceivePotionDrinkEvent(this); }
}
public class PotionEffect_Regeneration : PotionEffect
{
    public int healPercent = 20;
    [MethodImpl(MethodImplOptions.NoInlining)] public override void CreateEffect_OnDrink(UnitAvatar avatar)
    {
        base.CreateEffect_OnDrink(avatar);
        float num = healPercent;
        float bonus = (float)avatar.GetCustomStat(ECustomStat.HpPotionBonus) / 100f;
        avatar.HealPercent(num + num * bonus);
    }
}
public class PassiveObject : NetworkBehaviour { public PlayerAvatar player; }
public class PassiveObject_PotionAndRandomStat : PassiveObject
{
    public int StatGains;
    public void Enable(PlayerAvatar avatar) { player = avatar; avatar.OnDrinkPotion += HandleDrinkPotion; }
    public void Invoke(PotionEffect effect) => HandleDrinkPotion(effect);
    [MethodImpl(MethodImplOptions.NoInlining)] private void HandleDrinkPotion(PotionEffect effect) { StatGains++; }
}
public static class SaveManager { public static object CurrentRun = new(); }
public class DungeonManager { public static DungeonManager Instance = new(); }
namespace SephiriaOne
{
    internal static class SessionSettings { public static RabbitPotionSettings RabbitPotionsForUse { get; set; } }
    internal static class HarmonyRuntime { public static void EnsureLoaded() { } }
    internal static class HostStateAdapter
    {
        public static bool IsReady(PlayerSpawner spawner) => spawner && spawner.isServer && spawner.netId != 0 &&
            spawner.connectionToClient?.isReady == true && spawner.connectionToClient.identity != null &&
            spawner.PlayerAvatar && spawner.PlayerAvatar.isServer && spawner.PlayerAvatar.netId != 0 &&
            spawner.PlayerAvatar.Inventory && spawner.PlayerAvatar.Inventory.isServer &&
            spawner.PlayerAvatar.Inventory.netId != 0 && spawner.PlayerAvatar.Inventory.canBroadcast > 0 &&
            spawner.PlayerAvatar.Race != null && !string.IsNullOrEmpty(spawner.PlayerAvatar.playerNameSource) &&
            !string.IsNullOrEmpty(spawner.PlayerAvatar.currentFloorGuid);
    }
}
