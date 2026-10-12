using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameNativePlayerChatTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type adapter = addon.GetType("SephiriaOne.NativePlayerChat", true)!;
        Type dungeon = game.GetType("DungeonManager", true)!;
        Type behaviour = dungeon.BaseType!;
        List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        void Require(bool value, string reason) { if (!value) throw new Exception("Private chat contract: " + reason); }
        var sender = AccessTools.DeclaredMethod(dungeon, "RpcChat");
        var receiver = AccessTools.DeclaredMethod(dungeon, "InvokeUserCode_RpcChat__PlayerAvatar__String__String");
        var body = AccessTools.DeclaredMethod(dungeon, "UserCode_RpcChat__PlayerAvatar__String__String");
        Require((bool)AccessTools.DeclaredMethod(adapter, "ValidateTransport").Invoke(null, null)!,
            "native cosmetic chat path\n" + string.Join("\n", Code(sender).Concat(Code(receiver)).Concat(Code(body))));
        var name = AccessTools.DeclaredMethod(adapter, "ReadableName");
        Require(name != null, "private names are validated with native chat normalization");
        foreach (string missing in new[] { "", " ", "?", "<color=red></color>", "\u200B\u200D", "\uE000", "\uD800" })
            Require(name!.Invoke(null, new object[] { missing }) == null, "unusable nickname omits notice: " + missing);
        Require(Equals(name!.Invoke(null, new object[] { "<color=red>PlayerX</color>\u200B" }), "PlayerX"),
            "native normalization preserves the visible nickname only");
        var normalize = AccessTools.DeclaredMethod(dungeon, "NormalizeChatMessage");
        string sample = "Killed by ‘PlayerX’ (Matchup: Me 3–4 Opponent)";
        Require(Equals(normalize.Invoke(null, new object[] { sample }), sample), "stock guest preserves the exact English message");
        sample = "‘PlayerX’에게 처치됨 (전적: 나 3-4 상대)";
        Require(Equals(normalize.Invoke(null, new object[] { sample }), sample), "stock guest preserves the exact Korean message");
        foreach (string mutation in new[] { "name", "hash", "channel", "owner", "payload", "reader", "gameplay", "write" })
        {
            var send = Code(sender); var read = Code(receiver); var receive = Code(body);
            int dispatch = send.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            switch (mutation)
            {
                case "name": send.First(i => i.LoadsConstant("System.Void DungeonManager::RpcChat(PlayerAvatar,System.String,System.String)")).operand = "changed"; break;
                case "hash": send.First(i => i.LoadsConstant(1586055649)).operand = 1; break;
                case "channel": send[dispatch - 2].opcode = OpCodes.Ldc_I4_1; break;
                case "owner": send[dispatch - 1].opcode = OpCodes.Ldc_I4_0; break;
                case "payload": send.RemoveAt(send.FindIndex(i => i.operand is MethodInfo m && m.Name == "WriteString")); break;
                case "reader": read.RemoveAt(read.FindIndex(i => i.operand is MethodInfo m && m.Name == "ReadString")); break;
                case "gameplay": receive.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(game.GetType("UnitAvatar"), "ForceDie"))); break;
                case "write": receive.Add(new CodeInstruction(OpCodes.Stsfld)); break;
            }
            Require(!(bool)AccessTools.DeclaredMethod(adapter, "ValidateCode").Invoke(null, new object[] { send, read, receive })!,
                "changed " + mutation + " fails closed");
        }
        Require(Code(dungeon.TypeInitializer!).Any(i => Equals(i.operand, receiver)), "stock clients register the chat receiver");
        var show = Code(AccessTools.DeclaredMethod(adapter, "Show"));
        int avatarWrite = show.FindIndex(i => i.operand is MethodInfo m && m.Name == "WriteNetworkBehaviour");
        Require(avatarWrite > 0 && show[avatarWrite - 1].opcode == OpCodes.Ldnull,
            "null avatar prevents any replacement of the respawn bubble");
        Require(show.Where(i => i.operand is MethodInfo m && m.Name.StartsWith("Write", StringComparison.Ordinal))
            .Select(i => ((MethodInfo)i.operand).Name).SequenceEqual(new[] { "WriteNetworkBehaviour", "WriteString", "WriteString" }),
            "adapter matches native payload order exactly");
        Require(!show.Any(i => i.operand is MethodInfo m && new[] { "Chat", "RpcChat", "CmdChat", "SendRPCInternal", "CreateChatBubble" }.Contains(m.Name)),
            "adapter never broadcasts or sends commands/bubbles");
        var target = Code(AccessTools.Method(behaviour, "SendTargetRPCInternal"));
        Require(target.Count(i => i.operand is MethodInfo m && m.Name == "Send") == 1 &&
            !target.Any(i => i.operand is MethodInfo m && m.Name.Contains("SendTo")), "Mirror targets one connection without observer fanout");
        Type local = behaviour.Assembly.GetType("Mirror.LocalConnectionToClient", true)!;
        Require(Code(AccessTools.DeclaredMethod(local, "Send")).Any(i => i.operand is MethodInfo m && m.Name == "Enqueue"),
            "host local connection also queues a client message");
        Console.WriteLine("Verified private stock chat payload, targeted/local delivery, null-avatar bubble isolation and fail-closed mutations (not live multiplayer).");
    }
}
