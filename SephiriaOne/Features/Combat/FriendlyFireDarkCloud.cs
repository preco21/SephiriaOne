using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private sealed class DarkCloudTargets
        {
            internal readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudWindow> Windows =
                new ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudWindow>();
        }
        private sealed class DarkCloudWindow
        {
            internal bool Started;
            internal long CombatEpoch, ParticipantEpoch, BalanceRevision, SourceLife, TargetLife;
            internal string SourceFloor, TargetFloor;
            internal float LastStrike;
        }
        private static ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudTargets> darkCloudWindows =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudTargets>();
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudTargets>.CreateValueCallback createCloudTargets =
            NewCloudTargets;
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudWindow>.CreateValueCallback createCloudWindow =
            NewCloudWindow;
        private static DarkCloudTargets NewCloudTargets(FriendlyFireParticipants.Receipt _) => new DarkCloudTargets();
        private static DarkCloudWindow NewCloudWindow(FriendlyFireParticipants.Receipt _) => new DarkCloudWindow();
        private static void ClearDarkCloudWindows() => darkCloudWindows =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, DarkCloudTargets>();

        // Throttle the native strike, not just ApplyDamage: OnAttackUnit also
        // applies Blazing Stormcloud's burn. Native caller costs/timers stay intact.
        internal static bool BeforeDarkCloudStrike(ComboEffect_DarkCloud __instance, UnitAvatar target)
        {
            if (!NetworkServer.active || !__instance.isPlayerCombo ||
                !(__instance.Networkavatar is PlayerAvatar source) || !(target is PlayerAvatar victim) ||
                !source || !victim || source == victim || victim.IsDead) return true;
            // Native multishot coroutines can outlive their owner; queued strikes
            // keep the same limit after death as long as that connection is current.
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Enabled || !settings.Balance || !settings.HasDamage || !CanAffectTeam(source, victim)) return true;

            var attacker = FriendlyFireParticipants.Capture(source);
            var defender = FriendlyFireParticipants.Capture(victim);
            if (attacker == null || defender == null) return true; // Central impact guards still reject stale actors.
            long participantEpoch = FriendlyFireParticipants.Epoch;
            // Align with BeforeHit before capturing its epoch, including a first
            // strike after inherited settings. This does not change score totals
            // when already enabled; it prevents that first hit bypassing the limit.
            FriendlyFireKda.SetEnabled(CombatMatchRuntime.ScoresEnabled(settings.Enabled));
            long combatEpoch = FriendlyFireKda.Epoch;
            var window = darkCloudWindows.GetValue(attacker, createCloudTargets).Windows.GetValue(defender, createCloudWindow);
            float now = Time.time;
            long balanceRevision = SessionSettings.CombatBalanceRevision;
            long sourceLife = BalanceLife(attacker), targetLife = BalanceLife(defender);
            if (window.Started && window.CombatEpoch == combatEpoch && window.ParticipantEpoch == participantEpoch && window.BalanceRevision == balanceRevision &&
                window.SourceLife == sourceLife && window.TargetLife == targetLife &&
                window.SourceFloor == source.currentFloorGuid && window.TargetFloor == victim.currentFloorGuid &&
                now >= window.LastStrike && now - window.LastStrike < settings.DotIntervals.Interval(PvpDotEffect.DarkCloud)) return false;

            // Reserve before native callbacks: recursive strikes and another
            // cloud object owned by this player share the same pair's window.
            window.Started = true; window.CombatEpoch = combatEpoch; window.ParticipantEpoch = participantEpoch;
            window.BalanceRevision = balanceRevision;
            window.SourceLife = sourceLife; window.TargetLife = targetLife;
            window.SourceFloor = source.currentFloorGuid; window.TargetFloor = victim.currentFloorGuid; window.LastStrike = now;
            return true;
        }
    }
}
