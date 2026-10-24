using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object obj) => obj != null && !obj.Destroyed;
    }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public static class Debug { public static int Warnings; public static void LogWarning(object _) => Warnings++; }
}
namespace Mirror
{
    public class NetworkConnectionToClient
    {
        public int connectionId;
        public bool isReady = true;
        public NetworkIdentity identity;
        public int Effects;
        public Vector3 Position;
        public bool Ex, Ice, Fail;
        public Action OnSend;
    }
    public class NetworkIdentity : UnityEngine.Object
    {
        public uint netId;
        public PlayerSpawner Player;
        public readonly Dictionary<int, NetworkConnectionToClient> observers = new();
        public T GetComponent<T>() where T : class => Player as T;
    }
    public static class NetworkServer
    {
        public static bool active = true;
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new();
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new();
    }
}
public class UnitAvatar : UnityEngine.Object { }
public class PlayerAvatar : UnitAvatar
{
    public string currentFloorGuid = "floor";
    public bool IsDead, Retired;
    public int Generation, Damage;
    public PlayerSpawner spawner;
}
public class PlayerSpawner : UnityEngine.Object
{
    public PlayerAvatar PlayerAvatar;
    public NetworkConnectionToClient connectionToClient;
    public NetworkIdentity netIdentity;
}
public class ComboEffect_DarkCloud : UnityEngine.Object
{
    public bool isServer = true, isPlayerCombo = true, Ex, Ice, Reject, ThrowBeforeFx;
    public bool RejectPrefix;
    public uint netId => netIdentity.netId;
    public NetworkIdentity netIdentity;
    public NetworkConnectionToClient connectionToClient;
    public UnitAvatar Networkavatar;
    public Vector3 Position = new(10, 20, 3);
    public Action Callback;
    public int Attacks;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void FireLightning(UnitAvatar target, int baseDamagePercent = 100)
    {
        if (Reject) return;
        Attacks++;
        if (target is PlayerAvatar p) p.Damage++;
        Callback?.Invoke();
        if (ThrowBeforeFx) throw new InvalidOperationException("native strike error");
        TargetCreateFx(connectionToClient, Position, Ex, Ice);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void TargetCreateFx(NetworkConnectionToClient conn, Vector3 targetPos, bool ex, bool isIce)
    {
        conn.Effects++; conn.Position = targetPos; conn.Ex = ex; conn.Ice = isIce;
        conn.OnSend?.Invoke();
        if (conn.Fail) throw new InvalidOperationException("ambiguous send");
    }
}
namespace SephiriaOne
{
    internal static class NativeStrikeLimiter
    {
        internal static bool BeforeStrike(ComboEffect_DarkCloud __instance) => !__instance.RejectPrefix;
    }
    internal static class FriendlyFireRuntime
    {
        internal static bool Enabled = true, HasDamage = true;
        internal static Func<PlayerAvatar, PlayerAvatar, bool> Pair = (_, _) => true;
        internal static bool CanShowDarkCloudStrike(PlayerAvatar source, PlayerAvatar target) => Enabled && HasDamage && Pair(source, target);
    }
    internal static class FriendlyFireParticipants
    {
        internal static int Scope;
        private static readonly Dictionary<PlayerAvatar, Receipt> receipts = new();
        internal sealed class Receipt
        {
            internal PlayerAvatar Player;
            internal NetworkConnectionToClient Connection;
            internal int Generation, Scope;
        }
        internal static Receipt Capture(PlayerAvatar player)
        {
            if (!player || player.Retired || !player.spawner || !player.spawner.netIdentity ||
                !NetworkServer.connections.TryGetValue(player.spawner.connectionToClient.connectionId, out var live) ||
                !ReferenceEquals(live, player.spawner.connectionToClient)) return null;
            if (receipts.TryGetValue(player, out var old) && IsCurrent(old)) return old;
            return receipts[player] = new() { Player = player, Connection = live, Generation = player.Generation, Scope = Scope };
        }
        internal static bool IsCurrent(Receipt receipt) => receipt != null && receipt.Scope == Scope && receipt.Player &&
            !receipt.Player.Retired && receipt.Player.Generation == receipt.Generation &&
            ReferenceEquals(receipt.Connection, receipt.Player.spawner.connectionToClient) &&
            NetworkServer.connections.TryGetValue(receipt.Connection.connectionId, out var live) && ReferenceEquals(receipt.Connection, live);
    }
}
