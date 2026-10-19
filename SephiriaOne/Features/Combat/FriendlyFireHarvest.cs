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
        // Only the native Harvest call sites use these helpers. Generic heals,
        // potions and nested PvE hits cannot borrow the surrounding PvP scope.
        // Read current policy; no stat override or restoration journal survives.
        internal static void HarvestHp(UnitAvatar recipient, float amount)
        {
            // Heal(0) still writes health and invokes native healing callbacks.
            // Skip the grant entirely, including admitted companion victims,
            // independently of the optional player-damage balancing policy.
            if (current.Friendly && NetworkServer.active && CurrentHitPermitted()) return;
            recipient.Heal(amount);
        }

        internal static float HarvestMpAmount(float amount)
        {
            if (!current.Friendly || !(current.Victim is PlayerAvatar) || !NetworkServer.active ||
                !SessionSettings.FriendlyFireForHit.Balance || !CurrentHitPermitted()) return amount;
            return amount > 0 && !float.IsInfinity(amount) ? amount * 0.2f : 0;
        }
    }

    internal static class FriendlyFireHarvestHooks
    {
        // Composed into the existing ApplyDamage transpiler/guard: installation
        // fails closed and unloading removes all combat adjustments together.
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var hp = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Heal", new[] { typeof(float) });
            var mp = AccessTools.DeclaredMethod(typeof(UnitAvatar), "HealMpFloat", new[] { typeof(float) });
            var stat = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });
            var extraHp = AccessTools.DeclaredField(typeof(DamageInstance), "hpSteal");
            if (hp == null || mp == null || stat == null || hp.IsStatic || mp.IsStatic || stat.IsStatic ||
                hp.ReturnType != typeof(void) || mp.ReturnType != typeof(void) || stat.ReturnType != typeof(int) ||
                extraHp == null || extraHp.FieldType != typeof(float))
                throw new InvalidOperationException("Native Harvest signatures changed.");
            int hpCall = code.FindIndex(i => i.Calls(hp)), mpCall = code.FindIndex(i => i.Calls(mp));
            int StatRead(ECustomStat value)
            {
                int found = -1;
                for (int i = 1; i < code.Count; i++)
                    if (code[i].Calls(stat) && code[i - 1].LoadsConstant((int)value))
                    { if (found >= 0) return -1; found = i; }
                return found;
            }
            int hpRead = StatRead(ECustomStat.HPSteal), mpRead = StatRead(ECustomStat.MPSteal);
            if (hpRead < 0 || hpCall <= hpRead || mpRead <= hpCall || mpCall <= mpRead ||
                code.Count(i => i.Calls(hp)) != 1 || code.Count(i => i.Calls(mp)) != 1 ||
                code.Skip(hpRead).Take(hpCall - hpRead).Count(i => i.LoadsField(extraHp)) != 2 ||
                !code[hpCall - 2].IsLdloc() || !code[hpCall - 1].IsLdloc() ||
                !code[mpCall - 2].IsLdloc() || !code[mpCall - 1].IsLdloc())
                throw new InvalidOperationException("Native HP/MP Harvest recovery boundaries changed.");
            // The HP wrapper consumes the same recipient/amount stack as Heal.
            // MP still adjusts only the amount, preserving its native caps,
            // modifiers and fractional accumulation.
            var hpGrant = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.HarvestHp));
            hpGrant.MoveLabelsFrom(code[hpCall]); hpGrant.MoveBlocksFrom(code[hpCall]);
            code[hpCall] = hpGrant;
            var mpScale = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.HarvestMpAmount));
            mpScale.MoveLabelsFrom(code[mpCall]); mpScale.MoveBlocksFrom(code[mpCall]);
            code.Insert(mpCall, mpScale);
            return code;
        }
    }
}
