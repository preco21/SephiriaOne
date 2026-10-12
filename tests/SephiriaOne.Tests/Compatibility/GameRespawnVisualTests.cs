using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameRespawnVisualTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type adapter = addon.GetType("SephiriaOne.NativeReviveVisuals", true)!;
        Type unit = game.GetType("UnitAvatar", true)!;
        List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        void Require(bool value, string why) { if (!value) throw new Exception("Respawn FX contract: " + why); }
        var sender = AccessTools.DeclaredMethod(unit, "RpcCreateAdditionalLifeFx");
        var receiver = AccessTools.DeclaredMethod(unit, "InvokeUserCode_RpcCreateAdditionalLifeFx");
        var body = AccessTools.DeclaredMethod(unit, "UserCode_RpcCreateAdditionalLifeFx");
        Require((bool)AccessTools.DeclaredMethod(adapter, "Validate").Invoke(null, null)!,
            "native white pillar transport\n" + string.Join("\n", Code(sender).Concat(Code(receiver)).Concat(Code(body))));
        foreach (string mutation in new[] { "name", "hash", "channel", "owner", "payload", "reader", "fx", "position", "gameplay", "write" })
        {
            var send = Code(sender); var read = Code(receiver); var visual = Code(body);
            int dispatch = send.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            switch (mutation)
            {
                case "name": send.First(i => i.opcode == OpCodes.Ldstr).operand = "changed"; break;
                case "hash": send.First(i => i.LoadsConstant(-2080231604)).operand = 1; break;
                case "channel": send[dispatch - 2].opcode = OpCodes.Ldc_I4_1; break;
                case "owner": send[dispatch - 1].opcode = OpCodes.Ldc_I4_0; break;
                case "payload": send.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(string), "Concat", new[] { typeof(string), typeof(string) }))); break;
                case "reader": read.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(unit, "ForceDie"))); break;
                case "fx": visual.First(i => i.LoadsConstant("AdditionalLifeReviveFx")).operand = "changed"; break;
                case "position": visual[visual.FindIndex(i => i.operand is MethodInfo m && m.Name == "get_transform") - 1].opcode = OpCodes.Ldnull; break;
                case "gameplay": visual.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(unit, "ForceDie"))); break;
                case "write": visual.Add(new CodeInstruction(OpCodes.Stsfld)); break;
            }
            Require(!(bool)AccessTools.DeclaredMethod(adapter, "ValidateCode").Invoke(null, new object[] { send, read, visual })!,
                "changed " + mutation + " fails closed");
        }
        Require(Code(unit.TypeInitializer!).Any(i => Equals(i.operand, receiver)), "stock clients register the pillar effect");
        var nativeRevive = Code(AccessTools.DeclaredMethod(unit, "Revive"));
        Require(!nativeRevive.Any(i => Equals(i.operand, sender)), "ordinary native revival does not already broadcast this effect");
        var receiveRevive = Code(AccessTools.DeclaredMethod(unit, "UserCode_RpcRevive"));
        Require(!receiveRevive.Any(i => i.operand is MethodInfo m && m.Name == "Spawn"), "native client revival does not duplicate a pillar");
        var show = Code(AccessTools.DeclaredMethod(adapter, "Show"));
        Require(!show.Any(i => i.operand is MethodInfo m && new[] { "Revive", "ReviveAnotherPlayer", "RpcRevived", "ApplyDamage", "SetHp" }.Contains(m.Name)),
            "visual adapter never calls gameplay revival or changes HP");
        var ui = game.GetType("UI_ReviveEffect", true)!;
        Require(Code(AccessTools.DeclaredMethod(ui, "OnOpened")).Any(i => i.operand is MethodInfo m && m.Name == "Play") &&
            Code(AccessTools.DeclaredMethod(ui, "Update")).Any(i => i.operand is MethodInfo m && m.Name == "Close"),
            "native visual includes its documented brief flash/sound and automatic close");
        var runtime = addon.GetType("SephiriaOne.CombatMatchRuntime", true)!;
        var restore = Code(AccessTools.DeclaredMethod(runtime, "Restore"));
        int revived = restore.FindIndex(i => i.operand is MethodInfo m && m.Name == "TryRevive");
        int effect = restore.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == adapter && m.Name == "Show");
        Require(revived >= 0 && effect > revived &&
            !Code(AccessTools.DeclaredMethod(runtime, "DrainRecovery")).Any(i => i.operand is MethodInfo m && m.DeclaringType == adapter),
            "effect belongs to successful countdown restoration, never immediate match cleanup");
        Console.WriteLine("Verified stock white revive pillar, empty reliable RPC, avatar world transform, native flash/sound and countdown-only completion (not live rendering).");
    }
}
