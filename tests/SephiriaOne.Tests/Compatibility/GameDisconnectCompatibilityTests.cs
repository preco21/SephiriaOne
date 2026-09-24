using System.Reflection;
using HarmonyLib;

internal static class GameDisconnectCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var transport = Assembly.Load("FizzySteamworks");
        var steam = Assembly.Load("com.rlabrecque.steamworks.net");
        var mirror = Assembly.Load("Mirror");
        Type server = transport.GetType("Mirror.FizzySteam.NextServer", true)!;
        Type socket = steam.GetType("Steamworks.HSteamNetConnection", true)!;
        Type info = steam.GetType("Steamworks.SteamNetConnectionInfo_t", true)!;
        Type api = steam.GetType("Steamworks.SteamNetworkingSockets", true)!;
        Type hooks = addon.GetType("SephiriaOne.DisconnectDiagnostics", true)!;
        foreach (var target in new[]
        {
            (server, "InternalDisconnect", new[] { typeof(int), socket }, "BeforeSocketClose"),
            (server, "Disconnect", new[] { typeof(int) }, "BeforeServerClose"),
            (game.GetType("HorayNetworkManager", true)!, "OnServerDisconnect",
                new[] { mirror.GetType("Mirror.NetworkConnectionToClient", true)! }, "BeforePlayerRemoval")
        })
        {
            var method = AccessTools.DeclaredMethod(target.Item1, target.Item2, target.Item3);
            var prefix = AccessTools.DeclaredMethod(hooks, target.Item4);
            if (method == null || method.IsStatic || method.ReturnType != typeof(void) || prefix == null ||
                !prefix.GetParameters().Select(p => p.ParameterType).SequenceEqual(target.Item3))
                throw new Exception("Disconnect diagnostic boundary changed: " + target.Item2);
            if (!prefix.GetMethodBody()!.ExceptionHandlingClauses.Any() && target.Item4 != "BeforeServerClose")
                throw new Exception("Diagnostic boundary no longer contains exceptions: " + target.Item4);
        }
        var close = AccessTools.DeclaredMethod(server, "InternalDisconnect", new[] { typeof(int), socket });
        if (!PatchProcessor.GetOriginalInstructions(close).Any(i => i.operand is MethodInfo m && m.DeclaringType == api && m.Name == "CloseConnection"))
            throw new Exception("Native socket close moved; diagnostic timing needs review.");
        var getInfo = AccessTools.DeclaredMethod(api, "GetConnectionInfo", new[] { socket, info.MakeByRefType() });
        if (getInfo?.ReturnType != typeof(bool) || info.GetProperty("m_szEndDebug")?.PropertyType != typeof(string) ||
            info.GetField("m_eEndReason")?.FieldType != typeof(int) || info.GetField("m_eState") == null)
            throw new Exception("Steam close-reason API changed.");
        Console.WriteLine("Verified installed-game disconnect diagnostic hooks and Steam close-reason API.");
    }
}
