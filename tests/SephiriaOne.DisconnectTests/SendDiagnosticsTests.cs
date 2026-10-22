using Mirror;
using Mirror.FizzySteam;
using SephiriaOne;
using Steamworks;
using UnityEngine;

internal static class SendDiagnosticsTests
{
    internal static void Run(Action<bool, string> check)
    {
        var server = new NextServer();
        var socket = new HSteamNetConnection(99);
        var connection = new NetworkConnectionToClient { connectionId = 12 };
        NetworkServer.connections[12] = connection; server.Map(12, socket);
        byte[] payload = new byte[256];
        int count = Debug.Lines.Count;
        NextCommon.Result = EResult.k_EResultLimitExceeded;
        for (int i = 0; i < 100; i++) check(server.SendFixture(socket, payload, 0) == NextCommon.Result,
            "Diagnostic preserves failed send return");
        check(Debug.Lines.Count(l => l.Contains("send-failure conn=12")) == 1,
            "First failed send names recipient and throttles subsequent failures");
        check(Debug.Lines.Last().Contains("playerBullets=12"), "Failure includes read-only limiter counts");
        Time.realtimeSinceStartup = 5;
        server.SendFixture(socket, payload, 1);
        check(Debug.Lines.Any(l => l.Contains("failures=101") && l.Contains("failedSendBytes=25957") &&
            l.Contains("reliable=100") && l.Contains("unreliable=1")), "Throttled summary counts failed sends and bytes per connection");
        var replacement = new NetworkConnectionToClient { connectionId = 12 };
        NetworkServer.connections[12] = replacement;
        server.SendFixture(socket, payload, 0);
        check(Debug.Lines.Last().Contains("failures=1;"), "Reused connection ID starts fresh diagnostic receipt");
        new HorayNetworkManager().OnServerDisconnect(replacement);
        server.SendFixture(socket, payload, 0);
        check(Debug.Lines.Last().Contains("failures=1;"), "Removal forgets diagnostics even on literal connection reuse");
        NextCommon.Result = EResult.k_EResultOK;
        count = Debug.Lines.Count; int reads = ProjectileLimitRuntime.Reads;
        for (int i = 0; i < 1000; i++) server.SendFixture(socket, payload, 0);
        check(Debug.Lines.Count == count && ProjectileLimitRuntime.Reads == reads, "Successful sends perform no snapshot/logging work");
        NextCommon.Result = EResult.k_EResultLimitExceeded;
        ProjectileLimitRuntime.Throw = true; Time.realtimeSinceStartup = 10;
        check(server.SendFixture(socket, payload, 0) == NextCommon.Result, "Snapshot failure never alters native send");
        check(Debug.Lines.Last().Contains("send-failure conn=12") && Debug.Lines.Last().Contains("snapshot unavailable"),
            "Optional projectile snapshot failure retains core send diagnostics");
        ProjectileLimitRuntime.Throw = false; Debug.Throw = true; Time.realtimeSinceStartup = 15;
        check(server.SendFixture(socket, payload, 0) == NextCommon.Result, "Log sink failure never alters native send");
        Debug.Throw = false; NetworkServer.connections.Clear();
        check(server.SendFixture(socket, payload, 0) == NextCommon.Result, "Missing connection map remains native");
        check(server.NativeSends == 1106 && server.NativeCloses == 0, "No retries, extra sends or disconnects from diagnostics");
        // Native transport cleanup removes the connection/ledger before the
        // game-manager callback. The pre-close snapshot must run before that.
        NetworkServer.connections[12] = replacement;
        server.OnClose = () =>
        {
            check(Debug.Lines.Last().Contains("transport-close conn=12") &&
                Debug.Lines.Any(l => l.Contains("pre-close conn=12") && l.Contains("playerBullets=12")),
                "Snapshot taken before native connection and projectile-ledger retirement");
            NetworkServer.connections.Clear();
            ProjectileLimitRuntime.Throw = true;
            new HorayNetworkManager().OnServerDisconnect(replacement);
            ProjectileLimitRuntime.Throw = false;
        };
        server.Lose(12);
        server.OnClose = null;
        SteamNetworkingSockets.Info.m_szEndDebug = "fixture reason";
        NextCommon.Result = EResult.k_EResultOK;
        SessionSettings.Reads = 0; Debug.Lines.Clear();
    }
}
