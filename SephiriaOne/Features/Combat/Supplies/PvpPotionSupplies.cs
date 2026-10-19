using System.Collections.Generic;

namespace SephiriaOne
{
    // Optional reconciliation has its own coordinator: an inventory fault must
    // never make the critical stats/Fountain read barrier fail (or vice versa).
    internal static class PvpPotionSupplies
    {
        private static readonly ReconciliationCoordinator<FriendlyFireParticipants.Receipt> coordinator = Create();
        private static readonly HashSet<FriendlyFireParticipants.Receipt> tracked = new HashSet<FriendlyFireParticipants.Receipt>();
        private static readonly HashSet<FriendlyFireParticipants.Receipt> current = new HashSet<FriendlyFireParticipants.Receipt>();
        private static readonly List<FriendlyFireParticipants.Receipt> roster = new List<FriendlyFireParticipants.Receipt>();
        private static readonly List<FriendlyFireParticipants.Receipt> departed = new List<FriendlyFireParticipants.Receipt>();
        private static long revision;
        private static bool ticking;

        private static ReconciliationCoordinator<FriendlyFireParticipants.Receipt> Create()
        {
            var result = new ReconciliationCoordinator<FriendlyFireParticipants.Receipt>();
            result.Register(ReconciliationRule<FriendlyFireParticipants.Receipt>.ObserveValue("pvp-large-potion",
                SyncDomain.Identity, SyncDomain.None, ReconcileMode.OnChange,
                // Readiness belongs to the first delivery attempt, not the rule:
                // a later loading frame must not reset an Applied/Faulted cursor.
                receipt => true,
                receipt => (revision, CombatMatchRuntime.MatchToken, Eligible(receipt)), Apply));
            return result;
        }

        internal static void SettingsChanged() => revision++;
        internal static void Clear()
        {
            coordinator.Clear(); tracked.Clear(); revision++;
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
            if (!Eligible(receipt)) return ReconcileResult.Applied();
            long observedRevision = revision, match = CombatMatchRuntime.MatchToken;
            bool Current() => observedRevision == revision && match == CombatMatchRuntime.MatchToken && Eligible(receipt);
            var result = LargePotionGrant.Give(receipt, Current);
            if (result == PotionGrantResult.Failed) return ReconcileResult.Faulted("Large Potion grant failed; see Player.log.");
            if (result == PotionGrantResult.Waiting || !Current()) return ReconcileResult.Waiting("Waiting for PvP inventory readiness.");
            return ReconcileResult.Applied();
        }

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
                foreach (var receipt in departed) { coordinator.Forget(receipt); tracked.Remove(receipt); }
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
