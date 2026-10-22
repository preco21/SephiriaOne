using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public static class Debug
    {
        public static readonly List<string> Lines = new();
        public static bool Throw;
        public static void LogWarning(object value) { if (Throw) throw new Exception("logger failed"); Lines.Add(value.ToString()); }
    }
}
namespace Steamworks
{
    public enum EResult { k_EResultOK = 1, k_EResultNoConnection = 3, k_EResultInvalidParam = 8, k_EResultLimitExceeded = 25 }
    public readonly record struct HSteamNetConnection(uint Value);
    public struct SteamNetConnectionInfo_t { public string m_eState; public string m_szEndDebug { get; set; } public int m_eEndReason; }
    public static class SteamNetworkingSockets
    {
        public static bool Available = true, Throw;
        public static SteamNetConnectionInfo_t Info = new() { m_eState = "ClosedByPeer", m_eEndReason = 1001, m_szEndDebug = "fixture reason" };
        public static bool GetConnectionInfo(HSteamNetConnection socket, out SteamNetConnectionInfo_t info)
        { if (Throw) throw new Exception("Steam query failed"); info = Info; return Available; }
    }
}
namespace Mirror
{
    public static class NetworkServer
    {
        public static bool active = true;
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new();
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new();
    }
    public class NetworkIdentity { public uint netId = 78; public static implicit operator bool(NetworkIdentity value) => value != null; }
    public class NetworkConnectionToClient { public int connectionId = 1; public NetworkIdentity identity = new(); }
    namespace FizzySteam
    {
        public class BidirectionalDictionary<T, U>
        {
            private readonly Dictionary<T, U> values = new();
            public void Add(T key, U value) => values[key] = value;
            public bool TryGetValue(T key, out U value) => values.TryGetValue(key, out value);
        }
        public class NextCommon
        {
            public static Steamworks.EResult Result = Steamworks.EResult.k_EResultOK;
            public int NativeSends;
            [MethodImpl(MethodImplOptions.NoInlining)] protected Steamworks.EResult SendSocket(Steamworks.HSteamNetConnection conn, byte[] data, int channelId)
            { NativeSends++; Array.Resize(ref data, data.Length + 1); data[data.Length - 1] = (byte)channelId; return Result; }
        }
        public class NextServer : NextCommon
        {
            private readonly BidirectionalDictionary<Steamworks.HSteamNetConnection, int> connToMirrorID = new();
            public int NativeCloses;
            public Action OnClose;
            public void Map(int id, Steamworks.HSteamNetConnection socket) => connToMirrorID.Add(socket, id);
            public Steamworks.EResult SendFixture(Steamworks.HSteamNetConnection socket, byte[] data, int channel) => SendSocket(socket, data, channel);
            [MethodImpl(MethodImplOptions.NoInlining)] private void InternalDisconnect(int connId, Steamworks.HSteamNetConnection socket)
            { NativeCloses++; OnClose?.Invoke(); Steamworks.SteamNetworkingSockets.Info.m_szEndDebug = "overwritten by native close"; }
            public void Lose(int id) => InternalDisconnect(id, new(10));
            [MethodImpl(MethodImplOptions.NoInlining)] public void Disconnect(int connectionId) { NativeCloses++; }
        }
    }
}
public class HorayNetworkManager
{
    public int NativeRemovals;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnServerDisconnect(Mirror.NetworkConnectionToClient conn)
    { NativeRemovals++; if (conn != null) conn.identity = null; }
}
namespace SephiriaOne
{
    internal static class ProjectileLimitRuntime
    {
        public static bool Throw;
        public static int Reads;
        internal static string DescribeDiagnostics(Mirror.NetworkConnectionToClient connection)
        { Reads++; if (Throw) throw new Exception("projectile snapshot failed"); return "projectile-limits enabled=True; playerBullets=12; playerEmitters=2"; }
    }
    internal static class SessionSettings
    {
        public static int Reads;
        public static bool Throw;
        public static IEnumerable<string> DescribeDisconnect(uint id)
        { Reads++; if (Throw) throw new Exception("snapshot failed"); return new[] { "snapshot player=" + id }; }
    }
}
