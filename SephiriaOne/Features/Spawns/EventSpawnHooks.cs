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
    internal static class EventSpawnHooks
    {
        private const string Id = "SephiriaOne.EventSpawns";
        private static bool warned;
        internal static void Install()
        {
            var methods = new[] { typeof(StageEntity_Choice), typeof(StageEntity_GrasslandTown) }
                .Select(t => AccessTools.DeclaredMethod(t, "GenerateStage")).ToArray();
            if (methods.Any(m => m == null || m.ReturnType != typeof(FloorData[]) || !Validate(PatchProcessor.GetOriginalInstructions(m))))
                throw new InvalidOperationException("Native random event room generation contract changed.");
            var harmony = new Harmony(Id);
            try { foreach (var method in methods) harmony.Patch(method, transpiler: new HarmonyMethod(typeof(EventSpawnHooks), nameof(Rewrite))); }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() { new Harmony(Id).UnpatchAll(Id); warned = false; }
        internal static bool Validate(IEnumerable<CodeInstruction> instructions)
        {
            try { Rewrite(instructions).ToList(); return true; }
            catch (InvalidOperationException) { return false; }
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            int a = code.FindIndex(i => i.opcode == OpCodes.Ldc_R8 && Equals(i.operand, .003d));
            int b = code.FindIndex(i => i.opcode == OpCodes.Ldc_R8 && Equals(i.operand, .043d));
            bool Bound(int at, int count) => at >= 1 && at + 3 < code.Count &&
                code[at - 1].IsLdloc() && (code[at + 1].opcode == OpCodes.Bgt_Un || code[at + 1].opcode == OpCodes.Bgt_Un_S) &&
                code[at + 2].LoadsConstant(count) && code[at + 3].IsStloc();
            bool Targets(int branch, int destination) => destination < code.Count &&
                code[branch].operand is Label label && code[destination].labels.Contains(label);
            if (!Bound(a, 2) || !Bound(b, 1) || b != a + 6 ||
                !Targets(a + 1, b - 1) || !Targets(b + 1, b + 4) ||
                (code[a + 4].opcode != OpCodes.Br && code[a + 4].opcode != OpCodes.Br_S) || !Targets(a + 4, b + 4) ||
                code.Count(i => i.opcode == OpCodes.Ldc_R8 && Equals(i.operand, .003d)) != 1 ||
                code.Count(i => i.opcode == OpCodes.Ldc_R8 && Equals(i.operand, .043d)) != 1 ||
                Local(code[a - 1]) != Local(code[b - 1]) || Local(code[a + 3]) != Local(code[b + 3]) ||
                a < 4 || !code[a - 2].IsStloc() || Local(code[a - 2]) != Local(code[a - 1]) ||
                !(code[a - 3].operand is MethodInfo draw) || draw.DeclaringType != typeof(System.Random) || draw.Name != "NextDouble" ||
                !(code[a - 4].operand is ConstructorInfo ctor) || ctor.DeclaringType != typeof(System.Random))
                throw new InvalidOperationException("Native event room thresholds/count/RNG IL changed.");
            int countLocal = Local(code[a + 3]);
            int initial = code.FindLastIndex(a - 1, i => i.IsStloc() && Local(i) == countLocal);
            int consumer = code.FindIndex(b + 4, i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f &&
                f.DeclaringType == typeof(FloorData) && f.Name == "randomRoomCount");
            if (initial < 1 || !code[initial - 1].LoadsConstant(0) || consumer < 1 ||
                !code[consumer - 1].IsLdloc() || Local(code[consumer - 1]) != countLocal ||
                code.Skip(initial + 1).Take(consumer - initial - 1).Count(i => i.IsStloc() && Local(i) == countLocal) != 2)
                throw new InvalidOperationException("Native event room zero/count write contract changed.");
            var adjust = AccessTools.Method(typeof(EventSpawnHooks), nameof(Threshold));
            // The native draw and count assignment remain intact. FloorData saves
            // and synchronizes the result, including to unmodified late joiners.
            code.Insert(b + 1, new CodeInstruction(OpCodes.Call, adjust));
            code.Insert(a + 1, new CodeInstruction(OpCodes.Call, adjust));
            return code;
        }
        private static int Local(CodeInstruction i)
        {
            if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
            if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
            if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
            if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;
            return i.operand is LocalBuilder local ? local.LocalIndex : Convert.ToInt32(i.operand);
        }
        internal static double Threshold(double native)
        {
            if (!NetworkServer.active) return native;
            try
            {
                var settings = SessionSettings.EventSpawnsForGeneration;
                if (!settings.HasChanges) return native;
                double probability = settings.Probability(native);
                return probability == 0 ? -1 : probability;
            }
            catch (Exception error)
            {
                if (!warned) { warned = true; Debug.LogWarning("[SephiriaOne] Random events use native odds after hook failure: " + error); }
                return native;
            }
        }
    }
}
