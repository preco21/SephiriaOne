using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        // Transform the native attacker+leader bonus once, at its consumption.
        // Reuse the scoped hit/connection/mode permission; no player stat writes,
        // new network state, or restore work is needed on any lifecycle transition.
        internal static float LimitAmplification(float nativeBonus)
        {
            if (!current.Friendly || !(current.Victim is PlayerAvatar) || !NetworkServer.active) return nativeBonus;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance || !settings.Amplification.Enabled || !CurrentHitPermitted()) return nativeBonus;
            return settings.Amplification.Limit(nativeBonus);
        }
    }

    internal static class FriendlyFireAmplificationHooks
    {
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var stat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });
            var leader = AccessTools.PropertyGetter(typeof(UnitAvatar), "NetworkLeader");
            var damage = AccessTools.DeclaredField(typeof(DamageInstance), "damage");
            if (stat == null || stat.ReturnType != typeof(int) || leader == null || damage == null || damage.FieldType != typeof(float))
                throw new InvalidOperationException("Native All Damage Amplified signatures changed.");
            var reads = Enumerable.Range(1, Math.Max(0, code.Count - 1)).Where(i => code[i].Calls(stat) &&
                code[i - 1].LoadsConstant((int)ECustomStat.AllDamageBonus)).ToArray();
            if (reads.Length != 2 || reads[0] < 2 || reads[1] < 4 || reads[1] + 2 >= code.Count)
                throw new InvalidOperationException("Native All Damage Amplified reads changed.");
            int own = reads[0], owner = reads[1];
            int source = Local(code[own - 2]), bonus = Local(code[own + 1]);
            if (!code[own - 2].IsLdloc() || !code[own + 1].IsStloc() || source < 0 || bonus < 0 ||
                !code[owner - 4].IsLdloc() || Local(code[owner - 4]) != bonus ||
                !code[owner - 3].IsLdloc() || Local(code[owner - 3]) != source || !code[owner - 2].Calls(leader) ||
                code[owner + 1].opcode != OpCodes.Add || !code[owner + 2].IsStloc() || Local(code[owner + 2]) != bonus)
                throw new InvalidOperationException("Native attacker/leader amplification aggregation changed.");
            var uses = Enumerable.Range(0, code.Count).Where(i => code[i].IsLdloc() && Local(code[i]) == bonus).ToArray();
            if (uses.Length != 2 || uses[0] != owner - 4)
                throw new InvalidOperationException("Native combined amplification consumers changed.");
            int consume = uses[1];
            // damage.damage * ((float)(attacker bonus + optional leader bonus) / 100f)
            if (consume <= owner + 2 || consume + 4 >= code.Count || code[consume - 2].opcode != OpCodes.Ldarg_1 ||
                !code[consume - 1].LoadsField(damage) || code[consume + 1].opcode != OpCodes.Conv_R4 ||
                !code[consume + 2].LoadsConstant(100f) || code[consume + 3].opcode != OpCodes.Div || code[consume + 4].opcode != OpCodes.Mul)
                throw new InvalidOperationException("Native combined amplification damage boundary changed.");
            var limit = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.LimitAmplification));
            limit.MoveLabelsFrom(code[consume + 2]); limit.MoveBlocksFrom(code[consume + 2]);
            code.Insert(consume + 2, limit);
            return code;
        }

        private static int Local(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Stloc_0) return 0;
            if (instruction.opcode == OpCodes.Ldloc_1 || instruction.opcode == OpCodes.Stloc_1) return 1;
            if (instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Stloc_2) return 2;
            if (instruction.opcode == OpCodes.Ldloc_3 || instruction.opcode == OpCodes.Stloc_3) return 3;
            return instruction.operand is LocalVariableInfo local ? local.LocalIndex :
                instruction.operand is byte b ? b : instruction.operand is short s ? s : instruction.operand is int i ? i : -1;
        }
    }
}
