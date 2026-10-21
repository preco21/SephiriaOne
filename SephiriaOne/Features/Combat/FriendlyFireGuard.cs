using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal static int GuardMpCost(float nativeCost)
        {
            if (!current.Friendly || !(current.Victim is PlayerAvatar victim) || !NetworkServer.active)
                return (int)nativeCost;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance || !CurrentHitPermitted()) return (int)nativeCost;
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

        internal static void SpendGuardMp(UnitAvatar victim, int amount)
        {
            bool balanced = BalancedDefense(victim);
            bool enforce = balanced && amount > 0 && SessionSettings.FriendlyFireForHit.GuardMpHundredths > 0;
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
            // the PvP floor. Revalidate after arbitrary native callbacks.
            if (enforce && BalancedDefense(victim) && SessionSettings.FriendlyFireForHit.GuardMpHundredths > 0)
            {
                int ceiling = Math.Max(0, before - amount);
                if (victim.mp > ceiling) victim.Networkmp = ceiling;
            }
        }

        internal static int GuardInfiniteMp(int nativeValue) => BalancedDefense(current.Victim) &&
            SessionSettings.FriendlyFireForHit.GuardMpHundredths > 0 ? 0 : nativeValue;

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
            if (guard < 3 || modifiers <= guard || cost <= modifiers ||
                !code.Skip(Math.Max(0, guard - 12)).Take(Math.Min(12, guard)).Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 10f)) ||
                !code.Skip(guard).Take(modifiers - guard).Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 0.5f)) ||
                code.Skip(guard).Take(cost - guard).Count(i => i.Calls(spend)) != 0 ||
                code.Skip(modifiers).Take(cost - modifiers).Count(i => i.LoadsField(durability)) != 2 ||
                code[cost - 3].opcode != OpCodes.Ldarg_0 || !code[cost - 2].IsLdloc() || code[cost - 1].opcode != OpCodes.Conv_I4)
                throw new InvalidOperationException("Native guard cost/modifier boundary changed.");
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
            return code;
        }
    }
}
