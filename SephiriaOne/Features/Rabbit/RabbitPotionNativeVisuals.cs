using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class RabbitPotionNativeHooks
    {
        private const string HealRpcName = "System.Void UnitAvatar::RpcBloodFestivalHealFx()";
        private const int HealRpcHash = 184881409;
        private static bool healVisualChecked, healVisualWarned;
        private static Action<UnitAvatar> sendHealVisual;

        private static void ResetHealVisuals()
        {
            healVisualChecked = healVisualWarned = false;
            sendHealVisual = null;
        }

        private static void ShowSharedHealVisual(PlayerAvatar recipient)
        {
            try
            {
                if (!healVisualChecked)
                {
                    healVisualChecked = true;
                    if (!ValidateHealVisual()) throw new InvalidOperationException("Native green heal FX contract changed.");
                    // Resolve only after validation. Missing future game methods
                    // must not prevent the HP-only sharing path from compiling.
                    sendHealVisual = (Action<UnitAvatar>)Delegate.CreateDelegate(typeof(Action<UnitAvatar>),
                        AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcBloodFestivalHealFx", Type.EmptyTypes));
                }
                sendHealVisual?.Invoke(recipient);
            }
            catch (Exception error)
            {
                // Presentation failure never retries HP, interrupts subsequent
                // recipients, or disables MP/infinite/Survival protection hooks.
                sendHealVisual = null;
                if (healVisualWarned) return;
                healVisualWarned = true;
                Debug.LogWarning("[SephiriaOne] Shared-heal particles unavailable; healing remains active: " + error);
            }
        }

        internal static bool ValidateHealVisual()
        {
            var send = AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcBloodFestivalHealFx", Type.EmptyTypes);
            var receive = AccessTools.DeclaredMethod(typeof(UnitAvatar), "InvokeUserCode_RpcBloodFestivalHealFx");
            var body = AccessTools.DeclaredMethod(typeof(UnitAvatar), "UserCode_RpcBloodFestivalHealFx", Type.EmptyTypes);
            return send != null && send.IsPublic && !send.IsStatic && send.ReturnType == typeof(void) &&
                receive != null && receive.IsStatic && receive.ReturnType == typeof(void) &&
                body != null && body.ReturnType == typeof(void) &&
                ValidateHealVisualCode(PatchProcessor.GetOriginalInstructions(send),
                    PatchProcessor.GetOriginalInstructions(receive), PatchProcessor.GetOriginalInstructions(body));
        }

        internal static bool ValidateHealVisualCode(IEnumerable<CodeInstruction> send,
            IEnumerable<CodeInstruction> receive, IEnumerable<CodeInstruction> body)
        {
            var code = send.Where(i => i.opcode != OpCodes.Nop).ToList();
            var sendCalls = Calls(code);
            int dispatch = code.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            if (!sendCalls.Select(m => m.Name).SequenceEqual(new[] { "Get", "SendRPCInternal", "Return" }) ||
                sendCalls[0].DeclaringType != typeof(Mirror.NetworkWriterPool) ||
                sendCalls[1].DeclaringType != typeof(Mirror.NetworkBehaviour) ||
                sendCalls[2].DeclaringType != typeof(Mirror.NetworkWriterPool) ||
                code.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, HealRpcName)) != 1 ||
                code.Count(i => i.opcode == OpCodes.Ldc_I4 && Equals(i.operand, HealRpcHash)) != 1 ||
                dispatch < 2 || !LoadsInt(code[dispatch - 2], 0) || !LoadsInt(code[dispatch - 1], 1))
                return false;
            var receiveCalls = Calls(receive);
            if (receiveCalls.Count(m => m.DeclaringType == typeof(UnitAvatar) && m.Name == "UserCode_RpcBloodFestivalHealFx") != 1 ||
                receiveCalls.Any(m => !(m.DeclaringType == typeof(UnitAvatar) && m.Name == "UserCode_RpcBloodFestivalHealFx") &&
                    !(m.DeclaringType == typeof(Mirror.NetworkClient) && m.Name == "get_active") &&
                    !(m.DeclaringType == typeof(Debug) && m.Name == "LogError"))) return false;
            var visual = body.ToList();
            var visualCalls = Calls(visual);
            return visual.Any(i => i.operand is FieldInfo f && f.DeclaringType == typeof(CombatManager) && f.Name == "bloodFestivalHealFxPrefab") &&
                visualCalls.Count(m => m.Name == "Spawn" && m.ReturnType == typeof(SpriteFx)) == 1 &&
                !visual.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli || i.opcode == OpCodes.Newobj) &&
                visualCalls.All(m =>
                    m.Name == "get_Instance" && m.ReturnType == typeof(CombatManager) ||
                    m.DeclaringType == typeof(UnityEngine.Object) && (m.Name == "op_Equality" || m.Name == "op_Inequality") ||
                    m.DeclaringType == typeof(SpriteFx) && m.Name == "get_Pool" ||
                    m.Name == "get_transform" && m.ReturnType == typeof(Transform) ||
                    m.DeclaringType == typeof(Transform) && m.Name == "get_position" ||
                    m.Name == "Spawn" && m.ReturnType == typeof(SpriteFx) &&
                        m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(GameObject), typeof(Vector3), typeof(GameObject) }));
        }

        private static bool LoadsInt(CodeInstruction instruction, int value) =>
            instruction.opcode == OpCodes.Ldc_I4 && Equals(instruction.operand, value) ||
            instruction.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(instruction.operand) == value ||
            value == 0 && instruction.opcode == OpCodes.Ldc_I4_0 ||
            value == 1 && instruction.opcode == OpCodes.Ldc_I4_1;

        private static List<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code
            .Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
            .Select(i => i.operand).OfType<MethodInfo>().ToList();
    }
}
