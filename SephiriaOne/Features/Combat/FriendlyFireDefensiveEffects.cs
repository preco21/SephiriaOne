using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private struct EvadeRefundWindow
        {
            internal bool Started;
            internal long CombatEpoch, ParticipantEpoch, Revision, Life;
            internal string Floor;
            internal float LastRefund;
        }

        internal static bool AllowEvadeRestore(Charm_EvasionRestore __instance, DamageInstance instance)
        {
            // Gate this artifact only: absolute-dodge charges and other native
            // OnEvade subscribers must still consume/respond to the same evade.
            var avatar = __instance.NetworkAvatar;
            if (!ReferenceEquals(instance, current.Damage) || !BalancedDefense(avatar)) return true;
            var receipt = current.VictimReceipt;
            if (receipt == null) return true;
            var actor = balanceActors.GetValue(receipt, createBalanceActor);
            ref var window = ref actor.EvadeRefund;
            long epoch = FriendlyFireKda.Epoch, participants = FriendlyFireParticipants.Epoch;
            long revision = SessionSettings.CombatBalanceRevision;
            string floor = receipt.Player.currentFloorGuid;
            float now = Time.time;
            if (window.Started && window.CombatEpoch == epoch && window.ParticipantEpoch == participants &&
                window.Revision == revision && window.Life == actor.Life && window.Floor == floor &&
                now >= window.LastRefund && now - window.LastRefund < 2f) return false;
            // One reservation per defender, across attackers and artifact
            // replacement. Reserve before the native callback can reenter.
            window.Started = true; window.LastRefund = now;
            window.CombatEpoch = epoch; window.ParticipantEpoch = participants;
            window.Revision = revision; window.Life = actor.Life; window.Floor = floor;
            return true;
        }

        internal static int CounterAllowed(int nativeCount)
        {
            if (nativeCount != 1 || !BalancedDefense(current.Victim)) return nativeCount;
            var controller = FriendlyFireDefensiveEffectHooks.Controller((PlayerAvatar)current.Victim);
            // Only shorten a counter whose native producer we can identify.
            // No writes: the same player retains the complete PvE window.
            if (!controller || !ReferenceEquals(controller.unitAvatar, current.Victim) ||
                !FriendlyFireDefensiveEffectHooks.CounterActive(controller)) return nativeCount;
            float elapsed = FriendlyFireDefensiveEffectHooks.CounterElapsed(controller);
            return controller.furyInvincibleTime > 0.35f && elapsed >= 0.35f &&
                !float.IsInfinity(elapsed) ? 0 : nativeCount;
        }
    }

    internal static class FriendlyFireDefensiveEffectHooks
    {
        internal static readonly AccessTools.FieldRef<PlayerAvatar, WeaponControllerSimple> Controller =
            AccessTools.FieldRefAccess<PlayerAvatar, WeaponControllerSimple>("weaponController");
        internal static readonly AccessTools.FieldRef<WeaponControllerSimple, float> CounterElapsed =
            AccessTools.FieldRefAccess<WeaponControllerSimple, float>("furyInvincibleTimer");
        internal static readonly AccessTools.FieldRef<WeaponControllerSimple, bool> CounterActive =
            AccessTools.FieldRefAccess<WeaponControllerSimple, bool>("isFuryInvincibleApplied");

        internal static void Install(Harmony harmony)
        {
            var method = AccessTools.DeclaredMethod(typeof(Charm_EvasionRestore), "HandleEvade", new[] { typeof(DamageInstance) });
            if (method == null || method.IsStatic || method.ReturnType != typeof(void) ||
                !ValidateEvadeRestore(PatchProcessor.GetOriginalInstructions(method)))
                throw new InvalidOperationException("Native evade dash-refund callback changed.");
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AllowEvadeRestore)));
        }

        internal static bool ValidateEvadeRestore(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            bool Method(int index, string name) => code[index].operand is MethodInfo m && m.Name == name &&
                (code[index].opcode == OpCodes.Call || code[index].opcode == OpCodes.Callvirt);
            return code.Count == 11 && code[0].opcode == OpCodes.Ldarg_0 && Method(1, "get_NetworkAvatar") &&
                code[2].LoadsField(AccessTools.DeclaredField(typeof(UnitAvatar), "CurrentDashModule")) &&
                code[3].opcode == OpCodes.Ldarg_0 &&
                code[4].LoadsField(AccessTools.DeclaredField(typeof(Charm_EvasionRestore), "restoreDashCountByLevel")) &&
                code[5].opcode == OpCodes.Ldarg_0 && Method(6, "CurrentLevelToIdx") && code[7].LoadsConstant(0) &&
                Method(8, "SafeRandomAccess") && code[9].Calls(AccessTools.DeclaredMethod(typeof(CharacterDash), "RestoreDashCount", new[] { typeof(int) })) &&
                code[10].opcode == OpCodes.Ret;
        }

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var field = AccessTools.DeclaredField(typeof(UnitAvatar), "isCounterInvincibleApplied");
            var callback = AccessTools.DeclaredField(typeof(UnitAvatar), "OnCounter");
            var reads = Enumerable.Range(0, code.Count).Where(i => code[i].LoadsField(field)).ToArray();
            var callbacks = Enumerable.Range(0, code.Count).Where(i => code[i].LoadsField(callback)).ToArray();
            if (field == null || field.FieldType != typeof(sbyte) || reads.Length != 1 || callbacks.Length != 1)
                throw new InvalidOperationException("Native counter immunity boundary changed.");
            int read = reads[0];
            if (read == 0 || read + 2 >= code.Count || code[read - 1].opcode != OpCodes.Ldarg_0 ||
                !code[read + 1].LoadsConstant(0) ||
                (code[read + 2].opcode != OpCodes.Ble && code[read + 2].opcode != OpCodes.Ble_S) ||
                callbacks[0] <= read + 2)
                throw new InvalidOperationException("Native counter immunity comparison changed.");
            var call = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.CounterAllowed));
            call.MoveLabelsFrom(code[read + 1]); call.MoveBlocksFrom(code[read + 1]);
            code.Insert(read + 1, call);
            return code;
        }
    }
}
