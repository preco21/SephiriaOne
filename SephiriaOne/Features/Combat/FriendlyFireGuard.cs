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
        // Called only inside the native successful Perfect Guard branch.
        // A nested hit starts with a clear flag and restores the outer scope.
        internal static void MarkPerfectGuard() => current.PerfectGuard = true;

        internal static int GuardMpCost(float nativeCost)
        {
            if (!current.Friendly || !(current.Victim is PlayerAvatar victim) || !NetworkServer.active)
                return (int)nativeCost;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance || !CurrentHitPermitted()) return (int)nativeCost;
            if (current.PerfectGuard && settings.PerfectGuard.Enabled)
            {
                // Native UseMp receives the truncated integer after all guard
                // modifiers. Scale that same amount, not a reconstructed base.
                // This explicit Perfect Guard rule is exempt from the shield
                // minimum, including native free guards. Reject invalid costs.
                current.PerfectGuardMpCost = 0;
                if (!(nativeCost > 0) || float.IsInfinity(nativeCost)) return 0;
                long nativeInteger = (long)Math.Min(int.MaxValue, (double)nativeCost);
                long scaled = (nativeInteger * settings.PerfectGuard.MultiplierHundredths + 99) / 100;
                current.PerfectGuardMpCost = (int)Math.Min(int.MaxValue, scaled);
                return current.PerfectGuardMpCost;
            }
            return BalancedGuardMpCost(nativeCost, victim, settings);
        }

        private static int BalancedGuardMpCost(float nativeCost, PlayerAvatar victim, FriendlyFireSettings settings)
        {
            int minimum = ActiveDefenseMinimum(victim);
            if (!(nativeCost > 10) || float.IsInfinity(nativeCost)) return minimum;
            // Retain proportional cost increases above native's 10 MP base;
            // cost reductions cannot undercut the guaranteed minimum. Keep
            // exact twentieths for callbacks without a broad rounding epsilon.
            double units = nativeCost * 20d;
            double roundedUnits = Math.Round(units);
            if (nativeCost == (float)(roundedUnits / 20d))
                units = roundedUnits;
            // Integer twentieths/percentage units also avoid intermediate
            // rounding before ceiling. No cached max MP or mutable stat state.
            double cost = units * GuardMaximumMp(victim) * settings.GuardMpHundredths / 2000000d;
            return Math.Max(minimum, (int)Math.Min(int.MaxValue, Math.Ceiling(cost)));
        }

        private static int ActiveDefenseMinimum(PlayerAvatar victim) =>
            (int)Math.Min(int.MaxValue, Math.Ceiling(GuardMaximumMp(victim) *
                (double)SessionSettings.FriendlyFireForHit.GuardMpHundredths / 10000d));

        private static bool BalancedDefense(UnitAvatar victim) => NetworkServer.active &&
            current.Friendly && victim is PlayerAvatar && ReferenceEquals(current.Victim, victim) &&
            SessionSettings.FriendlyFireForHit.Balance && CurrentHitPermitted();

        private static bool HasDefenseMpRequirement()
        {
            var settings = SessionSettings.FriendlyFireForHit;
            return current.PerfectGuard && settings.PerfectGuard.Enabled
                ? current.PerfectGuardMpCost > 0 : settings.GuardMpHundredths > 0;
        }

        internal static void SpendGuardMp(UnitAvatar victim, int amount)
        {
            bool balanced = BalancedDefense(victim);
            bool enforce = balanced && amount > 0 && HasDefenseMpRequirement();
            if (balanced) current.DefenseMpSpent = true;
            int before = Math.Max(0, victim.mp);
            if (enforce)
            {
                // Native MP-use artifacts count the callback argument, not
                // the actual debit. Empty/insufficient MP must not manufacture
                // shield or buff rewards from an unpaid floor.
                amount = Math.Min(amount, before);
                if (amount == 0) return;
            }
            victim.UseMp(amount);
            // Native INFINITYMP and synchronous MP-use refunds cannot waive
            // the PvP charge. Revalidate after arbitrary native callbacks.
            if (enforce && BalancedDefense(victim) && HasDefenseMpRequirement())
            {
                int ceiling = Math.Max(0, before - amount);
                if (victim.mp > ceiling) victim.Networkmp = ceiling;
            }
        }

        internal static int GuardInfiniteMp(int nativeValue) => BalancedDefense(current.Victim) &&
            HasDefenseMpRequirement() ? 0 : nativeValue;

        private static int GuardMaximumMp(PlayerAvatar victim)
        {
            if (victim.GetCustomStatUnsafe("INFINITYMP") > 0) return Math.Max(0, victim.MaxMp);
            // Native MaxMp multiplies FinalMP * maxMp as int before casting.
            // Retain its ordinary float rounding, but widen the product and
            // sum so large stacks cannot wrap into a smaller/free guard cost.
            int baseMp = victim.maxMp;
            long product = (long)victim.GetCustomStat(ECustomStat.FinalMP) * baseMp;
            double bonus = product >= int.MinValue && product <= int.MaxValue
                ? (float)product / 100f : product / 100d;
            double total = baseMp + Math.Truncate(bonus);
            return (int)Math.Max(0, Math.Min(int.MaxValue, total));
        }
    }

    internal static class FriendlyFireGuardHooks
    {
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var stat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });
            var spend = AccessTools.DeclaredMethod(typeof(UnitAvatar), "UseMp", new[] { typeof(int) });
            var durability = AccessTools.DeclaredField(typeof(UnitAvatar), "OnCalculateDurabilityDamage");
            if (stat == null || spend == null || spend.IsStatic || spend.ReturnType != typeof(void) || durability == null)
                throw new InvalidOperationException("Native guard MP signature changed.");
            var reads = Enumerable.Range(1, code.Count - 1).Where(i => code[i].Calls(stat) &&
                code[i - 1].LoadsConstant((int)ECustomStat.GuardResist)).ToArray();
            int guard = reads.Length == 1 ? reads[0] : -1;
            int modifiers = guard < 0 ? -1 : code.FindIndex(guard, i => i.LoadsField(durability));
            int cost = modifiers < 0 ? -1 : code.FindIndex(modifiers, i => i.Calls(spend));
            var perfectScales = guard < 0 || modifiers <= guard ? Array.Empty<int>() :
                Enumerable.Range(guard, modifiers - guard).Where(i =>
                    i + 2 < code.Count && code[i].opcode == OpCodes.Ldc_R4 && Equals(code[i].operand, 0.5f) &&
                    code[i - 1].IsLdloc() && code[i + 1].opcode == OpCodes.Mul && code[i + 2].IsStloc()).ToArray();
            if (guard < 3 || modifiers <= guard || cost <= modifiers ||
                !code.Skip(Math.Max(0, guard - 12)).Take(Math.Min(12, guard)).Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 10f)) ||
                perfectScales.Length != 1 ||
                code.Skip(guard).Take(cost - guard).Count(i => i.Calls(spend)) != 0 ||
                code.Skip(modifiers).Take(cost - modifiers).Count(i => i.LoadsField(durability)) != 2 ||
                code[cost - 3].opcode != OpCodes.Ldarg_0 || !code[cost - 2].IsLdloc() || code[cost - 1].opcode != OpCodes.Conv_I4)
                throw new InvalidOperationException("Native guard cost/modifier boundary changed.");
            int perfect = perfectScales[0], costLocal = Local(code[cost - 2]);
            if (costLocal < 0 || Local(code[perfect - 1]) != costLocal || Local(code[perfect + 2]) != costLocal)
                throw new InvalidOperationException("Native Perfect Guard cost local changed.");
            // Adapt the final cost/spend and the native guard-break exemption.
            // Other MP uses are covered only at their audited defense sites.
            var scale = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.GuardMpCost));
            scale.MoveLabelsFrom(code[cost - 1]); scale.MoveBlocksFrom(code[cost - 1]);
            code[cost - 1] = scale;
            var wrappedSpend = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.SpendGuardMp));
            wrappedSpend.MoveLabelsFrom(code[cost]); wrappedSpend.MoveBlocksFrom(code[cost]);
            code[cost] = wrappedSpend;
            var unsafeStat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStatUnsafe", new[] { typeof(string) });
            int infinity = code.FindIndex(cost + 1, i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "INFINITYMP"));
            if (infinity < 0 || infinity + 1 >= code.Count || !code[infinity + 1].Calls(unsafeStat))
                throw new InvalidOperationException("Native guard-break MP exemption changed.");
            var guardBreak = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.GuardInfiniteMp));
            guardBreak.MoveLabelsFrom(code[infinity + 2]); guardBreak.MoveBlocksFrom(code[infinity + 2]);
            code.Insert(infinity + 2, guardBreak);
            // Preserve the evaluated native branch even if guard callbacks
            // change timers/flags before MP is spent. No private-field polling.
            var mark = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.MarkPerfectGuard));
            mark.MoveLabelsFrom(code[perfect]); mark.MoveBlocksFrom(code[perfect]);
            code.Insert(perfect, mark);
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
