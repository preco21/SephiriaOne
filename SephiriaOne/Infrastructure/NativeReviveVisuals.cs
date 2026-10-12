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
    // Native pillar and its brief revive flash/sound. Never invokes revival,
    // additional-life accounting or the player-to-player revival callbacks.
    internal static class NativeReviveVisuals
    {
        private const string RpcName = "System.Void UnitAvatar::RpcCreateAdditionalLifeFx()";
        private const int RpcHash = -2080231604;
        private static bool checkedTransport, warned;
        private static Action<UnitAvatar> send;

        internal static void Reset() { checkedTransport = warned = false; send = null; }

        internal static void Show(PlayerAvatar player)
        {
            try
            {
                if (!NetworkServer.active || !player || !player.isServer || player.netId == 0 || player.IsDead ||
                    player.hp <= 0 || !Finite(player.hp) || SpriteFx.Pool == null) return;
                Vector3 position = player.transform.position;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return;
                if (!checkedTransport)
                {
                    checkedTransport = true;
                    if (!Validate()) throw new InvalidOperationException("Native revive visual contract changed.");
                    send = (Action<UnitAvatar>)Delegate.CreateDelegate(typeof(Action<UnitAvatar>),
                        AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcCreateAdditionalLifeFx", Type.EmptyTypes));
                }
                // The receiver reads this avatar's world transform, just as it
                // does for a native extra life, on the host and stock observers.
                send?.Invoke(player);
            }
            catch (Exception error)
            {
                send = null;
                if (warned) return;
                warned = true;
                Debug.LogWarning("[SephiriaOne] Respawn visual unavailable; native recovery remains active: " + error);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool Validate()
        {
            var sender = AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcCreateAdditionalLifeFx", Type.EmptyTypes);
            var receive = AccessTools.DeclaredMethod(typeof(UnitAvatar), "InvokeUserCode_RpcCreateAdditionalLifeFx");
            var body = AccessTools.DeclaredMethod(typeof(UnitAvatar), "UserCode_RpcCreateAdditionalLifeFx", Type.EmptyTypes);
            return sender != null && sender.IsPublic && !sender.IsStatic && sender.ReturnType == typeof(void) &&
                receive != null && body != null && ValidateCode(PatchProcessor.GetOriginalInstructions(sender),
                    PatchProcessor.GetOriginalInstructions(receive), PatchProcessor.GetOriginalInstructions(body));
        }

        internal static bool ValidateCode(IEnumerable<CodeInstruction> sendCode, IEnumerable<CodeInstruction> receive,
            IEnumerable<CodeInstruction> body)
        {
            var sender = sendCode.Where(i => i.opcode != OpCodes.Nop).ToList();
            int dispatch = sender.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            var visual = body.Where(i => i.opcode != OpCodes.Nop).ToList();
            int transform = visual.FindIndex(i => i.operand is MethodInfo m && m.Name == "get_transform");
            return sender.Count(i => i.LoadsConstant(RpcName)) == 1 && sender.Count(i => i.LoadsConstant(RpcHash)) == 1 &&
                dispatch >= 2 && sender[dispatch - 2].LoadsConstant(0) && sender[dispatch - 1].LoadsConstant(1) &&
                Calls(sender).Select(m => m.Name).SequenceEqual(new[] { "Get", "SendRPCInternal", "Return" }) &&
                Calls(receive).Select(m => m.Name).SequenceEqual(new[] { "get_active", "LogError", "UserCode_RpcCreateAdditionalLifeFx" }) &&
                !visual.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli || i.opcode == OpCodes.Newobj) &&
                visual.Count(i => i.LoadsConstant("AdditionalLifeReviveFx")) == 1 &&
                visual.Count(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 0.001f)) == 1 &&
                transform > 0 && visual[transform - 1].opcode == OpCodes.Ldarg_0 &&
                Calls(visual).Select(m => m.Name).SequenceEqual(new[] { "get_Pool", "get_transform", "get_position", "get_down",
                    "op_Multiply", "op_Addition", "Spawn", "get_Instance", "GetElement", "Open" }) &&
                Calls(visual).Any(m => m.Name == "GetElement" && m.IsGenericMethod &&
                    m.GetGenericArguments().SequenceEqual(new[] { typeof(UI_ReviveEffect) }));
        }

        private static IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code
            .Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt).Select(i => i.operand).OfType<MethodInfo>();
    }
}
