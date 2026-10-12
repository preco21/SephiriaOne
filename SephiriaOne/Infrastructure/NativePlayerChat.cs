using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // A targeted invocation of the stock chat receiver. Unlike Dungeon.Chat,
    // this never broadcasts, calls a command, or replaces an avatar's bubble.
    internal static class NativePlayerChat
    {
        private const string RpcName = "System.Void DungeonManager::RpcChat(PlayerAvatar,System.String,System.String)";
        private const int RpcHash = 1586055649;
        private static bool checkedTransport, warned;
        private static MethodInfo targetSend;

        internal static void Reset() { checkedTransport = warned = false; targetSend = null; }

        internal static string ReadableName(string name)
        {
            // Use the stock receiver's Unicode/tag rules before deciding a
            // nickname exists; format-only names otherwise become empty later.
            string plain = ChatTextSanitizer.Sanitize(name, 32);
            return string.IsNullOrWhiteSpace(plain) || plain == "?" ? null : plain;
        }

        internal static void Show(PlayerAvatar player, string message)
        {
            try
            {
                var dungeon = DungeonManager.Instance;
                if (!NetworkServer.active || !dungeon || !dungeon.isServer || dungeon.netId == 0 ||
                    !player || !player.isServer || !player.spawner ||
                    !ReferenceEquals(player.spawner.PlayerAvatar, player) || string.IsNullOrWhiteSpace(message)) return;
                var connection = player.spawner.connectionToClient;
                if (connection == null || !connection.isReady || player.netId == 0 ||
                    !ReferenceEquals(player.connectionToClient, connection) ||
                    !ReferenceEquals(connection.identity, player.netIdentity) ||
                    !NetworkServer.connections.TryGetValue(connection.connectionId, out var live) ||
                    !ReferenceEquals(live, connection)) return;
                if (!checkedTransport)
                {
                    checkedTransport = true;
                    if (!ValidateTransport()) throw new InvalidOperationException("Native private chat contract changed.");
                    targetSend = AccessTools.DeclaredMethod(typeof(NetworkBehaviour), "SendTargetRPCInternal",
                        new[] { typeof(NetworkConnection), typeof(string), typeof(int), typeof(NetworkWriter), typeof(int) });
                    if (targetSend == null) throw new InvalidOperationException("Native targeted RPC sender unavailable.");
                }
                if (targetSend == null) return;
                var writer = NetworkWriterPool.Get();
                try
                {
                    // Preserve the native payload exactly. Null avatar leaves
                    // countdown bubbles untouched. Mirror also delivers this
                    // targeted message to the host's local client.
                    writer.WriteNetworkBehaviour(null);
                    writer.WriteString("SephiriaOne");
                    writer.WriteString(message);
                    targetSend.Invoke(dungeon, new object[] { connection, RpcName, RpcHash, writer, 0 });
                }
                finally { NetworkWriterPool.Return(writer); }
            }
            catch (Exception error)
            {
                if (warned) return;
                warned = true;
                Debug.LogWarning("[SephiriaOne] Private chat notice unavailable; combat accounting remains active: " + error);
            }
        }

        internal static bool ValidateTransport()
        {
            var send = AccessTools.DeclaredMethod(typeof(DungeonManager), "RpcChat");
            var receive = AccessTools.DeclaredMethod(typeof(DungeonManager), "InvokeUserCode_RpcChat__PlayerAvatar__String__String");
            var body = AccessTools.DeclaredMethod(typeof(DungeonManager), "UserCode_RpcChat__PlayerAvatar__String__String");
            return send != null && receive != null && body != null &&
                ValidateCode(PatchProcessor.GetOriginalInstructions(send), PatchProcessor.GetOriginalInstructions(receive),
                    PatchProcessor.GetOriginalInstructions(body));
        }

        internal static bool ValidateCode(IEnumerable<CodeInstruction> send, IEnumerable<CodeInstruction> receive,
            IEnumerable<CodeInstruction> body)
        {
            var sender = send.Where(i => i.opcode != OpCodes.Nop).ToList();
            int dispatch = sender.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            return sender.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, RpcName)) == 1 && sender.Count(i => i.LoadsConstant(RpcHash)) == 1 &&
                dispatch >= 2 && sender[dispatch - 2].LoadsConstant(0) && sender[dispatch - 1].LoadsConstant(1) &&
                Calls(sender).Select(m => m.Name).SequenceEqual(new[] { "Get", "WriteNetworkBehaviour", "WriteString", "WriteString", "SendRPCInternal", "Return" }) &&
                Calls(receive).Select(m => m.Name).SequenceEqual(new[] { "get_active", "LogError", "ReadNetworkBehaviour", "ReadString", "ReadString", "UserCode_RpcChat__PlayerAvatar__String__String" }) &&
                !body.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli || i.opcode == OpCodes.Newobj) &&
                Calls(body).Select(m => m.Name).SequenceEqual(new[] { "NormalizeChatName", "NormalizeChatMessage", "IsNullOrWhiteSpace", "Concat", "Log", "get_Instance", "get_cyan", "WriteLog", "op_Implicit", "CreateChatBubble" });
        }

        private static IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code
            .Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt).Select(i => i.operand).OfType<MethodInfo>();
    }
}
