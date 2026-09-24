using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class StartingResourceTranspilers
    {
        public static IEnumerable<CodeInstruction> Initialize(IEnumerable<CodeInstruction> instructions,
            MethodInfo money, MethodInfo dice)
        {
            var code = instructions.ToList();
            int seed = UniqueCall(code, "UnitAvatar", "AddMoney", typeof(int), typeof(void));
            int current = UniqueCall(code, "PlayerAvatar", "set_NetworkrerollDice", typeof(int), typeof(void));
            int fallback = UniqueCall(code, "TreeShopItemStorage", "GettStartingMoney", null, typeof(int));
            if (fallback >= seed || seed >= current || seed - fallback > 12 ||
                !code.Any(i => LoadsString(i, "PlayerMoney")) ||
                !code.Any(i => LoadsString(i, "PlayerRerollDice")))
                throw new InvalidOperationException("PlayerSpawner.Initialize starting-resource order changed.");
            Replace(code[seed], money);
            Replace(code[current], dice);
            return code;
        }

        public static IEnumerable<CodeInstruction> Departure(IEnumerable<CodeInstruction> instructions,
            MethodInfo plan, MethodInfo apply)
        {
            var code = instructions.ToList();
            var keys = Enumerable.Range(0, code.Count).Where(i => LoadsString(code[i], "STARTINGMONEY")).ToList();
            if (keys.Count != 1) throw new InvalidOperationException("First-departure STARTINGMONEY read changed.");
            int key = keys[0];
            int add = UniqueCall(code, "UnitAvatar", "AddMoney", typeof(int), typeof(void));
            // 1.0.33: key/read/store/load/zero/ble/player/load/AddMoney. Preserve
            // every label and exception-block marker by replacing calls only.
            if (key < 1 || key + 8 >= code.Count || add != key + 8 ||
                !IsCall(code[key + 1], "UnitAvatar", "GetCustomStatUnsafe", typeof(string), typeof(int)) ||
                !code[key + 4].LoadsConstant(0) ||
                (code[key + 5].opcode != OpCodes.Ble && code[key + 5].opcode != OpCodes.Ble_S) ||
                !SameLocal(code[key + 2], code[key + 3]) || !SameLocal(code[key + 3], code[key + 7]) ||
                !SameLocal(code[key - 1], code[key + 6]))
                throw new InvalidOperationException("First-departure money grant shape changed.");
            Replace(code[key + 1], plan);
            Replace(code[add], apply);
            return code;
        }

        private static int UniqueCall(List<CodeInstruction> code, string type, string name, Type argument, Type result)
        {
            var matches = Enumerable.Range(0, code.Count).Where(i => IsCall(code[i], type, name, argument, result)).ToList();
            if (matches.Count != 1) throw new InvalidOperationException(type + "." + name + " signature/count changed.");
            return matches[0];
        }

        private static bool IsCall(CodeInstruction instruction, string type, string name, Type argument, Type result)
        {
            if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) ||
                !(instruction.operand is MethodInfo method) || method.IsStatic ||
                method.DeclaringType?.Name != type || method.Name != name || method.ReturnType != result) return false;
            var parameters = method.GetParameters();
            return argument == null ? parameters.Length == 0 : parameters.Length == 1 && parameters[0].ParameterType == argument;
        }

        private static bool SameLocal(CodeInstruction a, CodeInstruction b)
        {
            int Local(CodeInstruction i)
            {
                if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
                if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
                if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
                if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;
                if (i.opcode != OpCodes.Ldloc && i.opcode != OpCodes.Ldloc_S &&
                    i.opcode != OpCodes.Stloc && i.opcode != OpCodes.Stloc_S) return -1;
                if (i.operand is LocalBuilder local) return local.LocalIndex;
                if (i.operand is LocalVariableInfo info) return info.LocalIndex;
                return Convert.ToInt32(i.operand);
            }
            int left = Local(a);
            return left >= 0 && left == Local(b);
        }

        private static void Replace(CodeInstruction instruction, MethodInfo replacement)
        {
            if (replacement == null || !replacement.IsStatic) throw new InvalidOperationException("Missing starting-resource adapter.");
            instruction.opcode = OpCodes.Call;
            instruction.operand = replacement;
        }

        // HarmonyX may have been loaded by another addon. This overload is not
        // part of its shared API surface, so inspect the stable IL representation.
        private static bool LoadsString(CodeInstruction instruction, string text) =>
            instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, text);
    }
}
