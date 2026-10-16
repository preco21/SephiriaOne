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
                !SessionSettings.FriendlyFireForHit.Balance || !CurrentHitPermitted()) return nativeChance;
            return Math.Min(nativeChance, 50f);
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
            var cap = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.EvasionChance));
            cap.MoveLabelsFrom(code[formula + 4]); cap.MoveBlocksFrom(code[formula + 4]);
            code.Insert(formula + 4, cap);
            return code;
        }
    }
}
