using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireTargetingHooks
    {
        internal static void Install(Harmony harmony)
        {
            var nearest = AccessTools.DeclaredMethod(typeof(PlayerInputController), "SearchTargetNearestPoint");
            var homing = AccessTools.DeclaredMethod(typeof(Bullet), "Update");
            if (nearest == null || homing == null) throw new InvalidOperationException("Native offensive targeting entry points changed.");
            RewriteNearestMask(PatchProcessor.GetOriginalInstructions(nearest)).ToList();
            RewriteHoming(PatchProcessor.GetOriginalInstructions(homing)).ToList();
            harmony.Patch(nearest, transpiler: new HarmonyMethod(typeof(FriendlyFireTargetingHooks), nameof(RewriteNearestMask)));
            harmony.Patch(homing, transpiler: new HarmonyMethod(typeof(FriendlyFireTargetingHooks), nameof(RewriteHoming)));
        }

        internal static List<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> instructions, MethodInfo original, string helper, int count)
        {
            if (original == null) throw new InvalidOperationException("Native offensive call missing: " + helper);
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var calls = code.Where(i => i.Calls(original)).ToArray();
            if (calls.Length != count) throw new InvalidOperationException("Native offensive call contract changed: " + helper);
            foreach (var call in calls) { call.opcode = OpCodes.Call; call.operand = AccessTools.Method(typeof(FriendlyFireRuntime), helper); }
            return code;
        }

        internal static IEnumerable<CodeInstruction> RewriteHoming(IEnumerable<CodeInstruction> instructions)
        {
            var code = FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.None, nameof(FriendlyFireRuntime.HomingTargetAllowed));
            int filter = code.FindIndex(i => i.Calls(AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.HomingTargetAllowed))));
            code.Insert(filter, new CodeInstruction(OpCodes.Ldarg_0));
            var reads = Enumerable.Range(1, code.Count - 1).Where(i => code[i].LoadsField(AccessTools.Field(typeof(UnitAvatar), "IsDead")) &&
                code[i - 1].Calls(AccessTools.PropertyGetter(typeof(Bullet), "HomingTarget"))).ToArray();
            if (reads.Length != 1) throw new InvalidOperationException("Native projectile homing target lifetime changed.");
            int read = reads[0];
            code[read].opcode = OpCodes.Ldarg_0; code[read].operand = null;
            code.Insert(read + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.HomingTargetLost))));
            return code;
        }

        internal static IEnumerable<CodeInstruction> RewriteNearestMask(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var masks = Enumerable.Range(1, code.Count - 1).Where(i =>
                code[i].Calls(AccessTools.Method(typeof(UnitAvatar), "GetHostileFactionLayers"))).ToArray();
            if (masks.Length != 1) throw new InvalidOperationException("Native nearest-point mask changed.");
            int mask = masks[0];
            // source, None, mask, faction-manager, target, faction, layer, AND
            if (mask + 5 >= code.Count || !code[mask - 1].LoadsConstant((int)EDamageFromType.None) ||
                !code[mask + 1].Calls(AccessTools.PropertyGetter(typeof(RuntimeFactionManager), "Instance")) ||
                !code[mask + 2].IsLdloc() || !code[mask + 3].LoadsField(AccessTools.Field(typeof(UnitAvatar), "faction")) ||
                !code[mask + 4].Calls(AccessTools.Method(typeof(RuntimeFactionManager), "FindFactionLayer")) || code[mask + 5].opcode != OpCodes.And ||
                code.Skip(mask + 1).Take(5).Any(i => i.labels.Count != 0 || i.blocks.Count != 0))
                throw new InvalidOperationException("Native nearest-point source/target pairing changed.");
            foreach (int offset in new[] { 0, 1, 3, 4 }) { code[mask + offset].opcode = OpCodes.Nop; code[mask + offset].operand = null; }
            code[mask + 5].opcode = OpCodes.Call;
            code[mask + 5].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.OffensiveNearestMask));
            return code;
        }
    }
}
