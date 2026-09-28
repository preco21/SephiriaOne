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
    // Presentation only: no client addon, persistent player state, callbacks into gameplay, or retries.
    internal static class NativePlayerAlert
    {
        private const string RpcName = "System.Void UnitAvatar::RpcShowDamageParticle(UnityEngine.Vector2,System.String,UnityEngine.Color,System.Int32,System.Boolean,UnitAvatar,UnitAvatar)";
        private const int RpcHash = -1687250273;
        private const string ReceiveName = "InvokeUserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar";
        private static bool transportChecked, warned;
        private static MethodInfo targetSend;

        public static void Show(PlayerAvatar player, NetworkConnectionToClient connection, string message)
        {
            try
            {
                if (!NetworkServer.active || !player || !player.isServer || player.netId == 0 ||
                    !player.spawner || !ReferenceEquals(player.spawner.PlayerAvatar, player) ||
                    !ReferenceEquals(player.spawner.connectionToClient, connection) || connection == null ||
                    !connection.isReady || !NetworkServer.connections.Values.Contains(connection)) return;
                // Mirror's local player is the spawner, not necessarily the controlled avatar.
                if (connection is LocalConnectionToClient)
                {
                    player.WriteSystemMessage(message, 2.5f, false);
                    return;
                }
                if (!transportChecked)
                {
                    transportChecked = true;
                    if (!ValidateTransport()) throw new InvalidOperationException("Native floating-text RPC contract changed.");
                    targetSend = AccessTools.DeclaredMethod(typeof(NetworkBehaviour), "SendTargetRPCInternal",
                        new[] { typeof(NetworkConnection), typeof(string), typeof(int), typeof(NetworkWriter), typeof(int) });
                    if (targetSend == null) throw new InvalidOperationException("Native targeted RPC sender unavailable.");
                }
                if (targetSend == null) return;
                Vector2 position = player.transform.position;
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y)) return;
                var writer = NetworkWriterPool.Get();
                try
                {
                    // Exactly the game's serializer, checked below. Targeted Mirror RPCs use the same
                    // registered receiver as broadcasts. Reliable channel; only this owner sees the text.
                    writer.WriteVector2(position);
                    writer.WriteString(message);
                    writer.WriteColor(new Color(1f, .75f, .2f, 1f));
                    writer.WriteVarInt(0);
                    writer.WriteBool(false);
                    writer.WriteNetworkBehaviour(player);
                    writer.WriteNetworkBehaviour(player);
                    targetSend.Invoke(player, new object[] { connection, RpcName, RpcHash, writer, 0 });
                }
                finally { NetworkWriterPool.Return(writer); }
            }
            catch (Exception error)
            {
                if (warned) return;
                warned = true;
                Debug.LogWarning("[SephiriaOne] Player alert unavailable; potion protections remain active: " + error);
            }
        }

        // This optional transport fails closed independently of potion mechanics after game updates.
        internal static bool ValidateTransport()
        {
            var send = AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcShowDamageParticle",
                new[] { typeof(Vector2), typeof(string), typeof(Color), typeof(int), typeof(bool), typeof(UnitAvatar), typeof(UnitAvatar) });
            var receive = AccessTools.DeclaredMethod(typeof(UnitAvatar), ReceiveName);
            return send != null && receive != null && send.ReturnType == typeof(void) &&
                ValidateCode(PatchProcessor.GetOriginalInstructions(send), PatchProcessor.GetOriginalInstructions(receive));
        }

        internal static bool ValidateCode(IEnumerable<CodeInstruction> send, IEnumerable<CodeInstruction> receive)
        {
            var code = send.ToList();
            string[] writes = { "WriteVector2", "WriteString", "WriteColor", "WriteVarInt", "WriteBool", "WriteNetworkBehaviour", "WriteNetworkBehaviour" };
            string[] reads = { "ReadVector2", "ReadString", "ReadColor", "ReadVarInt", "ReadBool", "ReadNetworkBehaviour", "ReadNetworkBehaviour" };
            return code.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, RpcName)) == 1 &&
                code.Count(i => i.opcode == OpCodes.Ldc_I4 && Equals(i.operand, RpcHash)) == 1 &&
                code.Count(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal") == 1 &&
                code.Where(i => i.operand is MethodInfo m && m.Name.StartsWith("Write", StringComparison.Ordinal))
                    .Select(i => ((MethodInfo)i.operand).Name).SequenceEqual(writes) &&
                receive.Where(i => i.operand is MethodInfo m && m.Name.StartsWith("Read", StringComparison.Ordinal))
                    .Select(i => ((MethodInfo)i.operand).Name).SequenceEqual(reads);
        }
    }
}
