using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class ChoiceTranspilers
    {
        public static IEnumerable<CodeInstruction> Items(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase original, MethodInfo guard)
        {
            var code = instructions.ToList();
            var matches = Enumerable.Range(0, Math.Max(0, code.Count - 3)).Where(i =>
                IsLessThan(code[i]) && code[i + 1].opcode == OpCodes.Ldarg_0 &&
                code[i + 2].LoadsConstant(1) && code[i + 3].operand is MethodInfo method &&
                method.Name == "set_NetworkisGenerated" && method.DeclaringType?.Name == "Sephirite").ToList();
            if (matches.Count != 1) throw new InvalidOperationException("Sephirite reward loop shape changed.");
            int index = matches[0];
            int pool = FindLocal(original, guard.GetParameters()[0].ParameterType);
            var attempts = generator.DeclareLocal(typeof(int));
            Label exit = generator.DefineLabel();
            code[index + 1].labels.Add(exit);
            object body = code[index].operand;
            code[index].opcode = OpCodes.Bge;
            code[index].operand = exit;
            code.InsertRange(index + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldloc, pool),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloca, attempts),
                new CodeInstruction(OpCodes.Call, guard),
                new CodeInstruction(OpCodes.Brtrue, body)
            });
            code.InsertRange(0, new[] { new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Stloc, attempts) });
            return code;
        }

        public static IEnumerable<CodeInstruction> Miracles(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase original, MethodInfo guard)
        {
            var code = instructions.ToList();
            var draws = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo method &&
                method.Name == "GetRandom" && method.DeclaringType?.Name == "WeightedEntitySelector`1").ToList();
            if (draws.Count != 1) throw new InvalidOperationException("Miracle draw shape changed.");
            var loops = Enumerable.Range(draws[0] + 1, Math.Max(0, code.Count - draws[0] - 8)).Where(i =>
                code[i].operand is MethodInfo method && method.Name == "RemoveItem" &&
                method.DeclaringType?.Name == "WeightedEntitySelector`1" &&
                code[i + 2].LoadsConstant(1) && code[i + 3].opcode == OpCodes.Add && IsLessThan(code[i + 7])).ToList();
            if (loops.Count != 1) throw new InvalidOperationException("Miracle reward loop shape changed.");
            int index = loops[0] + 7;
            int pool = FindLocal(original, guard.GetParameters()[1].ParameterType);
            var load = new CodeInstruction(OpCodes.Ldloc, pool);
            load.labels.AddRange(code[index].labels);
            code[index].labels.Clear();
            load.blocks.AddRange(code[index].blocks);
            code[index].blocks.Clear();
            code.InsertRange(index, new[] { load, new CodeInstruction(OpCodes.Call, guard) });
            return code;
        }

        private static bool IsLessThan(CodeInstruction instruction)
            => instruction.opcode == OpCodes.Blt || instruction.opcode == OpCodes.Blt_S;

        private static int FindLocal(MethodBase method, Type type)
        {
            var body = method.GetMethodBody() ?? throw new InvalidOperationException("Missing method body: " + method.Name);
            var locals = body.LocalVariables.Where(x => x.LocalType == type).ToList();
            if (locals.Count != 1) throw new InvalidOperationException("Candidate pool local changed in " + method.Name);
            return locals[0].LocalIndex;
        }
    }
}
