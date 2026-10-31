using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    internal static class FriendlyFireArtifactRangeHooks
    {
        internal static void Install(Harmony harmony)
        {
            var guillotine = AccessTools.DeclaredMethod(typeof(Charm_Guillotine), "RpcCreateGuillotine",
                new[] { typeof(UnitAvatar), typeof(Vector3), typeof(float), typeof(bool) });
            var earring = FriendlyFireEffectHooks.Iterator(typeof(Charm_ElectricEarring), "AttackCoroutine");
            if (guillotine == null || guillotine.IsStatic || guillotine.ReturnType != typeof(void) || earring == null ||
                !ValidateGuillotineSender(PatchProcessor.GetOriginalInstructions(guillotine)))
                throw new InvalidOperationException("Native autonomous artifact emission contract changed.");
            RewriteEarringTargets(PatchProcessor.GetOriginalInstructions(earring), earring).ToList();
            harmony.Patch(guillotine, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeGuillotineEmission)));
            harmony.Patch(earring, transpiler: new HarmonyMethod(typeof(FriendlyFireArtifactRangeHooks), nameof(RewriteEarringTargets)));
        }

        internal static bool ValidateGuillotineSender(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            return code.Count(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal") == 1 &&
                code.Count(i => i.operand is MethodInfo m && m.Name == "Get" && m.DeclaringType.Name == "NetworkWriterPool") == 1;
        }

        internal static IEnumerable<CodeInstruction> RewriteEarringTargets(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var remove = AccessTools.Method(typeof(List<UnitAvatar>), "RemoveAll", new[] { typeof(Predicate<UnitAvatar>) });
            var owner = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
            var getter = AccessTools.PropertyGetter(typeof(Charm_Basic), "NetworkAvatar");
            var calls = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(remove)).ToArray();
            if (owner == null || owner.FieldType != typeof(Charm_ElectricEarring) || getter == null || calls.Length != 1 ||
                code[calls[0]].blocks.Count != 0)
                throw new InvalidOperationException("Native Earring cached-target pruning changed.");
            int index = calls[0];
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(code[index].labels); code[index].labels.Clear();
            code[index].opcode = OpCodes.Call;
            code[index].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.PruneAutonomousTargets));
            code.InsertRange(index, new[] { load, new CodeInstruction(OpCodes.Ldfld, owner), new CodeInstruction(OpCodes.Call, getter) });
            return code;
        }
    }
}
