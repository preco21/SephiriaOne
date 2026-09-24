using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class Debug
    {
        public static readonly List<string> Lines = new();
        public static bool Throw;
        public static void LogWarning(object value) { if (Throw) throw new Exception("logger failed"); Lines.Add(value.ToString()); }
    }
}
namespace Steamworks
{
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
    public class NetworkIdentity { public uint netId = 78; public static implicit operator bool(NetworkIdentity value) => value != null; }
    public class NetworkConnectionToClient { public int connectionId = 1; public NetworkIdentity identity = new(); }
    namespace FizzySteam
    {
        public class NextServer
        {
            public int NativeCloses;
            [MethodImpl(MethodImplOptions.NoInlining)] private void InternalDisconnect(int connId, Steamworks.HSteamNetConnection socket)
            { NativeCloses++; Steamworks.SteamNetworkingSockets.Info.m_szEndDebug = "overwritten by native close"; }
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
    internal static class SessionSettings
    {
        public static int Reads;
        public static bool Throw;
        public static IEnumerable<string> DescribeDisconnect(uint id)
        { Reads++; if (Throw) throw new Exception("snapshot failed"); return new[] { "snapshot player=" + id }; }
    }
}
