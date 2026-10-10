using System.Runtime.CompilerServices;

namespace Mirror
{
    public sealed class NetworkIdentity : UnityEngine.Object { public uint netId; }
    public sealed partial class NetworkConnectionToClient
    {
        public int connectionId;
        public NetworkIdentity identity;
    }
    public static partial class NetworkServer
    {
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new();
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool AddConnection(NetworkConnectionToClient conn)
        {
            if (connections.ContainsKey(conn.connectionId)) return false;
            connections.Add(conn.connectionId, conn); return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool RemoveConnection(int connectionId) => connections.Remove(connectionId);
    }
}

public sealed partial class PlayerSpawner
{
    public Mirror.NetworkIdentity netIdentity;
    public Action Initializing, Destroying;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnStartServer() { }
    [MethodImpl(MethodImplOptions.NoInlining)] private void OnDestroy()
    { Destroying?.Invoke(); MultiplayerList.Remove(this); }
    public void DestroyForTest() => OnDestroy();
}

public sealed partial class PlayerAvatar
{
    public Mirror.NetworkIdentity netIdentity;
    public Mirror.NetworkConnectionToClient connectionToClient;
}

public sealed class HorayNetworkManager
{
    public Action Disconnecting, Stopping;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnServerDisconnect(Mirror.NetworkConnectionToClient conn)
    { Disconnecting?.Invoke(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnStopServer() { Stopping?.Invoke(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnStopClient() { Stopping?.Invoke(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnStartServer() { }
}

internal static class FriendlyFireParticipantFixtures
{
    internal static void Connect(PlayerSpawner spawner, uint id)
    {
        var identity = new Mirror.NetworkIdentity { netId = id };
        spawner.netIdentity = spawner.PlayerAvatar.netIdentity = identity;
        var connection = spawner.connectionToClient;
        connection.connectionId = checked((int)id - 1);
        connection.identity = identity;
        spawner.PlayerAvatar.connectionToClient = connection;
        if (Mirror.NetworkServer.connections.ContainsKey(connection.connectionId))
            Mirror.NetworkServer.RemoveConnection(connection.connectionId);
        Mirror.NetworkServer.AddConnection(connection);
        Mirror.NetworkServer.spawned[id] = identity;
    }
}
