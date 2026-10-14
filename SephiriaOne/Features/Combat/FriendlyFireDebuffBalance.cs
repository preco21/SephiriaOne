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
        internal struct DebuffPulse
        {
            private bool started;
            private float last;
            internal bool Ready(float now) => !float.IsNaN(now) && !float.IsInfinity(now) &&
                (!started || now < last || now - last >= 0.25f);
            internal void Reserve(float now) { started = true; last = now; }
        }

        // Shock (including Plasma) and Freeze deal damage on refresh/application,
        // not a periodic tick. Limit only their resolved damage, never the native
        // caller's stun/stack/expiry work that follows ApplyDamage.
        private static int DebuffPulseKind(DamageInstance damage) => damage != null &&
            damage.damageType == EDamageType.ElementalEffectDamage && damage.fromType == EDamageFromType.None
                ? damage.id == "Debuff_Electric" ? 1 : damage.id == "Debuff_Freeze" ? 2 : 0 : 0;

        // Scale elapsed time, not the shared prefab or the debuff's lifetime.
        // No retained override needs restoration when policy/ownership changes.
        internal static float DebuffTickDelta(float elapsed, CharacterDebuff effect)
        {
            if (!NetworkServer.active || !SessionSettings.FriendlyFireForHit.Balance ||
                !effect || effect is CharacterDebuff_Poison poison && poison.isSystemDamage ||
                !(effect.NetworkTarget is PlayerAvatar victim) || !victim) return elapsed;
            var source = effect.NetworkAttacker;
            if (!source || ReferenceEquals(source, victim) || !CanAffectTeam(source, victim)) return elapsed;
            return elapsed * 0.75f;
        }
    }

    internal static class FriendlyFireDebuffBalanceHooks
    {
        private static readonly (Type Type, string Field, bool Speed, int DeltaReads)[] Sites =
        {
            (typeof(CharacterDebuff_Burn), "tickTimer", true, 1),
            (typeof(CharacterDebuff_Plasma), "tickTimer", true, 2),
            (typeof(CharacterDebuff_Frostbite), "frostDamageTimer", false, 1),
            (typeof(CharacterDebuff_Poison), "poisonTickTimer", false, 1),
            (typeof(CharacterDebuff_Wound), "tickTimer", false, 1)
        };
        internal static IEnumerable<MethodInfo> Updates() => Sites.Select(s =>
            AccessTools.DeclaredMethod(s.Type, "OnUpdate_Server", Type.EmptyTypes));

        internal static void Install(Harmony harmony)
        {
            var updates = Updates().ToArray();
            foreach (var update in updates)
            {
                if (update == null || update.IsStatic || update.ReturnType != typeof(void))
                    throw new InvalidOperationException("Native debuff tick entry point changed.");
                Rewrite(PatchProcessor.GetOriginalInstructions(update), update).ToList();
            }
            foreach (var update in updates)
                harmony.Patch(update, transpiler: new HarmonyMethod(typeof(FriendlyFireDebuffBalanceHooks), nameof(Rewrite)));
        }

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var site = Sites.SingleOrDefault(s => s.Type == __originalMethod.DeclaringType);
            if (site.Type == null) throw new InvalidOperationException("Unknown debuff timer contract.");
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var tick = AccessTools.DeclaredField(site.Type, site.Field);
            var delta = AccessTools.PropertyGetter(typeof(Time), "deltaTime");
            var update = AccessTools.DeclaredMethod(typeof(Timer), "Update", new[] { typeof(float) });
            if (tick == null || tick.FieldType != typeof(Timer) || delta == null || update == null || update.ReturnType != typeof(bool))
                throw new InvalidOperationException("Native debuff timer signature changed.");
            int call = code.FindIndex(i => i.Calls(update));
            int start = call - (site.Speed ? 5 : 3);
            // Wrap only the audited periodic Timer.Update delta. Plasma's
            // separate charge accumulator and all expiry timers stay native.
            if (start < 0 || code.Count(i => i.Calls(update)) != 1 ||
                code.Count(i => i.LoadsField(tick)) != 1 || code.Count(i => i.Calls(delta)) != site.DeltaReads ||
                code[start].opcode != OpCodes.Ldarg_0 || !code[start + 1].LoadsField(tick) ||
                !code[start + 2].Calls(delta) || site.Speed && (!code[call - 2].IsLdloc() || code[call - 1].opcode != OpCodes.Mul) ||
                code.Skip(start + 1).Take(call - start).Any(i => i.labels.Count != 0 || i.blocks.Count != 0))
                throw new InvalidOperationException("Native debuff tick timer contract changed: " + site.Type.Name);
            code.InsertRange(call, new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.DebuffTickDelta))) });
            return code;
        }
    }
}
