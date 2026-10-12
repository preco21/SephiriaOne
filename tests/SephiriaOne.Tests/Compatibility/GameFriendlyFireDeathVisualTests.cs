using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireDeathVisualTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = addon.GetType("SephiriaOne.FriendlyFireDeathVisuals", true)!;
        var native = game.GetType("DungeonManager", true)!;
        var sender = AccessTools.DeclaredMethod(native, "RpcBulletDestroyed");
        var receiver = AccessTools.DeclaredMethod(native, "InvokeUserCode_RpcBulletDestroyed__UInt32__Boolean__String__Vector3__Single__Single__Single");
        var body = AccessTools.DeclaredMethod(native, "UserCode_RpcBulletDestroyed__UInt32__Boolean__String__Vector3__Single__Single__Single");
        var visual = AccessTools.DeclaredMethod(game.GetType("BulletDestroyModule"), "CreateDestroyVisualOnClient");
        List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        void Require(bool value, string reason) { if (!value) throw new Exception("Death FX contract: " + reason); }
        Require((bool)AccessTools.DeclaredMethod(hooks, "Validate").Invoke(null, null)!,
            "native cosmetic path\n" + string.Join("\n", Code(body).Concat(Code(visual))));
        foreach (string mutation in new[] { "name", "hash", "channel", "owner", "payload", "reader", "gameplay", "write" })
        {
            var send = Code(sender); var read = Code(receiver); var receive = Code(body); var fx = Code(visual);
            int dispatch = send.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            switch (mutation)
            {
                case "name": send.First(i => i.opcode == OpCodes.Ldstr).operand = "changed"; break;
                case "hash": send.First(i => i.LoadsConstant(-1283831235)).operand = 1; break;
                case "channel": send[dispatch - 2].opcode = OpCodes.Ldc_I4_1; break;
                case "owner": send[dispatch - 1].opcode = OpCodes.Ldc_I4_0; break;
                case "payload": send.RemoveAt(send.FindIndex(i => i.operand is MethodInfo m && m.Name == "WriteString")); break;
                case "reader": read.RemoveAt(read.FindIndex(i => i.operand is MethodInfo m && m.Name == "ReadString")); break;
                case "gameplay": fx.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(game.GetType("UnitAvatar"), "ForceDie"))); break;
                case "write": fx.Add(new CodeInstruction(OpCodes.Stsfld)); break;
            }
            Require(!(bool)AccessTools.DeclaredMethod(hooks, "ValidateCode").Invoke(null, new object[] { send, read, receive, fx })!, "rejects changed " + mutation);
        }
        Require(Code(native.TypeInitializer!).Any(i => Equals(i.operand, receiver)), "receiver registered on stock guests");
        var show = Code(AccessTools.DeclaredMethod(hooks, "Show"));
        int broadcast = show.FindIndex(i => i.operand is MethodInfo m && m.Name == "BroadcastDestroyBullet");
        Require(broadcast >= 9 && show[broadcast - 6].LoadsConstant(0) &&
            show[broadcast - 5].LoadsConstant("PlayerDefaultBigBullet") &&
            show[broadcast - 2].opcode == OpCodes.Ldc_R4 && Equals(show[broadcast - 2].operand, 0f) &&
            show[broadcast - 1].opcode == OpCodes.Ldc_R4 && Equals(show[broadcast - 1].operand, 4f),
            "red burst uses fixed native identity, no transparency, zero angle and four-times visual scale");
        Require(show.Any(i => i.operand is MethodInfo m && m.Name == "BroadcastDestroyBullet") &&
            !show.Any(i => i.operand is MethodInfo m && new[] { "RpcDie", "ApplyDamage", "Spawn", "RpcCreateCastFx", "DestroySelf" }.Contains(m.Name)),
            "emits only visual notification without creating/destroying a projectile or replaying death");
        var wrapper = Code(AccessTools.DeclaredMethod(native, "BroadcastDestroyBullet"));
        Require(wrapper.Where(i => i.operand is MethodInfo).Select(i => ((MethodInfo)i.operand).Name)
            .SequenceEqual(new[] { "get_active", "LogWarning", "RpcBulletDestroyed" }), "public wrapper only sends the visual RPC");
        Console.WriteLine("Verified stock red impact FX payload, cosmetic receiver, no fake projectile/death, and fail-closed mutations (not live multiplayer).");
    }
}
