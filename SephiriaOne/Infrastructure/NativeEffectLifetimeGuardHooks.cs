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
    internal static class NativeEffectLifetimeGuardHooks
    {
        private const string Owner = "preco21.SephiriaOne.NativeEffectLifetimeGuard";
        private static Harmony harmony;
        internal static bool Available { get; private set; }

        internal static void Install()
        {
            if (harmony != null) return;
            try
            {
                var update = AccessTools.DeclaredMethod(typeof(CharacterBuff_StatusInstance_FrostTouch), "OnUpdate_Server", Type.EmptyTypes);
                if (update == null || update.IsStatic || update.ReturnType != typeof(void))
                    throw new InvalidOperationException("Native FrostTouch server-update signature changed.");
                Rewrite(PatchProcessor.GetOriginalInstructions(update)).ToList();
                harmony = new Harmony(Owner);
                harmony.Patch(update, transpiler: new HarmonyMethod(typeof(NativeEffectLifetimeGuardHooks), nameof(Rewrite)));
                Available = true;
            }
            catch (Exception error)
            {
                Uninstall();
                Debug.LogWarning("[SephiriaOne] Optional FrostTouch lifetime guard unavailable; native effect continues: " + error);
            }
        }

        internal static void Uninstall()
        {
            Available = false;
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        // Run after the unchanged base HUD update, where a callback may already
        // have ended the buff or destroyed its target. Only the derived FX tail
        // is gated: native Update expiry and delayed despawn remain in charge.
        private static bool CanRunFrostTouchFx(CharacterBuff_StatusInstance_FrostTouch effect) =>
            effect && effect.isServer && !effect.IsEndBuff && effect.NetworkTarget;

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var baseUpdate = AccessTools.DeclaredMethod(typeof(CharacterBuff), "OnUpdate_Server", Type.EmptyTypes);
            var server = AccessTools.PropertyGetter(typeof(NetworkBehaviour), "isServer");
            var target = AccessTools.PropertyGetter(typeof(CharacterBuff), "NetworkTarget");
            var transform = AccessTools.PropertyGetter(typeof(Component), "transform");
            var fx = AccessTools.DeclaredMethod(typeof(CharacterBuff_StatusInstance_FrostTouch), "RpcCreateFrostTouchFx");
            var body = code.Where(i => i.opcode != OpCodes.Nop).ToList();
            if (baseUpdate == null || server == null || target == null || transform == null || fx == null || body.Count < 6 ||
                body[0].opcode != OpCodes.Ldarg_0 || body[1].opcode != OpCodes.Call || !body[1].Calls(baseUpdate) ||
                body[2].opcode != OpCodes.Ldarg_0 || !body[3].Calls(server) ||
                (body[4].opcode != OpCodes.Brfalse && body[4].opcode != OpCodes.Brfalse_S) ||
                body.Count(i => i.Calls(baseUpdate)) != 1 || body.Count(i => i.Calls(server)) != 1 ||
                body.Count(i => i.Calls(target)) != 1 || body.Count(i => i.Calls(transform)) != 1 || body.Count(i => i.Calls(fx)) != 1 ||
                body.FindIndex(i => i.Calls(target)) >= body.FindIndex(i => i.Calls(transform)) ||
                body.FindIndex(i => i.Calls(transform)) >= body.FindIndex(i => i.Calls(fx)))
                throw new InvalidOperationException("Native FrostTouch base-update/FX boundary changed.");
            int gate = code.FindIndex(i => i.Calls(server));
            var replacement = CodeInstruction.Call(typeof(NativeEffectLifetimeGuardHooks), nameof(CanRunFrostTouchFx));
            replacement.MoveLabelsFrom(code[gate]); replacement.MoveBlocksFrom(code[gate]);
            code[gate] = replacement;
            return code;
        }
    }
}
