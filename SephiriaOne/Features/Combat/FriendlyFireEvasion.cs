using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        // Transform only the native stat-derived chance, before anti-Evasion.
        // No stat writes, new RNG calls or retained state; guaranteed item dodges
        // and native dodge callbacks continue through their separate branches.
        internal static float EvasionChance(float nativeChance)
        {
            if (!current.Friendly || !(current.Victim is PlayerAvatar) || !NetworkServer.active ||
                !CurrentHitPermitted()) return nativeChance;
            var settings = SessionSettings.FriendlyFireForHit;
            return settings.Balance ? settings.Evasion.Limit(nativeChance) : nativeChance;
        }
    }

    internal static class FriendlyFireEvasionHooks
    {
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var stat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });
            var log = AccessTools.DeclaredMethod(typeof(Mathf), "Log", new[] { typeof(float) });
            var roll = AccessTools.DeclaredMethod(typeof(UnityEngine.Random), "Range", new[] { typeof(float), typeof(float) });
            if (stat == null || log == null || roll == null || stat.ReturnType != typeof(int) || log.ReturnType != typeof(float))
                throw new InvalidOperationException("Native Evasion signatures changed.");
            var helper = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetEvasionChancePercent", Type.EmptyTypes);
            var helperCalls = Enumerable.Range(0, code.Count).Where(i => helper != null && code[i].Calls(helper)).ToArray();
            if (helperCalls.Length != 0)
            {
                int call = helperCalls[0];
                int antiEvasion = code.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "IGNOREEVASION"));
                if (helperCalls.Length != 1 || helper.IsStatic || helper.ReturnType != typeof(float) || call < 1 ||
                    !code[call - 1].IsLdarg(0) || call + 1 >= code.Count || !code[call + 1].IsStloc() || antiEvasion <= call + 1 ||
                    code.FindIndex(antiEvasion, i => i.Calls(roll)) <= antiEvasion || code.Any(i => i.Calls(log)) ||
                    !ValidateChanceHelper(PatchProcessor.GetOriginalInstructions(helper)))
                    throw new InvalidOperationException("Native extracted Evasion chance boundary changed.");
                InsertCap(code, call + 1);
                return code;
            }
            var reads = Enumerable.Range(1, code.Count - 1).Where(i => code[i].Calls(stat) &&
                code[i - 1].LoadsConstant((int)ECustomStat.Evasion)).ToArray();
            var logs = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(log)).ToArray();
            int evasion = reads.Length == 1 ? reads[0] : -1;
            int formula = logs.Length == 1 ? logs[0] : -1;
            int ignore = code.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "IGNOREEVASION"));
            int random = ignore < 0 ? -1 : code.FindIndex(ignore, i => i.Calls(roll));
            // Native chance: 100 * Log(evasion / 6200 + 1) * 0.8.
            // Its next store precedes the lower clamp, attacker reduction and
            // both native rolls. Do not replace a generic stat or RNG reader.
            if (evasion < 0 || formula < evasion + 7 || formula + 4 >= code.Count || ignore <= formula + 4 || random <= ignore ||
                !code.Skip(evasion).Take(formula - evasion).Any(i => i.LoadsConstant(10000f)) ||
                !code[formula - 6].LoadsConstant(100f) || !code[formula - 5].IsLdloc() ||
                !code[formula - 4].LoadsConstant(6200f) || code[formula - 3].opcode != OpCodes.Div ||
                !code[formula - 2].LoadsConstant(1f) || code[formula - 1].opcode != OpCodes.Add ||
                code[formula + 1].opcode != OpCodes.Mul || !code[formula + 2].LoadsConstant(0.8f) ||
                code[formula + 3].opcode != OpCodes.Mul || !code[formula + 4].IsStloc())
                throw new InvalidOperationException("Native stat-derived Evasion formula boundary changed.");
            InsertCap(code, formula + 4);
            return code;
        }
        private static void InsertCap(List<CodeInstruction> code, int at)
        {
            var cap = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.EvasionChance));
            cap.MoveLabelsFrom(code[at]); cap.MoveBlocksFrom(code[at]);
            code.Insert(at, cap);
        }

        internal static bool ValidateChanceHelper(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Where(i => i.opcode != OpCodes.Nop).ToList();
            var stat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });
            var unsafeStat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStatUnsafe", new[] { typeof(string) });
            var log = AccessTools.DeclaredMethod(typeof(Mathf), "Log", new[] { typeof(float) });
            // Audited 1.1.0 helper: the original formula, its new signed rate
            // modifier, then the lower clamp. Cap its final result at the caller
            // so C-menu reads of this helper retain native probability.
            if (code.Count != 43 || !code[0].IsLdarg(0) || !code[1].LoadsConstant((int)ECustomStat.Evasion) || !code[2].Calls(stat) ||
                code[3].opcode != OpCodes.Conv_R4 || !code[4].IsStloc() || !Local(code[5], code[4]) ||
                !code[6].LoadsConstant(10000f) || !Branch(code, 7, 10, OpCodes.Ble_Un, OpCodes.Ble_Un_S) ||
                !code[8].LoadsConstant(10000f) || !Store(code[9], code[4]) || !code[10].LoadsConstant(100f) ||
                !Local(code[11], code[4]) || !code[12].LoadsConstant(6200f) || code[13].opcode != OpCodes.Div ||
                !code[14].LoadsConstant(1f) || code[15].opcode != OpCodes.Add || !code[16].Calls(log) ||
                code[17].opcode != OpCodes.Mul || !code[18].LoadsConstant(0.8f) || code[19].opcode != OpCodes.Mul || !code[20].IsStloc() ||
                !code[21].IsLdarg(0) || code[22].opcode != OpCodes.Ldstr || !Equals(code[22].operand, "EVASIONRATEREDUCTION") ||
                !code[23].Calls(unsafeStat) || !code[24].IsStloc() || !Local(code[25], code[24]) ||
                !Branch(code, 26, 36, OpCodes.Brfalse, OpCodes.Brfalse_S) || !Local(code[27], code[20]) ||
                !code[28].LoadsConstant(100) || !Local(code[29], code[24]) || code[30].opcode != OpCodes.Add ||
                code[31].opcode != OpCodes.Conv_R4 || code[32].opcode != OpCodes.Mul || !code[33].LoadsConstant(100f) ||
                code[34].opcode != OpCodes.Div || !Store(code[35], code[20]) || !Local(code[36], code[20]) ||
                !code[37].LoadsConstant(0f) || !Branch(code, 38, 41, OpCodes.Bge_Un, OpCodes.Bge_Un_S) ||
                !code[39].LoadsConstant(0f) || !Store(code[40], code[20]) || !Local(code[41], code[20]) || code[42].opcode != OpCodes.Ret)
                return false;
            return true;
        }

        private static bool Store(CodeInstruction a, CodeInstruction b) => a.opcode == b.opcode && Equals(a.operand, b.operand);
        private static bool Local(CodeInstruction load, CodeInstruction store)
        {
            OpCode expected = store.opcode == OpCodes.Stloc_0 ? OpCodes.Ldloc_0 : store.opcode == OpCodes.Stloc_1 ? OpCodes.Ldloc_1 :
                store.opcode == OpCodes.Stloc_2 ? OpCodes.Ldloc_2 : store.opcode == OpCodes.Stloc_3 ? OpCodes.Ldloc_3 :
                store.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc;
            return store.IsStloc() && load.opcode == expected && Equals(load.operand, store.operand);
        }
        private static bool Branch(List<CodeInstruction> code, int at, int target, OpCode wide, OpCode shortForm) =>
            (code[at].opcode == wide || code[at].opcode == shortForm) && code[at].operand is Label label && code[target].labels.Contains(label);
    }
}
