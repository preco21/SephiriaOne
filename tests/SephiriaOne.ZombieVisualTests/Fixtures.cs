using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
        public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation) where T : Object
        {
            if (FailInstantiate) throw new InvalidOperationException("instantiate failure");
            var copy = new CharacterDebuff_ChimDamage(); copy.transform.position = position;
            Created.Add(copy); return (T)(Object)copy;
        }
        internal static bool FailInstantiate;
        internal static readonly List<CharacterDebuff_ChimDamage> Created = new();
        public static void Destroy(Object value)
        {
            if (value is GameObject go)
            {
                go.Effect.Destroyed = true; go.Effect.Visual = false;
                go.Effect.netIdentity.Destroyed = true;
            }
            if (value != null) value.Destroyed = true;
        }
    }
    public class GameObject : Object { internal CharacterDebuff Effect; }
    public class Transform { public Vector3 position; }
    public struct Vector3 { public float x; public Vector3(float value) { x = value; } }
    public struct Quaternion { public static Quaternion identity; }
    public static class Debug { internal static int Warnings; public static void LogWarning(object value) { Warnings++; } }
}
namespace Mirror
{
    public class NetworkIdentity : UnityEngine.Object { public uint netId; }
    public static class NetworkServer
    {
        public static bool active = true;
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new();
        internal static uint nextId;
        internal static int Spawns, Destroys;
        internal static Action<CharacterDebuff> DuringSpawn;
        internal static bool FailBeforeSpawn, FailAfterSpawn, FailDestroy;
        public static void Spawn(GameObject go)
        {
            Spawns++;
            if (FailBeforeSpawn) throw new InvalidOperationException("before spawn");
            var effect = go.Effect;
            effect.netIdentity.netId = ++nextId; spawned.Add(nextId, effect.netIdentity);
            DuringSpawn?.Invoke(effect);
            effect.OnStartServer();
            effect.isClient = true; effect.Visual = true; // Mirror host client callback, even when disabled.
            if (FailAfterSpawn) throw new InvalidOperationException("after spawn");
        }
        public static void Destroy(GameObject go)
        {
            Destroys++; spawned.Remove(go.Effect.netIdentity.netId);
            if (FailDestroy) throw new InvalidOperationException("destroy failure");
            UnityEngine.Object.Destroy(go);
        }
    }
}
public class UnitAvatar : UnityEngine.Object { public Transform transform = new(); public bool IsDead; }
public class PlayerAvatar : UnitAvatar { }
public class CharacterDebuff : UnityEngine.Object
{
    public bool enabled = true, isClient, Visual;
    public GameObject gameObject;
    public Transform transform = new();
    public NetworkIdentity netIdentity = new();
    public UnitAvatar NetworkTarget, NetworkAttacker;
    public sbyte NetworkCurrentStack;
    internal int ServerStarts, ServerUpdates, ClientUpdates, GameplayEnds;
    internal CharacterDebuff() { gameObject = new() { Effect = this }; }
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void OnStartServer() { ServerStarts++; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Destroy() { GameplayEnds++; Visual = false; DestroyInner(); }
    protected virtual void DestroyInner() { }
    internal void Frame() { if (enabled && !Destroyed) ServerUpdates++; }
    internal void ClientFrame()
    {
        ClientUpdates++; transform.position = NetworkTarget.transform.position;
        if (SephiriaOne.ZombieVisualNative.FailUpdate) throw new InvalidOperationException("client update failure");
    }
}
public class CharacterDebuff_Poison : CharacterDebuff { }
public struct TestEventReference
{
    public bool HasValue;
    public bool IsNull => !HasValue;
}
public class CharacterDebuff_ChimDamage : CharacterDebuff
{
    public string bonusDamageId = "CHIMDAMAGEBONUS";
    public GameObject damageFxPrefab = new();
    public TestEventReference chimDamageEvent = new() { HasValue = true };
    internal int DamageApplications, Sounds;
    protected override void DestroyInner()
    {
        if (!NetworkTarget) return;
        DamageApplications++;
        if (!chimDamageEvent.IsNull) Sounds++;
    }
}
namespace SephiriaOne
{
    internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceComparer<T> Instance = new();
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
    internal static class FriendlyFireParticipants
    {
        internal sealed class Receipt { internal PlayerAvatar Player; }
        internal static readonly Dictionary<PlayerAvatar, Receipt> Receipts = new();
        internal static Receipt Capture(PlayerAvatar player) => Receipts.TryGetValue(player, out var receipt) ? receipt : null;
    }
    internal static class CombatMatchRuntime
    {
        internal static long MatchToken = 1;
        internal static readonly HashSet<PlayerAvatar> Zombies = new();
        internal static bool IsZombieVisualCurrent(PlayerAvatar player, FriendlyFireParticipants.Receipt receipt, long match) =>
            NetworkServer.active && MatchToken != 0 && match == MatchToken && Zombies.Contains(player) && receipt != null &&
            ReferenceEquals(receipt, FriendlyFireParticipants.Capture(player));
    }
    internal static class ZombieVisualNative
    {
        internal static bool FailContract, FailUpdate;
        internal static int Validations;
        internal static CharacterDebuff_ChimDamage Prefab = new();
        internal static Action<CharacterDebuff> ClientUpdate = effect => effect.ClientFrame();
        internal static bool ValidateNativeCode() => !FailContract;
        internal static CharacterDebuff_ChimDamage ValidateAndGetPrefab()
        {
            Validations++;
            if (FailContract) throw new InvalidOperationException("unsupported contract");
            return Prefab;
        }
    }
}
