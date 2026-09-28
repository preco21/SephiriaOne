using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    public sealed class SessionSettingsController : MonoBehaviour
    {
        private void OnEnable() => SessionSettings.Start();
        private void LateUpdate() => SessionSettings.Synchronize();
        private void OnDisable() => SessionSettings.Stop();
    }

    internal static partial class SessionSettings
    {
        private static SessionPolicy policy = new SessionPolicy();
        private static readonly Dictionary<PlayerAvatar, HostPlayer> subjects =
            new Dictionary<PlayerAvatar, HostPlayer>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly ReconciliationCoordinator<HostPlayer> players = CreatePlayerCoordinator();
        private static readonly ReconciliationCoordinator<DungeonManager> session = CreateSessionCoordinator();
        private static DungeonManager dungeon;
        private static bool enabled, synchronizing, applyingCommand, restoreFountainLimit;
        private static long epoch, runGeneration, intentRevision;
        private static StateWriteBatch failedBatch;
        private static string failedFeature, failedReason;
        private static Action recovered;
        private static bool criticalFresh = true;
        private static readonly Dictionary<string, bool> boundaries = new Dictionary<string, bool>();
        // Synchronize's reentrancy guard gives one owner to these scratch buffers.
        private static readonly HashSet<PlayerAvatar> activePlayers = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly List<HostPlayer> departedPlayers = new List<HostPlayer>();

        private static ReconciliationCoordinator<HostPlayer> CreatePlayerCoordinator()
        {
            var result = new ReconciliationCoordinator<HostPlayer>();
            result.Register(ReconciliationRule<HostPlayer>.ObserveValue("session-inheritance", SyncDomain.Identity,
                SyncDomain.Stats | SyncDomain.Choices | SyncDomain.Fountain | SyncDomain.Resources, ReconcileMode.Once,
                subject => subject.IsReady, subject => subject.Id, Inherit));
            result.Register(ReconciliationRule<HostPlayer>.ObserveValue("resources", SyncDomain.Resources,
                SyncDomain.Resources, ReconcileMode.OnChange, subject => subject.IsReady,
                subject => ResourceRuntime.Observe(subject.Player), ResourceRuntime.Maintain));
            result.Register(ReconciliationRule<HostPlayer>.ObserveValue("fountain-multiplier", SyncDomain.Fountain,
                SyncDomain.Fountain, ReconcileMode.OnChange, subject => subject.IsReady,
                subject => (policy.HasFountainMultiplier, CaptureFountain(subject.Player)), MaintainFountainMultiplier));
            result.Register(ReconciliationRule<HostPlayer>.ObserveValue("fountain-capacity", SyncDomain.Fountain,
                SyncDomain.Limits, ReconcileMode.OnChange, subject => subject.IsReady,
                subject => CaptureFountain(subject.Player), subject =>
                {
                    if (IsFountainEnrolled(subject.Player))
                    { restoreFountainLimit = true; session.Invalidate(dungeon, SyncDomain.Limits); }
                    return ReconcileResult.Applied();
                }));
            return result;
        }

        private static ReconciliationCoordinator<DungeonManager> CreateSessionCoordinator()
        {
            var result = new ReconciliationCoordinator<DungeonManager>();
            result.Register(ReconciliationRule<DungeonManager>.ObserveValue("fountain-carryover", SyncDomain.Limits,
                SyncDomain.None, ReconcileMode.OnChange,
                current => enabled && NetworkServer.active && current && ReferenceEquals(current, dungeon),
                current => (runGeneration, restoreFountainLimit), current =>
                {
                    if (!restoreFountainLimit) return ReconcileResult.Applied();
                    ReconcileResult outcome = FountainPoints.RestoreCarryoverLimit(current, fountainPlayers);
                    if (outcome.State == ReconcileState.Applied) restoreFountainLimit = false;
                    return outcome;
                }));
            return result;
        }

        public static void Start()
        {
            Stop();
            store = new PresetStore(System.IO.Path.Combine(Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
            InvalidateSavedPresetSnapshot();
            enabled = true;
            HorayModAPI.OnStartSessionServerside += OnStartSession;
        }

        private static void ClearScope()
        {
            MerchantFeature.Clear();
            ChoicePoints.ClearCleanup();
            policy.Clear();
            relativeStats.Clear(); relative.Clear();
            ResourceRuntime.Clear();
            fountainPlayers.Clear(); subjects.Clear(); players.Clear(); session.Clear();
            restoreFountainLimit = false; runGeneration = 0; intentRevision = 0;
            failedBatch = null; failedFeature = null; failedReason = null; recovered = null; criticalFresh = true;
            boundaries.Clear();
            NotifySettingsChanged();
        }

        public static void Stop()
        {
            enabled = false;
            HorayModAPI.OnStartSessionServerside -= OnStartSession;
            ClearScope(); dungeon = null; store = null;
            InvalidateSavedPresetSnapshot();
        }

        private static void OnStartSession(bool isSaved)
        {
            if (enabled && NetworkServer.active && dungeon && ReferenceEquals(dungeon, DungeonManager.Instance))
            { runGeneration++; restoreFountainLimit = true; session.Invalidate(dungeon, SyncDomain.Limits); }
        }

        public static bool IsReady(PlayerSpawner spawner) => HostStateAdapter.IsReady(spawner);

        public static bool Prepare(out string message)
        {
            if (!Synchronize())
            { message = L.T("Host synchronization is not ready or is already processing. Retry after entering town or a run."); return false; }
            message = ""; return true;
        }

        public static bool PrepareCommand(string feature, bool reset, out HostCommandContext context, out string message, bool fullReset = true)
        {
            context = null;
            if (!Prepare(out message)) return false;
            if (failedBatch != null)
            {
                if (!reset || (feature != failedFeature && failedFeature != "inheritance"))
                { message = L.F("A {0} write is faulted. Use /{1} reset to recover, or end this session. {2}", failedFeature, failedFeature == "inheritance" ? "stats" : failedFeature, failedReason); return false; }
                if (!fullReset && failedFeature != "inheritance")
                { message = L.F("Partial {0} writes require /{0} reset across the whole command family; a selective reset cannot recover uncommitted settings.", failedFeature); return false; }
                if (!Recover(failedBatch, out message)) return false;
                if (!Synchronize()) { message = L.T("Synchronization is not ready after recovery."); return false; }
            }
            return HostStateAdapter.TryCollect(out context, out message);
        }

        public static bool Commit(string feature, StateWriteBatch batch, Action remember, out string message)
        {
            if (failedBatch != null || applyingCommand || synchronizing)
            { message = L.T("Another state write is faulted or still processing. Inspect /one status before retrying."); return false; }
            applyingCommand = true;
            bool committed = false;
            try
            {
                if (!batch.TryCommit(out message))
                {
                    if (batch.MayHaveWritten) RecordFault(feature, batch, message);
                    return false;
                }
                remember(); intentRevision++;
                committed = true;
                return true;
            }
            finally
            {
                applyingCommand = false;
                if (committed) NotifySettingsChanged();
            }
        }

        internal static bool Recover(StateWriteBatch batch, out string message)
        {
            applyingCommand = true;
            try
            {
                if (!batch.TryRecover(out message)) return false;
                if (ReferenceEquals(batch, failedBatch))
                {
                    recovered?.Invoke(); recovered = null;
                    failedBatch = null; failedReason = null; failedFeature = null;
                    if (dungeon) session.Invalidate(dungeon, SyncDomain.Limits);
                }
                return true;
            }
            finally { applyingCommand = false; }
        }

        internal static void RecordFault(string feature, StateWriteBatch batch, string message, Action onRecovered = null)
        {
            failedFeature = feature; failedBatch = batch; failedReason = message;
            recovered = onRecovered;
            Report(message + L.T(" Maintenance is paused; use reset for explicit recovery."), false);
        }

        internal static ResourcePolicy ResourcePolicy => policy.Resources;
        internal static bool ResourceWritesBlocked => failedBatch != null || applyingCommand;
        internal static long ResourceGeneration => runGeneration;
        internal static bool EnsureResourceScope()
        {
            DungeonManager current = enabled && NetworkServer.active ? DungeonManager.Instance : null;
            if (!current || !current.isServer || current.netId == 0) current = null;
            if (!ReferenceEquals(dungeon, current))
            {
                ClearScope(); dungeon = current; epoch++;
                if (current) LoadPreset();
            }
            return dungeon;
        }

        public static bool Synchronize()
        {
            if (synchronizing || applyingCommand) return false;
            if (!EnsureResourceScope()) return false;
            if (failedBatch != null) return true;
            synchronizing = true;
            try
            {
                bool playersFresh = true;
                var active = activePlayers;
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                {
                    if (!spawner || !spawner.isServer || !spawner.PlayerAvatar) continue;
                    PlayerAvatar player = spawner.PlayerAvatar;
                    if (!active.Add(player)) continue;
                    if (!subjects.TryGetValue(player, out HostPlayer subject) || subject.Id != player.netId ||
                        !ReferenceEquals(subject.Spawner, spawner))
                    {
                        if (subject != null) Forget(subject);
                        subject = new HostPlayer(spawner); subjects[player] = subject;
                    }
                    playersFresh &= players.Reconcile(subject);
                    if (failedBatch != null) break;
                    if (subject.IsReady) playersFresh &= MaintainRelativeStats(player);
                    if (failedBatch != null) break;
                }
                var departed = departedPlayers;
                foreach (var subject in subjects.Values) if (!active.Contains(subject.Player)) departed.Add(subject);
                if (failedBatch == null)
                    foreach (HostPlayer subject in departed) { Forget(subject); subjects.Remove(subject.Player); }
                if (failedBatch == null) criticalFresh = session.Reconcile(dungeon) && playersFresh;
                return true;
            }
            finally { activePlayers.Clear(); departedPlayers.Clear(); synchronizing = false; }
        }

        public static bool EnsureFresh() => Synchronize() && failedBatch == null && criticalFresh;

        public static void BeforeNativeRead(string consumer)
        {
            if (!enabled || !NetworkServer.active) return;
            bool fresh;
            try { fresh = EnsureFresh(); }
            catch (Exception error)
            {
                fresh = false;
                failedReason = L.T("Critical synchronization exception: ") + error.Message;
            }
            if (!fresh && (!boundaries.TryGetValue(consumer, out bool previous) || previous))
                Report(L.F("{0} could not establish fresh addon state; native behavior continues. Inspect /one status.", L.T(consumer)), false);
            boundaries[consumer] = fresh;
        }

        private static void Forget(HostPlayer subject)
        {
            players.Forget(subject);
            ForgetRelativeStats(subject.Player);
            ResourceRuntime.Forget(subject.Player);
            fountainPlayers.Remove(subject.Player);
        }

        private static ReconcileResult Inherit(HostPlayer subject)
        {
            if (!policy.HasChanges && !policy.Resources.HasIntent) return ReconcileResult.Applied(L.T("No retained settings."));
            PlayerAvatar player = subject.Player;
            SessionPlayerSnapshot snapshot = Capture(player);
            if (!policy.TryPlan(snapshot, out SessionPlan plan, out string error))
            {
                Report(L.F("Could not inherit session settings for player {0}: {1}", player.netId, error), false);
                return ReconcileResult.Rejected(error);
            }
            var context = new HostCommandContext(dungeon, new[] { subject });
            StateWriteBatch batch = context.CreateBatch();
            if (plan.Fountain != null) NativeStateWrites.Fountain(batch, dungeon, new[] { player }, plan.Fountain);
            foreach (SessionStatWrite write in plan.Stats)
                NativeStateWrites.Stat(batch, player, write.Key, write.Marker, write.Raw, write.Contribution);
            if (!ResourceRuntime.TryAppend(player, batch, out Action rememberResources, out error))
                return ResourceRuntime.HasRestored(player) ?
                    ReconcileResult.Waiting(error + L.T(" Restored inventory/talent data retained; inheritance will retry when safe.")) :
                    ReconcileResult.Rejected(error);
            if (!batch.TryCommit(out error))
            {
                if (batch.MayHaveWritten)
                {
                    RecordFault("inheritance", batch, error);
                    recovered = () =>
                    {
                        if (plan.Fountain != null) fountainPlayers.Add(player);
                        TrackRelativeStats(player, null);
                        rememberResources();
                        players.AcceptObservation(subject, "session-inheritance");
                    };
                    return ReconcileResult.Faulted(error);
                }
                return ReconcileResult.Waiting(error);
            }
            if (plan.Fountain != null) fountainPlayers.Add(player);
            TrackRelativeStats(player, null);
            rememberResources();
            Report(L.F("Applied active session settings to joining player {0}.", player.netId), true);
            return ReconcileResult.Applied();
        }

        private static SessionPlayerSnapshot Capture(PlayerAvatar player)
        {
            player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution);
            int? limit = dungeon.constValueDictionary.TryGetValue(FountainPoints.LimitKey, out int current) ? current : (int?)null;
            int? original = dungeon.constValueDictionary.TryGetValue(FountainPoints.OriginalLimitKey, out int first) ? first : (int?)null;
            int? applied = dungeon.constValueDictionary.TryGetValue(FountainPoints.AppliedLimitKey, out int last) ? last : (int?)null;
            return new SessionPlayerSnapshot(new Dictionary<string, int>(player.customStats),
                new Dictionary<string, int>(player.calculatedBonusStats), new Dictionary<string, int>(player.customStatsAmp),
                player.Inventory.dimensionPocket, contribution, limit, original, applied, ChoiceFeature.Available);
        }

        public static void Remember(FountainCommand command)
        {
            policy.Record(command);
            session.Invalidate(dungeon, SyncDomain.Limits);
            foreach (HostPlayer subject in subjects.Values)
            {
                if (!subject.IsReady) continue;
                if (policy.HasFountainSetting) fountainPlayers.Add(subject.Player);
                else fountainPlayers.Remove(subject.Player);
                players.AcceptObservation(subject, "fountain-multiplier");
                players.AcceptObservation(subject, "fountain-capacity");
            }
        }
        public static bool TryPlanStats(StatCommand command, IReadOnlyList<StatSnapshot> values,
            out StatUpdate[] updates, out string error) => policy.TryPlanStatCommand(command, values, out updates, out error);
        public static bool TryPlanFountain(FountainCommand command, IReadOnlyList<int> balances, IReadOnlyList<int> contributions,
            int limit, int? original, int? applied, out FountainPlan plan, out string error) =>
            policy.TryPlanFountainCommand(command, balances, contributions, limit, original, applied, out plan, out error);
        public static void Remember(StatCommand command, IReadOnlyList<PlayerAvatar> participants)
        { policy.Record(command); foreach (PlayerAvatar player in participants) TrackRelativeStats(player, command.Stat); }
        public static void RememberChoice(string key, int contribution) => policy.RecordChoice(key, contribution);

        private static void Report(string message, bool success)
        {
            string text = "[SephiriaOne] " + message;
            if (success) Debug.Log(text); else Debug.LogWarning(text);
            if (GameLogWriter.Instance) GameLogWriter.Instance.WriteLog(text, success ? Color.green : Color.yellow);
        }
    }
}
