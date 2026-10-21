using System.Reflection;
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
        Console.WriteLine("Verified native party-teleport host/owner RPC and command call path (not live movement).");
    }
}
