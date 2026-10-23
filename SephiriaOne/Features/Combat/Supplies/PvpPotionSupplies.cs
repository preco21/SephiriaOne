using System.Collections.Generic;

namespace SephiriaOne
{
    // Optional reconciliation has its own coordinator: an inventory fault must
    // never make the critical stats/Fountain read barrier fail (or vice versa).
    internal static class PvpPotionSupplies
    {
        private const string RuleId = "pvp-large-potion";
        private static readonly ReconciliationCoordinator<FriendlyFireParticipants.Receipt> coordinator = Create();
        private static readonly HashSet<FriendlyFireParticipants.Receipt> tracked = new HashSet<FriendlyFireParticipants.Receipt>();
        private static readonly HashSet<FriendlyFireParticipants.Receipt> current = new HashSet<FriendlyFireParticipants.Receipt>();
        private static readonly List<FriendlyFireParticipants.Receipt> roster = new List<FriendlyFireParticipants.Receipt>();
        private static readonly List<FriendlyFireParticipants.Receipt> departed = new List<FriendlyFireParticipants.Receipt>();
        private static readonly Dictionary<FriendlyFireParticipants.Receipt, (long Attempt, long Revision)> revivals =
            new Dictionary<FriendlyFireParticipants.Receipt, (long, long)>();
        private static long revision, nextAttempt;
        private static bool ticking;

        private static ReconciliationCoordinator<FriendlyFireParticipants.Receipt> Create()
        {
            var result = new ReconciliationCoordinator<FriendlyFireParticipants.Receipt>();
            result.Register(ReconciliationRule<FriendlyFireParticipants.Receipt>.ObserveValue(RuleId,
                SyncDomain.Identity, SyncDomain.None, ReconcileMode.OnChange,
                // Readiness belongs to the first delivery attempt, not the rule:
                // a later loading frame must not reset an Applied/Faulted cursor.
                receipt => true,
                receipt => (revision, CombatMatchRuntime.MatchToken, Eligible(receipt)), Apply));
            return result;
        }

        internal static void SettingsChanged() => revision++;

        // Capture before native callbacks; a revive belonging to an old match,
        // option state or connection must not refresh the new session's supply.
        internal static long BeginRevive(FriendlyFireParticipants.Receipt receipt)
        {
            if (!Eligible(receipt)) return -1;
            // A newer revival supersedes any previous life's pending delivery.
            // Native Revive may clear IsDead and then throw: only its successful
            // finalizer can request supplies again, never that partial state.
            long attempt = ++nextAttempt;
            revivals[receipt] = (attempt, revision);
            tracked.Add(receipt);
            coordinator.AcceptObservation(receipt, RuleId);
            return attempt;
        }

        internal static void Revived(FriendlyFireParticipants.Receipt receipt, long attempt, long match)
        {
            if (attempt <= 0 || !Eligible(receipt) || !revivals.TryGetValue(receipt, out var revival) ||
                revival.Attempt != attempt || revival.Revision != revision ||
                match != CombatMatchRuntime.MatchToken || receipt.Player.IsDead ||
                receipt.Player.hp <= 0 || float.IsNaN(receipt.Player.hp) || float.IsInfinity(receipt.Player.hp)) return;
            // Reuse the cursor captured before revival; teardown can retire it.
            // Inventory writes stay outside the native revival callback stack.
            if (tracked.Contains(receipt)) coordinator.Invalidate(receipt, SyncDomain.Identity);
        }

        internal static void Clear()
        {
            coordinator.Clear(); tracked.Clear(); revivals.Clear(); revision++;
            // A native callback can end the session during a grant. The active
            // iterator owns its scratch lists until its finally block.
            if (!ticking) { current.Clear(); roster.Clear(); departed.Clear(); }
        }

        private static bool Eligible(FriendlyFireParticipants.Receipt receipt)
        {
            var settings = SessionSettings.FriendlyFireSupplies;
            if (!settings.AutoPotion || !FriendlyFireFeature.Available || !FriendlyFireParticipants.IsCurrent(receipt)) return false;
            return CombatMatchRuntime.IsRunning ? CombatMatchRuntime.IsSupplyParticipant(receipt.Player) :
                settings.Enabled && FriendlyFireParticipants.Includes(receipt.Player, requireReady: false);
        }

        private static ReconcileResult Apply(FriendlyFireParticipants.Receipt receipt)
        {
            // If death races the deferred check, wait for the next completed
            // revival event, not merely a later IsDead/readiness flag change.
            if (!Eligible(receipt) || receipt.Player.IsDead) return ReconcileResult.Applied();
            long observedRevision = revision, match = CombatMatchRuntime.MatchToken, attempt = Attempt(receipt);
            bool Current() => observedRevision == revision && match == CombatMatchRuntime.MatchToken && Eligible(receipt) &&
                !receipt.Player.IsDead && attempt == Attempt(receipt);
            var result = LargePotionGrant.Give(receipt, Current);
            // A callback may start a newer revival while an old grant is in
            // progress. Acknowledge the cancelled attempt instead of retrying
            // it as Waiting; only the newer successful finalizer can invalidate.
            if (attempt != Attempt(receipt)) return ReconcileResult.Applied();
            if (result == PotionGrantResult.Failed) return ReconcileResult.Faulted("Large Potion grant failed; see Player.log.");
            if (result == PotionGrantResult.Waiting || !Current()) return ReconcileResult.Waiting("Waiting for PvP inventory readiness.");
            return ReconcileResult.Applied();
        }

        private static long Attempt(FriendlyFireParticipants.Receipt receipt) =>
            revivals.TryGetValue(receipt, out var revival) ? revival.Attempt : 0;

        internal static void Tick()
        {
            if (ticking) return;
            ticking = true;
            try
            {
                var settings = SessionSettings.FriendlyFireSupplies;
                if (!settings.AutoPotion || !settings.Enabled && !CombatMatchRuntime.IsRunning)
                { if (tracked.Count != 0) Clear(); return; }
                // No inventory scans on unchanged frames. Receipts, selection and
                // match tokens are shared with all other PvP lifecycle features.
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                {
                    var receipt = spawner ? FriendlyFireParticipants.Capture(spawner.PlayerAvatar) : null;
                    if (receipt != null && current.Add(receipt)) roster.Add(receipt);
                }
                foreach (var receipt in tracked) if (!current.Contains(receipt)) departed.Add(receipt);
                foreach (var receipt in departed) { coordinator.Forget(receipt); tracked.Remove(receipt); revivals.Remove(receipt); }
                long scope = revision;
                foreach (var receipt in roster)
                {
                    if (scope != revision) break;
                    tracked.Add(receipt);
                    coordinator.Reconcile(receipt);
                }
            }
            finally { current.Clear(); roster.Clear(); departed.Clear(); ticking = false; }
        }
    }
}
