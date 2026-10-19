using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private sealed class BalanceActor
        {
            internal long Life;
            internal readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow> Targets =
                new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow>();
        }

        internal sealed class BalanceWindow
        {
            internal bool Started;
            internal long CombatEpoch, ParticipantEpoch, Revision, SourceLife, TargetLife, Generation, RoleRevision;
            internal string SourceFloor, TargetFloor;
            internal FriendlyFireDamageBudget Budget;
            internal FriendlyFireHitFrequency Frequency;
            internal long FrequencyRevision;
            internal DebuffPulse Shock, Freeze, Chim;
        }

        private static ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor> balanceActors =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>();
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>.CreateValueCallback createBalanceActor =
            NewBalanceActor;
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow>.CreateValueCallback createBalanceWindow =
            NewBalanceWindow;
        private static BalanceActor NewBalanceActor(FriendlyFireParticipants.Receipt _) => new BalanceActor();
        private static BalanceWindow NewBalanceWindow(FriendlyFireParticipants.Receipt _) => new BalanceWindow();

        // Strike admission can precede damage-budget creation (or emit no hit).
        // Register both actors here so revival also retires those strike windows.
        private static long BalanceLife(FriendlyFireParticipants.Receipt receipt) => balanceActors.GetValue(receipt, createBalanceActor).Life;

        private static void ClearBalanceBudgets() => balanceActors =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>();

        // A dead avatar cannot deal new attacks before revival succeeds. Advance
        // its life here so immediate native/manual revivals do not inherit debt.
        private static void ResetBalanceLife(PlayerAvatar player)
        {
            var receipt = FriendlyFireParticipants.Capture(player);
            if (receipt != null && balanceActors.TryGetValue(receipt, out BalanceActor actor)) actor.Life++;
        }

        private static double LimitBalancedDamage(double damage, double maxHp, double refillPerSecond, double baseRefill, double incomingScale, bool hitFrequency, PvpDotSettings intervals)
        {
            var attacker = current.AttackerReceipt;
            var victim = current.VictimReceipt;
            if (attacker == null || victim == null) return 0;
            var source = balanceActors.GetValue(attacker, createBalanceActor);
            var target = balanceActors.GetValue(victim, createBalanceActor);
            var window = source.Targets.GetValue(victim, createBalanceWindow);
            long epoch = FriendlyFireKda.Epoch, participants = FriendlyFireParticipants.Epoch;
            long revision = SessionSettings.CombatBalanceRevision;
            string sourceFloor = attacker.Player.currentFloorGuid, targetFloor = victim.Player.currentFloorGuid;
            if (!window.Started || window.CombatEpoch != epoch || window.ParticipantEpoch != participants ||
                window.Revision != revision || window.SourceLife != source.Life || window.TargetLife != target.Life ||
                window.SourceFloor != sourceFloor || window.TargetFloor != targetFloor)
            {
                window.Started = true; window.Budget = default; window.Generation++;
                window.Shock = window.Freeze = window.Chim = default;
                window.Frequency = default;
                window.CombatEpoch = epoch; window.ParticipantEpoch = participants; window.Revision = revision;
                window.SourceLife = source.Life; window.TargetLife = target.Life;
                window.SourceFloor = sourceFloor; window.TargetFloor = targetFloor;
            }
            // Reserve before later native callbacks; nested procs and companions
            // consume the owner's same directed budget, regardless of damage ID.
            float now = Time.time;
            int pulse = DebuffPulseKind(current.Damage);
            if (pulse == 1 && !window.Shock.Ready(now, intervals.Interval(PvpDotEffect.Lightning)) ||
                pulse == 2 && !window.Freeze.Ready(now, intervals.Interval(PvpDotEffect.Freeze)) ||
                pulse == 3 && !window.Chim.Ready(now, intervals.Interval(PvpDotEffect.Chim))) return 0;
            if (hitFrequency && damage > 0)
            {
                long frequencyRevision = SessionSettings.CombatFrequencyRevision;
                if (window.FrequencyRevision != frequencyRevision)
                { window.Frequency = default; window.FrequencyRevision = frequencyRevision; }
                // Count before callbacks and before budget admission, so nested
                // attacks and exhausted-budget barrages share the same history.
                damage *= window.Frequency.Next(now);
            }
            long chaserRevision = CombatMatchRuntime.RoleRevision;
            // A carrier can change away and back between two hits. Preserve tokens,
            // but do not award boosted credit across an unobserved role/config edit.
            double elapsedCeiling = window.RoleRevision == chaserRevision ? double.PositiveInfinity : baseRefill;
            // Reserve the final one-point minimum before native callbacks. Role
            // reduction uses pre-reduction units, matching the refund ledger.
            double allowed = window.Budget.Take(damage, maxHp, now, refillPerSecond, elapsedCeiling, 1d / incomingScale);
            window.RoleRevision = chaserRevision;
            if (allowed > 0)
            {
                // Reserve before native callbacks; zero-charge or exhausted
                // hits must not consume the next available pulse. The existing
                // receipt/life/epoch/floor reset owns both clocks and tokens.
                if (pulse == 1) window.Shock.Reserve(now);
                else if (pulse == 2) window.Freeze.Reserve(now);
                else if (pulse == 3) window.Chim.Reserve(now);
                current.BudgetWindow = window; current.BudgetGeneration = window.Generation;
                current.BudgetReserved = allowed; current.BudgetMaxHp = maxHp;
                current.BudgetIncomingScale = incomingScale;
            }
            return allowed;
        }

        private static void FinishBalancedDamage()
        {
            var window = current.BudgetWindow;
            if (window == null) return;
            // Tokens use pre-chaser-reduction damage units. Refund only genuine
            // native losses (MP rounding, veto, armor, overkill), not the intended
            // chaser reduction; otherwise a rapid barrage would refund it away.
            double unused = current.BudgetReserved - current.BudgetAccounted / current.BudgetIncomingScale;
            // The usual fully delivered hit needs no additional lifecycle query.
            // Native damage is float precision. A double-to-float residue is not
            // real unused credit and must not seed another one-point minimum hit.
            if (unused <= current.BudgetReserved * 1e-7d || current.BudgetGeneration != window.Generation || !CurrentHitPermitted()) return;
            // Native shields and HP report their own applied amounts. Preserve
            // fractional credit lost to MP integer rounding or a later veto.
            // Never refund an exceptional hit: it may already have written HP.
            window.Budget.Refund(unused / current.BudgetMaxHp);
        }
    }
}
