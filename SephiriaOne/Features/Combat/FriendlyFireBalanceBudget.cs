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
            internal long CombatEpoch, ParticipantEpoch, Revision, SourceLife, TargetLife, Generation;
            internal string SourceFloor, TargetFloor;
            internal FriendlyFireDamageBudget Budget;
        }

        private static ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor> balanceActors =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>();
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>.CreateValueCallback createBalanceActor =
            NewBalanceActor;
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow>.CreateValueCallback createBalanceWindow =
            NewBalanceWindow;
        private static BalanceActor NewBalanceActor(FriendlyFireParticipants.Receipt _) => new BalanceActor();
        private static BalanceWindow NewBalanceWindow(FriendlyFireParticipants.Receipt _) => new BalanceWindow();

        private static void ClearBalanceBudgets() => balanceActors =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>();

        // A dead avatar cannot deal new attacks before revival succeeds. Advance
        // its life here so immediate native/manual revivals do not inherit debt.
        private static void ResetBalanceLife(PlayerAvatar player)
        {
            var receipt = FriendlyFireParticipants.Capture(player);
            if (receipt != null && balanceActors.TryGetValue(receipt, out BalanceActor actor)) actor.Life++;
        }

        private static double LimitBalancedDamage(double damage, double maxHp)
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
                window.CombatEpoch = epoch; window.ParticipantEpoch = participants; window.Revision = revision;
                window.SourceLife = source.Life; window.TargetLife = target.Life;
                window.SourceFloor = sourceFloor; window.TargetFloor = targetFloor;
            }
            // Reserve before later native callbacks; nested procs and companions
            // consume the owner's same directed budget, regardless of damage ID.
            double allowed = window.Budget.Take(damage, maxHp, Time.time);
            if (allowed > 0)
            {
                current.BudgetWindow = window; current.BudgetGeneration = window.Generation;
                current.BudgetReserved = allowed; current.BudgetMaxHp = maxHp;
            }
            return allowed;
        }

        private static void FinishBalancedDamage()
        {
            var window = current.BudgetWindow;
            double unused = current.BudgetReserved - current.BudgetAccounted;
            // The usual fully delivered hit needs no additional lifecycle query.
            if (window == null || unused <= 0 || current.BudgetGeneration != window.Generation || !CurrentHitPermitted()) return;
            // Native shields and HP report their own applied amounts. Preserve
            // fractional credit lost to MP integer rounding or a later veto.
            // Never refund an exceptional hit: it may already have written HP.
            window.Budget.Refund(unused / current.BudgetMaxHp);
        }
    }
}
