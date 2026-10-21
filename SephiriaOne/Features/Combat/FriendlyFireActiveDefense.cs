using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal static int ParryAllowed(int nativeCount) => nativeCount > 0 && BalancedDefense(current.Victim) &&
            current.Victim.mp <= 0 && ActiveDefenseMinimum((PlayerAvatar)current.Victim) > 0 ? 0 : nativeCount;

        internal static void BeginParryMp()
        {
            // Capture even if a native callback enables balancing. This lives
            // in the nested hit scope, never on a weapon or connection ID.
            current.ParryMpBefore = current.Victim ? current.Victim.mp : 0;
        }

        internal static void AfterParryMp()
        {
            if (!BalancedDefense(current.Victim)) return;
            var victim = (PlayerAvatar)current.Victim;
            int minimum = ActiveDefenseMinimum(victim);
            if (minimum <= 0) return;
            // Dagger's successful-parry refund is synchronous. Charge it back
            // along with the floor; native activation costs remain separate.
            long refund = Math.Max(0L, (long)victim.mp - current.ParryMpBefore);
            SpendGuardMp(victim, (int)Math.Min(int.MaxValue, minimum + refund));
        }

        internal static float AbsorbShield(UnitAvatar victim, float incoming)
        {
            float remaining = victim.UseShield(incoming);
            if (incoming > remaining && BalancedDefense(victim)) current.ShieldAbsorbed = true;
            return remaining;
        }
        internal static void BeforeSubShield(DamageInstance damage, out bool __state) =>
            __state = damage != null && damage.failed == EDamageFailType.None;
        internal static void AfterSubShield(UnitSubShield __instance, DamageInstance damage, bool __state)
        {
            if (__state && damage.failed == EDamageFailType.Block && ReferenceEquals(damage, current.Damage) &&
                BalancedDefense(__instance.owner)) current.ShieldAbsorbed = true;
        }
        private static void FinishDefenseMp()
        {
            if (current.ShieldAbsorbed && !current.DefenseMpSpent && BalancedDefense(current.Victim))
            {
                int minimum = ActiveDefenseMinimum((PlayerAvatar)current.Victim);
                if (minimum > 0) SpendGuardMp(current.Victim, minimum);
            }
        }

        internal static int ActiveMpShieldCost(float resolvedDamage)
        {
            // A preceding shield may absorb the entire hit. Fold its floor
            // into this existing MP-use site, avoiding a zero-use callback
            // followed by a second charged callback during final settlement.
            current.ActiveShieldSpend = resolvedDamage >= 0 && (resolvedDamage > 0 || current.ShieldAbsorbed) && !float.IsInfinity(resolvedDamage) &&
                BalancedDefense(current.Victim);
            if (!current.ActiveShieldSpend) return (int)resolvedDamage;
            return Math.Max(ActiveDefenseMinimum((PlayerAvatar)current.Victim),
                (int)Math.Min(int.MaxValue, (double)resolvedDamage));
        }

        internal static void SpendActiveShieldMp(UnitAvatar victim, int amount)
        {
            if (current.ActiveShieldSpend) SpendGuardMp(victim, amount);
            else victim.UseMp(amount);
        }

        internal static void SpendPartialShieldMp(UnitAvatar victim, int amount)
        {
            // Native partial absorption already spends all remaining MP.
            // Keep its HP spill exactly as calculated before this call.
            if (BalancedDefense(victim)) SpendGuardMp(victim, amount);
            else victim.UseMp(amount);
        }
    }

    internal static class FriendlyFireActiveDefenseHooks
    {
        internal static void Install(Harmony harmony)
        {
            var method = AccessTools.DeclaredMethod(typeof(UnitSubShield), "OnCalculateDamage", new[] { typeof(DamageInstance) });
            if (method == null || method.IsStatic || method.ReturnType != typeof(void))
                throw new InvalidOperationException("Native subshield callback changed.");
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeSubShield)),
                postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterSubShield)));
        }

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var parry = AccessTools.DeclaredField(typeof(UnitAvatar), "parryInvincibleApplied");
            var callback = AccessTools.DeclaredField(typeof(UnitAvatar), "OnParry");
            var timer = AccessTools.DeclaredField(typeof(UnitAvatar), "parrySuccessBonusTimer");
            var mp = AccessTools.DeclaredField(typeof(UnitAvatar), "mp");
            var spend = AccessTools.DeclaredMethod(typeof(UnitAvatar), "UseMp", new[] { typeof(int) });
            int Unique(Func<CodeInstruction, bool> test)
            {
                var matches = Enumerable.Range(0, code.Count).Where(i => test(code[i])).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Native active-defense boundary is ambiguous.");
                return matches[0];
            }
            int counter = Unique(i => i.LoadsField(parry));
            // The native null-conditional delegate has a common continuation.
            int begin = code.FindIndex(counter, i => i.LoadsField(callback)) - 1;
            int after = Unique(i => i.LoadsField(timer)) - 1;
            int shield = code.FindLastIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "MPSHIELD"));
            if (shield <= after) throw new InvalidOperationException("Native MP-shield boundary changed.");
            var absorb = AccessTools.DeclaredMethod(typeof(UnitAvatar), "UseShield", new[] { typeof(float) });
            int absorbIndex = Unique(i => i.Calls(absorb));
            var spends = Enumerable.Range(shield, code.Count - shield).Where(i => code[i].Calls(spend)).ToArray();
            if (counter < 0 || !code[counter + 1].LoadsConstant(0) || begin <= counter || after <= begin ||
                code[begin].opcode != OpCodes.Ldarg_0 || code[after].opcode != OpCodes.Ldarg_0 ||
                code.Skip(begin).Take(after - begin).Count(i => i.LoadsField(callback)) != 1 ||
                absorbIndex <= after || absorbIndex >= shield || spends.Length != 2 || !code[spends[0] - 1].LoadsField(mp) ||
                code[spends[1] - 1].opcode != OpCodes.Conv_I4 || !code[spends[1] - 2].IsLdloc())
                throw new InvalidOperationException("Native parry or active MP-shield accounting changed.");
            void Replace(int index, string helper)
            {
                var call = CodeInstruction.Call(typeof(FriendlyFireRuntime), helper);
                call.MoveLabelsFrom(code[index]); call.MoveBlocksFrom(code[index]); code[index] = call;
            }
            void Insert(int index, string helper)
            {
                var call = CodeInstruction.Call(typeof(FriendlyFireRuntime), helper);
                call.MoveLabelsFrom(code[index]); call.MoveBlocksFrom(code[index]); code.Insert(index, call);
            }
            Replace(spends[1], nameof(FriendlyFireRuntime.SpendActiveShieldMp));
            Replace(spends[1] - 1, nameof(FriendlyFireRuntime.ActiveMpShieldCost));
            Replace(spends[0], nameof(FriendlyFireRuntime.SpendPartialShieldMp));
            Replace(absorbIndex, nameof(FriendlyFireRuntime.AbsorbShield));
            Insert(after, nameof(FriendlyFireRuntime.AfterParryMp));
            Insert(begin, nameof(FriendlyFireRuntime.BeginParryMp));
            Insert(counter + 1, nameof(FriendlyFireRuntime.ParryAllowed));
            return code;
        }
    }
}
