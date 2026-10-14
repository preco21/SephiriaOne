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
        private const string DrinkVisualRpc = "System.Void PotionEffect::RpcCreateDrinkVisual(UnityEngine.Vector3)";
        private const int DrinkVisualHash = -1992013641;
        private static bool healPresentationChecked, healPresentationWarned;
        private static Action<PotionEffect, Vector3> sendHealPresentation;

        private static void ResetHealPresentation()
        { healPresentationChecked = healPresentationWarned = false; sendHealPresentation = null; }

        // True means an audiovisual send was attempted. An ambiguous send failure
        // must not retry or also publish fallback particles for the same event.
        private static bool TryShowSharedHealPresentation(PotionEffect effect, PlayerAvatar recipient)
        {
            if (!effect || !effect.isServer || effect.netId == 0 || effect.GetType() != typeof(PotionEffect_Regeneration)) return false;
            Vector3 position = recipient.transform.position;
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                float.IsNaN(position.z) || float.IsInfinity(position.z)) return false;
            bool attempted = false;
            try
            {
                if (!healPresentationChecked)
                {
                    healPresentationChecked = true;
                    if (!ValidateHealPresentation()) throw new InvalidOperationException("Native potion audiovisual contract changed.");
                    sendHealPresentation = (Action<PotionEffect, Vector3>)Delegate.CreateDelegate(typeof(Action<PotionEffect, Vector3>),
                        AccessTools.DeclaredMethod(typeof(PotionEffect), "RpcCreateDrinkVisual", new[] { typeof(Vector3) }));
                }
                if (sendHealPresentation == null) return false;
                attempted = true;
                sendHealPresentation(effect, position);
                return true;
            }
            catch (Exception error)
            {
                sendHealPresentation = null;
                if (!healPresentationWarned)
                {
                    healPresentationWarned = true;
                    Debug.LogWarning("[SephiriaOne] Shared-heal sound unavailable; native healing remains active: " + error);
                }
                return attempted;
            }
        }

        internal static bool ValidateHealPresentation()
        {
            var sender = AccessTools.DeclaredMethod(typeof(PotionEffect), "RpcCreateDrinkVisual", new[] { typeof(Vector3) });
            var reader = AccessTools.DeclaredMethod(typeof(PotionEffect), "InvokeUserCode_RpcCreateDrinkVisual__Vector3");
            var receiver = AccessTools.DeclaredMethod(typeof(PotionEffect), "UserCode_RpcCreateDrinkVisual__Vector3");
            var drink = AccessTools.DeclaredMethod(typeof(PotionEffect), "CreateDrinkVisual", new[] { typeof(Vector3) });
            var heal = AccessTools.DeclaredMethod(typeof(PotionEffect_Regeneration), "CreateDrinkVisual", new[] { typeof(Vector3) });
            return sender != null && reader != null && receiver != null && drink != null && heal != null &&
                ValidateHealPresentationCode(PatchProcessor.GetOriginalInstructions(sender), PatchProcessor.GetOriginalInstructions(reader),
                    PatchProcessor.GetOriginalInstructions(receiver), PatchProcessor.GetOriginalInstructions(drink), PatchProcessor.GetOriginalInstructions(heal));
        }

        internal static bool ValidateHealPresentationCode(IEnumerable<CodeInstruction> sender, IEnumerable<CodeInstruction> reader,
            IEnumerable<CodeInstruction> receiver, IEnumerable<CodeInstruction> drink, IEnumerable<CodeInstruction> heal)
        {
            var code = sender.Where(i => i.opcode != OpCodes.Nop).ToList();
            var calls = Calls(code);
            int dispatch = code.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            if (!calls.Select(m => m.Name).SequenceEqual(new[] { "Get", "WriteVector3", "SendRPCInternal", "Return" }) ||
                calls[0].DeclaringType != typeof(Mirror.NetworkWriterPool) || calls[1].DeclaringType != typeof(Mirror.NetworkWriterExtensions) ||
                calls[2].DeclaringType != typeof(Mirror.NetworkBehaviour) || calls[3].DeclaringType != typeof(Mirror.NetworkWriterPool) ||
                code.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, DrinkVisualRpc)) != 1 ||
                code.Count(i => i.LoadsConstant(DrinkVisualHash)) != 1 || dispatch < 2 || !LoadsInt(code[dispatch - 2], 0) || !LoadsInt(code[dispatch - 1], 1)) return false;
            var receiveCalls = Calls(reader);
            if (receiveCalls.Count(m => m.Name == "ReadVector3" && m.DeclaringType == typeof(Mirror.NetworkReaderExtensions)) != 1 ||
                receiveCalls.Count(m => m.Name == "UserCode_RpcCreateDrinkVisual__Vector3" && m.DeclaringType == typeof(PotionEffect)) != 1 ||
                receiveCalls.Any(m => !(m.DeclaringType == typeof(Mirror.NetworkReaderExtensions) && m.Name == "ReadVector3") &&
                    !(m.DeclaringType == typeof(PotionEffect) && m.Name == "UserCode_RpcCreateDrinkVisual__Vector3") &&
                    !(m.DeclaringType == typeof(Mirror.NetworkClient) && m.Name == "get_active") &&
                    !(m.DeclaringType == typeof(Debug) && m.Name == "LogError"))) return false;
            var bodyCalls = Calls(receiver);
            if (bodyCalls.Count != 1 || bodyCalls[0].DeclaringType != typeof(PotionEffect) || bodyCalls[0].Name != "CreateDrinkVisual") return false;
            bool Visual(IEnumerable<CodeInstruction> body, bool healing)
            {
                var list = body.ToList();
                string[] expected = healing ? new[] { "CreateDrinkVisual", "CreateInstance", "To3DAttributes", "set3DAttributes", "start", "release", "get_Pool", "Spawn" } :
                    new[] { "CreateInstance", "To3DAttributes", "set3DAttributes", "start", "release" };
                return Calls(list).Select(m => m.Name).SequenceEqual(expected) && Calls(list).All(m =>
                    m.DeclaringType == typeof(PotionEffect) && m.Name == "CreateDrinkVisual" ||
                    m.DeclaringType?.FullName == "FMODUnity.RuntimeManager" && m.Name == "CreateInstance" ||
                    m.DeclaringType?.FullName == "FMODUnity.RuntimeUtils" && m.Name == "To3DAttributes" ||
                    m.DeclaringType?.FullName == "FMOD.Studio.EventInstance" && (m.Name == "set3DAttributes" || m.Name == "start" || m.Name == "release") ||
                    m.DeclaringType == typeof(SpriteFx) && m.Name == "get_Pool" ||
                    m.Name == "Spawn" && m.ReturnType == typeof(SpriteFx) && m.GetParameters().Select(p => p.ParameterType)
                        .SequenceEqual(new[] { typeof(GameObject), typeof(Vector3), typeof(GameObject) })) &&
                    list.Any(i => i.operand is FieldInfo f && f.DeclaringType == (healing ? typeof(PotionEffect_Regeneration) : typeof(PotionEffect)) &&
                        f.Name == (healing ? "regenerationSoundEvent" : "drinkSoundEvent")) &&
                    (!healing || list.Any(i => i.operand is FieldInfo f && f.DeclaringType == typeof(PotionEffect_Regeneration) && f.Name == "regenerationFxPrefab")) &&
                    !list.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli);
            }
            return Visual(drink, false) && Visual(heal, true);
        }
    }
}
