using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameTeleportCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var native = game.GetType("CombatBehaviour", true)!;
        var request = AccessTools.DeclaredMethod(native, "ReqSetPosition");
        var target = AccessTools.DeclaredMethod(native, "TargetSetPosition");
        var receiver = AccessTools.DeclaredMethod(native, "UserCode_TargetSetPosition__NetworkConnectionToClient__Vector3__Boolean");
        var set = AccessTools.DeclaredMethod(native, "SetPosition");
        if (request == null || !request.IsPublic || target == null || receiver == null || set == null ||
            !PatchProcessor.GetOriginalInstructions(request).Any(i => i.Calls(target)) ||
            !PatchProcessor.GetOriginalInstructions(receiver).Any(i => i.Calls(set)) ||
            !PatchProcessor.GetOriginalInstructions(target).Any(i => i.operand is MethodInfo m && m.Name == "SendTargetRPCInternal"))
            throw new Exception("Native host-to-owner teleport transport changed.");
        var together = AccessTools.DeclaredMethod(game.GetType("TeleportTogether"), "UserCode_CmdTeleport__Vector3");
        if (!PatchProcessor.GetOriginalInstructions(together).Any(i => i.Calls(request)))
            throw new Exception("Native party teleport no longer uses ReqSetPosition.");
        var action = addon.GetType("SephiriaOne.TeleportAllAction", true)!;
        var execute = AccessTools.DeclaredMethod(action, "TryExecute");
        var code = PatchProcessor.GetOriginalInstructions(execute).ToList();
        if (code.Count(i => i.Calls(request)) != 1 || code.Any(i => i.operand is MethodInfo m && m.Name == "set_position"))
            throw new Exception("Party command must request native movement, not overwrite a client-owned transform.");
        var dungeon = game.GetType("DungeonManager", true)!;
        var player = game.GetType("PlayerAvatar", true)!;
        var countdown = AccessTools.PropertyGetter(player, "MovingFloorViaWorldmap");
        var countdownGuard = AccessTools.DeclaredMethod(action, "PartyTravelCountdown");
        if (countdown == null || !countdown.IsPublic || !PatchProcessor.GetOriginalInstructions(countdownGuard).Any(i => i.Calls(countdown)) ||
            !PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(player, "Update")).Any(i => i.Calls(AccessTools.DeclaredMethod(dungeon, "MoveTogether"))))
            throw new Exception("Party countdown must be excluded before the native all-player travel enqueue.");
        var queue = AccessTools.DeclaredField(dungeon, "travelingTransactions");
        if (queue == null || !queue.FieldType.IsGenericType || queue.FieldType.GetGenericTypeDefinition() != typeof(List<>))
            throw new Exception("Native travel queue layout changed.");
        var transaction = queue.FieldType.GetGenericArguments()[0];
        var owner = AccessTools.DeclaredField(transaction, "avatar");
        var overridePosition = AccessTools.DeclaredField(transaction, "overridePosition");
        var move = AccessTools.DeclaredMethod(dungeon, "MoveFloor");
        var localMove = AccessTools.DeclaredMethod(dungeon, "LocalMoveFloor");
        var update = AccessTools.DeclaredMethod(dungeon, "Update");
        var prepare = AccessTools.DeclaredMethod(player, "ServerPrepareTravel");
        List<CodeInstruction> IL(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        if (owner?.FieldType != player || move == null || !move.IsPublic || move.GetParameters().Length != 10 ||
            move.GetParameters()[9].ParameterType != overridePosition?.FieldType ||
            Nullable.GetUnderlyingType(overridePosition.FieldType) != request.GetParameters()[0].ParameterType ||
            !IL(move).Any(i => i.Calls(localMove))) throw new Exception("Server travel override/owner contract changed.");
        var enqueue = IL(localMove); var completion = IL(update);
        if (!enqueue.Any(i => i.opcode == OpCodes.Stfld && Equals(i.operand, overridePosition)) ||
            !enqueue.Any(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, queue)) ||
            !enqueue.Any(i => i.Calls(AccessTools.PropertySetter(player, "NetworkloadingScreenType"))) ||
            !completion.Any(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, overridePosition)) ||
            !completion.Any(i => i.Calls(prepare)) || !completion.Any(i => i.Calls(request)) ||
            !completion.Any(i => i.Calls(AccessTools.PropertyGetter(game.GetType("FloorGenerator"), "GenerateSuccess"))) ||
            !completion.Any(i => i.operand is FieldInfo field && field.Name == "eachPlayersPosition") ||
            !completion.Any(i => i.Calls(AccessTools.DeclaredMethod(dungeon, "ReleaseUnusedFloors"))))
            throw new Exception("Native travel must retain generation wait, preparation, position override and occupancy cleanup.");
        foreach (string fieldName in new[] { "currentFloorGuid", "currentSpawnPoint", "loadingScreenType" })
        {
            var field = AccessTools.DeclaredField(player, fieldName);
            if (!completion.Any(i => i.Calls(AccessTools.PropertySetter(player, "Network" + fieldName))) ||
                IL(AccessTools.DeclaredMethod(player, "SerializeSyncVars")).Count(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field)) != 2 ||
                IL(AccessTools.DeclaredMethod(player, "DeserializeSyncVars")).Count(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, field)) != 2)
                throw new Exception("Travel state must replicate to initial and current stock clients: " + fieldName);
        }
        var adapter = addon.GetType("SephiriaOne.NativeFloorTravel", true)!;
        var dispatch = AccessTools.DeclaredMethod(adapter, "Request");
        if (!(bool)AccessTools.DeclaredMethod(adapter, "Bind").Invoke(null, null)! ||
            IL(dispatch).Count(i => i.Calls(move)) != 1 || !code.Any(i => i.Calls(dispatch)) ||
            IL(dispatch).Any(i => i.operand is MethodInfo m && (m.Name == "set_position" || m.Name == "ReqSetPosition")))
            throw new Exception("Cross-floor adapter must delegate both transfer and final position to native travel.");
        Console.WriteLine("Verified native party-teleport host/owner RPC and command call path (not live movement).");
        Console.WriteLine("Verified native cross-floor travel queue, override position, preparation and stock floor/loading replication (not live client arrival).");
    }
}
