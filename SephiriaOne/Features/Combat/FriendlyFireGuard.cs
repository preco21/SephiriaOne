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
            if (!(nativeCost > 0) || float.IsInfinity(nativeCost)) return 0;
            // Native cost starts at 10, then applies guard/perfect-guard/item
            // modifiers. Scale that result, preserving the native callbacks.
            // Integer Guard Efficiency and perfect guard produce multiples of
            // 0.05. Recognize exact native float results on that grid: at 99%
            // efficiency the game produces 0.10000038 instead of 0.1. Without
            // this, ceiling can charge 2 MP instead of 1. Preserve off-grid
            // callbacks and positive tiny costs; do not use a broad epsilon
            // that would absorb genuine nearby callback adjustments.
            double units = nativeCost * 20d;
            double roundedUnits = Math.Round(units);
            if (roundedUnits > 0 && (nativeCost == (float)(roundedUnits / 20d) ||
                roundedUnits <= 200 && roundedUnits % 2 == 0 && nativeCost == 10f - 10f * (100 - (int)roundedUnits / 2) / 100f ||
                roundedUnits <= 100 && nativeCost == (10f - 10f * (100 - (int)roundedUnits) / 100f) * 0.5f))
                units = roundedUnits;
            // Integer twentieths/percentage units also avoid intermediate
            // rounding before ceiling. No cached max MP or mutable stat state.
            double cost = units * Math.Max(0, victim.MaxMp) * settings.GuardMpHundredths / 2000000d;
            return (int)Math.Min(int.MaxValue, Math.Ceiling(cost));
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
            // Replace only the final integer conversion. MP shields, parries,
            // native UseMp/infinite-MP and subsequent guard break stay intact.
            var scale = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.GuardMpCost));
            scale.MoveLabelsFrom(code[cost - 1]); scale.MoveBlocksFrom(code[cost - 1]);
            code[cost - 1] = scale;
            return code;
        }
    }
}
