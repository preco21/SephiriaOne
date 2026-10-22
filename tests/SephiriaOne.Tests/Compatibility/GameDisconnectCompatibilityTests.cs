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
        Type result = steam.GetType("Steamworks.EResult", true)!;
        var send = AccessTools.DeclaredMethod(server.BaseType, "SendSocket", new[] { socket, typeof(byte[]), typeof(int) });
        var map = AccessTools.DeclaredField(server, "connToMirrorID");
        var expectedMap = transport.GetType("Mirror.FizzySteam.BidirectionalDictionary`2", true)!.MakeGenericType(socket, typeof(int));
        if (send == null || send.IsStatic || send.ReturnType != result || map?.FieldType != expectedMap)
            throw new Exception("Native Steam send/result/connection mapping contract changed.");
        Type sendHooks = addon.GetType("SephiriaOne.TransportSendDiagnostics", true)!;
        var observe = AccessTools.DeclaredMethod(sendHooks, "AfterSend");
        if (observe == null || observe.ReturnType != typeof(void) ||
            !observe.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(object), socket, typeof(byte[]), typeof(int), result }) ||
            !observe.GetMethodBody()!.ExceptionHandlingClauses.Any())
            throw new Exception("Send observer must preserve inputs/result and contain diagnostic failures.");
        var nativeSend = PatchProcessor.GetOriginalInstructions(send).Where(i => i.operand is MethodInfo).Select(i => (MethodInfo)i.operand).ToList();
        if (nativeSend.Count(m => m.DeclaringType == api && m.Name == "SendMessageToConnection") != 1)
            throw new Exception("Native send no longer represents one transport attempt.");
        Type networkServer = mirror.GetType("Mirror.NetworkServer", true)!;
        var disconnected = AccessTools.DeclaredMethod(networkServer, "OnTransportDisconnected", new[] { typeof(int) });
        var cleanup = PatchProcessor.GetOriginalInstructions(disconnected).Where(i => i.operand is MethodInfo)
            .Select(i => (MethodInfo)i.operand).ToList();
        int retireIndex = cleanup.FindIndex(m => m.DeclaringType == networkServer && m.Name == "RemoveConnection");
        int notifyIndex = cleanup.FindIndex(m => m.Name == "Invoke" && m.DeclaringType!.IsGenericType &&
            m.DeclaringType.GetGenericTypeDefinition() == typeof(Action<>));
        if (retireIndex < 0 || notifyIndex <= retireIndex)
            throw new Exception("Native connection-retirement/callback order changed; snapshot timing needs review.");
        var beforeClose = AccessTools.DeclaredMethod(sendHooks, "BeforeClose");
        foreach (string prefix in new[] { "BeforeSocketClose", "BeforeServerClose" })
            if (!PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(hooks, prefix)).Any(i => i.Calls(beforeClose)))
                throw new Exception("Live projectile snapshot no longer precedes native transport cleanup: " + prefix);
        Console.WriteLine("Verified installed-game disconnect diagnostic hooks and Steam close-reason API.");
    }
}
