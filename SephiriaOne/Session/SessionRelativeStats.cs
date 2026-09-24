using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private sealed class RelativeTarget
        {
            public readonly PlayerAvatar Player;
            public readonly StatDefinition Stat;
            public RelativeTarget(PlayerAvatar player, StatDefinition stat) { Player = player; Stat = stat; }
        }
        private static readonly Dictionary<PlayerAvatar, Dictionary<StatDefinition, RelativeTarget>> relativeStats =
            new Dictionary<PlayerAvatar, Dictionary<StatDefinition, RelativeTarget>>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly ReconciliationCoordinator<RelativeTarget> relative = CreateRelativeCoordinator();

        private static ReconciliationCoordinator<RelativeTarget> CreateRelativeCoordinator()
        {
            var result = new ReconciliationCoordinator<RelativeTarget>();
            result.Register(new ReconciliationRule<RelativeTarget>("relative-stat", SyncDomain.Stats, SyncDomain.None,
                ReconcileMode.OnChange, target => subjects.TryGetValue(target.Player, out HostPlayer subject) && subject.IsReady,
                target => CaptureStat(target.Player, target.Stat), ApplyRelative));
            return result;
        }

        private static StatSnapshot CaptureStat(PlayerAvatar player, StatDefinition stat)
        {
            player.customStats.TryGetValue(stat.Key, out int raw);
            player.customStats.TryGetValue(stat.Marker, out int contribution);
            player.calculatedBonusStats.TryGetValue(stat.Key, out int bonus);
            player.customStatsAmp.TryGetValue(stat.Key, out int amplifier);
            return new StatSnapshot(stat, raw, contribution, bonus, amplifier);
        }

        private static void TrackRelativeStats(PlayerAvatar player, StatDefinition selected)
        {
            if (!relativeStats.TryGetValue(player, out var targets))
            { targets = new Dictionary<StatDefinition, RelativeTarget>(); relativeStats.Add(player, targets); }
            foreach (StatDefinition stat in StatCatalog.All)
            {
                if (selected != null && selected != stat) continue;
                if (targets.TryGetValue(stat, out RelativeTarget previous)) relative.Forget(previous);
                if (policy.IsRelativeStat(stat)) targets[stat] = new RelativeTarget(player, stat);
                else targets.Remove(stat);
            }
            if (targets.Count == 0) relativeStats.Remove(player);
        }

        private static void ForgetRelativeStats(PlayerAvatar player)
        {
            if (!relativeStats.TryGetValue(player, out var targets)) return;
            foreach (var target in targets.Values) relative.Forget(target);
            relativeStats.Remove(player);
        }

        private static bool MaintainRelativeStats(PlayerAvatar player)
        {
            if (!relativeStats.TryGetValue(player, out var targets)) return true;
            bool fresh = true;
            foreach (var target in targets.Values)
            { fresh &= relative.Reconcile(target); if (failedBatch != null) return false; }
            return fresh;
        }

        private static ReconcileResult ApplyRelative(RelativeTarget target)
        {
            PlayerAvatar player = target.Player;
            StatDefinition stat = target.Stat;
            StatSnapshot snapshot = CaptureStat(player, stat);
            bool success = policy.TryPlanRelativeStat(snapshot, out StatUpdate update, out string error);
            string recovery = "";
            if (!success)
            {
                if (StatPlanner.TryPlan(new StatCommand(stat, StatOperation.Reset, 0), new[] { snapshot },
                    out StatUpdate[] reset, out _))
                { update = reset[0]; recovery = "The addon contribution was removed."; }
                else
                { update = new StatUpdate(snapshot.Raw, snapshot.Contribution); recovery = "Native baseline could not be restored; current values were left unchanged."; }
            }
            var context = new HostCommandContext(dungeon, new[] { subjects[player] });
            StateWriteBatch batch = context.CreateBatch();
            NativeStateWrites.Stat(batch, player, stat.Key, stat.Marker, update.Raw, update.Contribution);
            if (!batch.TryCommit(out string writeError))
            {
                if (batch.MayHaveWritten) RecordFault("stats", batch, writeError);
                return ReconcileResult.Faulted(writeError);
            }
            if (success) return ReconcileResult.Applied();
            if (!IsRelativeStatSuspended(player, stat))
                Report($"Relative {stat.Name} setting suspended for player {player.netId}: {error.Replace(" Nobody was changed.", "")} {recovery} It will retry when native stat inputs change.", false);
            return ReconcileResult.Suspended(error + " " + recovery);
        }

        private static bool IsRelativeStatSuspended(PlayerAvatar player, StatDefinition stat)
        {
            if (!relativeStats.TryGetValue(player, out var targets) || !targets.TryGetValue(stat, out var target)) return false;
            foreach (var status in relative.Describe(target)) if (status.State == ReconcileState.Suspended) return true;
            return false;
        }
    }
}
