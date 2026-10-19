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
            internal bool Ready(float now, float interval) => !float.IsNaN(now) && !float.IsInfinity(now) &&
                (!started || now < last || now - last >= interval);
            internal void Reserve(float now) { started = true; last = now; }
        }

        // Shock (including Plasma), Freeze and Chim deal damage on refresh,
        // application or expiry, not a periodic tick. Limit only their resolved
        // damage, never the native caller's stun/stack/expiry work.
        private static int DebuffPulseKind(DamageInstance damage)
        {
            if (damage == null || damage.damageType != EDamageType.ElementalEffectDamage || damage.fromType != EDamageFromType.None) return 0;
            // The shipped Plasma prefab overrides BOTH damage IDs to Debuff_Plasma.
            // Only its shock attack carries this flag; its burn tick must not
            // borrow the Lightning clock despite having the same damage ID.
            if (damage.id == "Debuff_Electric" || damage.flag == "DEBUFF_ELECTRIC") return 1;
            return damage.id == "Debuff_Freeze" ? 2 : damage.id == "Debuff_Chim" ? 3 : 0;
        }

        // Preserve native timer progress/lifetime and Update's reset/activeOnce
        // semantics. No interval writes or retained override need restoration.
        internal static bool UpdateDebuffTick(Timer timer, float elapsed, CharacterDebuff effect, int kind)
        {
            if (!NetworkServer.active ||
                !effect || effect is CharacterDebuff_Poison poison && poison.isSystemDamage ||
                !(effect.NetworkTarget is PlayerAvatar victim) || !victim) return timer.Update(elapsed);
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance) return timer.Update(elapsed);
            var source = effect.NetworkAttacker;
            if (!source || ReferenceEquals(source, victim) || !CanAffectTeam(source, victim)) return timer.Update(elapsed);
            // Burn and Plasma use raw delta so BURNSPEED cannot shorten
            // the configured seconds. Plasma's shock accumulator stays native.
            if (!(timer.time > 0) || float.IsInfinity(timer.time)) return timer.Update(elapsed);
            float delta = Time.deltaTime;
            if (!(delta > 0) || float.IsInfinity(delta)) delta = 0;
            return timer.Update(delta * (timer.time / settings.DotIntervals.Interval((PvpDotEffect)kind)));
        }
    }

    internal static class FriendlyFireDebuffBalanceHooks
    {
        private static readonly (Type Type, string Field, bool Speed, int DeltaReads, int Kind)[] Sites =
        {
            (typeof(CharacterDebuff_Burn), "tickTimer", true, 1, (int)PvpDotEffect.Burn),
            (typeof(CharacterDebuff_Plasma), "tickTimer", true, 2, (int)PvpDotEffect.PlasmaBurn),
            (typeof(CharacterDebuff_Frostbite), "frostDamageTimer", false, 1, (int)PvpDotEffect.Frostbite),
            (typeof(CharacterDebuff_Poison), "poisonTickTimer", false, 1, (int)PvpDotEffect.Poison),
            (typeof(CharacterDebuff_Wound), "tickTimer", false, 1, (int)PvpDotEffect.Wound)
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
            var interval = AccessTools.DeclaredField(typeof(Timer), "time");
            if (tick == null || tick.FieldType != typeof(Timer) || delta == null || update == null || update.ReturnType != typeof(bool) ||
                interval == null || interval.FieldType != typeof(float) || interval.IsStatic || !interval.IsPublic)
                throw new InvalidOperationException("Native debuff timer signature changed.");
            int call = code.FindIndex(i => i.Calls(update));
            int start = call - (site.Speed ? 5 : 3);
            // Wrap only the audited periodic Timer.Update call. Plasma's
            // separate charge accumulator and all expiry timers stay native.
            if (start < 0 || code.Count(i => i.Calls(update)) != 1 ||
                code.Count(i => i.LoadsField(tick)) != 1 || code.Count(i => i.Calls(delta)) != site.DeltaReads ||
                code[start].opcode != OpCodes.Ldarg_0 || !code[start + 1].LoadsField(tick) ||
                !code[start + 2].Calls(delta) || site.Speed && (!code[call - 2].IsLdloc() || code[call - 1].opcode != OpCodes.Mul) ||
                code.Skip(start + 1).Take(call - start).Any(i => i.labels.Count != 0 || i.blocks.Count != 0))
                throw new InvalidOperationException("Native debuff tick timer contract changed: " + site.Type.Name);
            code[call] = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.UpdateDebuffTick));
            code.InsertRange(call, new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldc_I4, site.Kind) });
            return code;
        }
    }
}
