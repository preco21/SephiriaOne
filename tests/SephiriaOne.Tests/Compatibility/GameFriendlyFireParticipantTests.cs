using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireParticipantTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type spawner = game.GetType("PlayerSpawner", true)!;
        Type player = game.GetType("PlayerAvatar", true)!;
        Type manager = game.GetType("HorayNetworkManager", true)!;
        Assembly mirror = spawner.BaseType!.Assembly;
        Type server = mirror.GetType("Mirror.NetworkServer", true)!;
        Type connection = mirror.GetType("Mirror.NetworkConnectionToClient", true)!;
        Type networkManager = mirror.GetType("Mirror.NetworkManager", true)!;
        void Require(bool value, string reason)
        { if (!value) throw new Exception("Participant lifecycle contract: " + reason); }
        List<CodeInstruction> Code(Type type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, method)).ToList();
        int Call(List<CodeInstruction> code, string name) => code.FindIndex(i => i.operand is MethodInfo m && m.Name == name);
        MethodInfo Signature(Type type, string name, Type result, bool isStatic, params Type[] parameters)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters);
            Require(method != null && method.ReturnType == result && method.IsStatic == isStatic, type.Name + "." + name);
            return method!;
        }
        Signature(spawner, "OnDestroy", typeof(void), false);
        Signature(spawner, "OnStartServer", typeof(void), false);
        Type initialization = addon.GetType("SephiriaOne.NativePlayerInitialization", true)!;
        var initialize = (MethodInfo)AccessTools.Method(initialization, "ResolveInitialize").Invoke(null, new object[] { spawner })!;
        Require(initialize.ReturnType == typeof(bool) && !initialize.IsStatic &&
            initialize.GetParameters().Take(4).Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int), typeof(string), typeof(string), typeof(int) }),
            "shared initialization adapter resolves native player inputs");
        Type? placement = game.GetType("ERestartPlacement");
        Require(initialize.GetParameters().Length == (placement == null ? 4 : 5) &&
            (placement == null || initialize.GetParameters()[4].ParameterType == placement), "audited placement-aware initialization boundary");
        Signature(manager, "OnServerDisconnect", typeof(void), false, connection);
        foreach (string name in new[] { "OnStartServer", "OnStopServer", "OnStopClient" })
            Signature(manager, name, typeof(void), false);
        Signature(server, "RemoveConnection", typeof(bool), true, typeof(int));
        Signature(server, "AddConnection", typeof(bool), true, connection);
        var awake = Code(spawner, "Awake");
        int playerComponent = awake.FindIndex(i => i.operand is MethodInfo m && m.Name == "GetComponent" &&
            m.IsGenericMethod && m.GetGenericArguments().SequenceEqual(new[] { player }));
        Require(Call(awake, "Add") >= 0 && Call(awake, "Add") < playerComponent, "roster insertion precedes avatar assignment");
        var playerAwake = Code(player, "Awake");
        Require(playerAwake.Any(i => i.operand is MethodInfo m && m.Name == "GetComponent" && m.IsGenericMethod &&
            m.GetGenericArguments().SequenceEqual(new[] { spawner })), "avatar and spawner share their GameObject identity");
        var destroy = Code(spawner, "OnDestroy");
        Require(Call(destroy, "RemoveAuthority") >= 0 && Call(destroy, "RemoveAuthority") < Call(destroy, "Remove"),
            "destroy prefix must retire before authority callbacks and roster removal");
        var disconnect = Code(manager, "OnServerDisconnect");
        Require(Call(disconnect, "TempSavePlayerSessionsBeforeDisconnect") >= 0 &&
            Call(disconnect, "TempSavePlayerSessionsBeforeDisconnect") < Call(disconnect, "OnServerDisconnect"),
            "game disconnect saves before base destruction");
        var transport = Code(server, "OnTransportDisconnected");
        int eventRead = transport.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnDisconnectedEvent");
        Require(Call(transport, "Cleanup") >= 0 && Call(transport, "Cleanup") < Call(transport, "RemoveConnection") &&
            Call(transport, "RemoveConnection") < eventRead, "transport removal precedes game disconnect event");
        var stopClient = Code(networkManager, "StopClient");
        Require(Call(stopClient, "OnServerDisconnect") >= 0 && Call(stopClient, "OnServerDisconnect") < Call(stopClient, "Disconnect"),
            "host callback may run before client disconnection");
        var stopServer = Code(networkManager, "StopServer");
        Require(Call(stopServer, "OnStopServer") >= 0 && Call(stopServer, "OnStopServer") < Call(stopServer, "Shutdown"),
            "server stop callback precedes shutdown");
        var restart = AccessTools.DeclaredMethod(spawner, "RestartNewGame", placement == null
            ? new[] { typeof(int) } : new[] { typeof(int), placement });
        Require(restart != null && PatchProcessor.GetOriginalInstructions(restart).Any(i => i.Calls(initialize)) &&
            Call(Code(player, "LocalEquipCostume"), "Initialize") < 0, "run initialization differs from wardrobe changes");
        foreach (string name in new[] { "FriendlyFireParticipantHooks", "StartingResourceHooks", "InventoryResourceHooks" })
            Require(Code(addon.GetType("SephiriaOne." + name, true)!, "Install").Any(i =>
                i.operand is MethodInfo m && m.DeclaringType == initialization && m.Name == "ResolveInitialize"),
                name + " installs at the shared initialization boundary");
        Require(Call(Code(manager, "OnStartServer"), "NotifyStartGameServerside") < Call(Code(manager, "OnStartServer"), "NewGame") &&
            Call(Code(manager, "NewGame"), "NotifyStartSessionServerside") >= 0, "server and run start event boundaries");
        Type hooks = addon.GetType("SephiriaOne.FriendlyFireHooks", true)!;
        Require(Code(hooks, "Install").Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "FriendlyFireParticipantHooks" && m.Name == "Install"),
            "participant hooks share combat installation and rollback ownership");
        Type participants = addon.GetType("SephiriaOne.FriendlyFireParticipants", true)!;
        foreach (var method in participants.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            if (method.GetMethodBody() != null)
                Require(!PatchProcessor.GetOriginalInstructions(method).Any(i => i.operand is MethodInfo m &&
                    m.DeclaringType?.Name == "SaveData" && m.Name.StartsWith("Set", StringComparison.Ordinal)), "selection never writes native saves");
        var capture = Code(participants, "Capture");
        Require(!capture.Any(i => i.opcode == OpCodes.Ldftn), "cached lifetime callbacks avoid C# 8 method-group allocations per capture");
        Console.WriteLine("Verified native participant identity, roster, initialization, connection retirement and server/run boundaries (not live multiplayer).");
    }
}
