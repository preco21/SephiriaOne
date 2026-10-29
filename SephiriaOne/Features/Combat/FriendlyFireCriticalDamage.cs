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
    internal static partial class FriendlyFireRuntime
    {
        internal static float LimitCriticalDamage(float nativeRate)
        {
            if (!current.Friendly || !(current.Victim is PlayerAvatar) || !NetworkServer.active) return nativeRate;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance || !settings.CriticalDamage.Enabled || !CurrentHitPermitted()) return nativeRate;
            // Preserve the native +50% critical base and signed penalties. This
            // value is local to damage calculation, never written into the attack.
            return 50f + settings.CriticalDamage.Limit(nativeRate - 50f);
        }
    }

    internal static class FriendlyFireCriticalDamageHooks
    {
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var rate = AccessTools.DeclaredField(typeof(DamageInstance), "criticalDamageRate");
            var multiplier = AccessTools.DeclaredField(typeof(DamageInstance), "criticalDamageMultiplier");
            var round = AccessTools.DeclaredMethod(typeof(Mathf), "RoundToInt", new[] { typeof(float) });
            var adapter = AccessTools.DeclaredMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.LimitCriticalDamage));
            if (rate?.FieldType != typeof(int) || multiplier?.FieldType != typeof(float) || round?.ReturnType != typeof(int) ||
                code.Any(i => i.Calls(adapter))) throw new InvalidOperationException("Native critical damage signatures changed.");
            var rates = Enumerable.Range(0, code.Count).Where(i => code[i].LoadsField(rate)).ToArray();
            var multipliers = Enumerable.Range(0, code.Count).Where(i => code[i].LoadsField(multiplier)).ToArray();
            if (rates.Length != 1 || multipliers.Length != 2) throw new InvalidOperationException("Native critical damage reads changed.");
            int read = rates[0], gate = multipliers[0] - 1, multiply = multipliers[1], store = multiply + 3;
            int local = read + 1 < code.Count ? Local(code[read + 1]) : -1;
            if (local < 0 || !code[read + 1].IsStloc() || gate <= read + 1 || store >= code.Count ||
                code[gate].opcode != OpCodes.Ldarg_1 || !code[gate + 2].LoadsConstant(1f) ||
                (code[gate + 3].opcode != OpCodes.Beq && code[gate + 3].opcode != OpCodes.Beq_S) ||
                multiply != gate + 7 || !Loads(code[multiply - 3], local) || code[multiply - 2].opcode != OpCodes.Conv_R4 ||
                code[multiply - 1].opcode != OpCodes.Ldarg_1 || code[multiply + 1].opcode != OpCodes.Mul ||
                !code[multiply + 2].Calls(round) || !code[store].IsStloc() || Local(code[store]) != local ||
                !code[store + 1].labels.Contains((Label)code[gate + 3].operand))
                throw new InvalidOperationException("Native critical multiplier boundary changed.");

            var uses = Enumerable.Range(store + 1, code.Count - store - 1).Where(i => Loads(code[i], local)).ToArray();
            if (uses.Length != 2 || !Consumer(code, uses[0], true) || !Consumer(code, uses[1], false))
                throw new InvalidOperationException("Native critical/execution consumers changed.");
            // Verify the rate still accumulates the companion leader contribution
            // before this boundary; future accumulator writes need a fresh audit.
            var writes = Enumerable.Range(read + 2, code.Count - read - 2)
                .Where(i => code[i].IsStloc() && Local(code[i]) == local).ToArray();
            var stat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });
            var leader = AccessTools.PropertyGetter(typeof(UnitAvatar), "NetworkLeader");
            int leaderStat = code.FindIndex(read + 2, gate - read - 2, i => i.Calls(stat));
            if (writes.Length != 2 || writes[0] >= gate || writes[1] != store || code[writes[0] - 1].opcode != OpCodes.Add ||
                leaderStat < read + 4 || !code[leaderStat - 1].LoadsConstant((int)ECustomStat.CriticalDamageBonus) ||
                !code[leaderStat - 2].Calls(leader))
                throw new InvalidOperationException("Native companion critical aggregation changed.");

            // A float local preserves fractional curve output. The optional native
            // per-attack multiplier retains its integer rounding; execution keeps
            // its x2 mechanic. All native fields, stat reads and callbacks survive.
            var effective = generator.DeclareLocal(typeof(float));
            foreach (int use in new[] { multiply - 3, uses[0], uses[1] })
            {
                code[use].opcode = OpCodes.Ldloc;
                code[use].operand = effective;
            }
            code.InsertRange(store + 1, new[] { new CodeInstruction(OpCodes.Ldloc, local),
                new CodeInstruction(OpCodes.Conv_R4), new CodeInstruction(OpCodes.Stloc, effective) });
            var load = new CodeInstruction(OpCodes.Ldloc, local);
            load.MoveLabelsFrom(code[gate]); load.MoveBlocksFrom(code[gate]);
            code.InsertRange(gate, new[] { load, new CodeInstruction(OpCodes.Conv_R4),
                new CodeInstruction(OpCodes.Call, adapter), new CodeInstruction(OpCodes.Stloc, effective) });
            return code;
        }

        private static bool Consumer(IReadOnlyList<CodeInstruction> code, int i, bool execution)
        {
            int end = i + (execution ? 8 : 6);
            return i >= 2 && end < code.Count && code[i - 2].IsLdloc() && code[i - 1].IsLdloc() &&
                Local(code[i - 2]) == Local(code[i - 1]) && code[i + 1].opcode == OpCodes.Conv_R4 &&
                code[i + 2].LoadsConstant(100f) && code[i + 3].opcode == OpCodes.Div && code[i + 4].opcode == OpCodes.Mul &&
                (!execution || code[i + 5].LoadsConstant(2f) && code[i + 6].opcode == OpCodes.Mul) &&
                code[end - 1].opcode == OpCodes.Add && code[end].IsStloc() && Local(code[end]) == Local(code[i - 1]);
        }
        private static bool Loads(CodeInstruction i, int local) => i.IsLdloc() && Local(i) == local;
        private static int Local(CodeInstruction i)
        {
            if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
            if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
            if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
            if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;
            return i.operand is LocalVariableInfo l ? l.LocalIndex : i.operand is byte b ? b :
                i.operand is short s ? s : i.operand is int n ? n : -1;
        }
    }
}
