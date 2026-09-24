using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private readonly struct RelativeObservation
        {
            public StatSnapshot Snapshot { get; }
            public bool Suspended { get; }
            public RelativeObservation(StatSnapshot snapshot, bool suspended)
            {
                Snapshot = snapshot;
                Suspended = suspended;
            }

            public bool Matches(StatSnapshot other) => Snapshot.Raw == other.Raw &&
                Snapshot.Contribution == other.Contribution && Snapshot.Bonus == other.Bonus &&
                Snapshot.Amplifier == other.Amplifier;
        }

        private static readonly Dictionary<uint, Dictionary<StatDefinition, RelativeObservation>> relativeStats =
            new Dictionary<uint, Dictionary<StatDefinition, RelativeObservation>>();

        private static StatSnapshot CaptureStat(PlayerAvatar player, StatDefinition stat)
        {
            player.customStats.TryGetValue(stat.Key, out int raw);
            player.customStats.TryGetValue(stat.Marker, out int contribution);
            player.calculatedBonusStats.TryGetValue(stat.Key, out int bonus);
            player.customStatsAmp.TryGetValue(stat.Key, out int amplifier);
            return new StatSnapshot(stat, raw, contribution, bonus, amplifier);
        }

        // Enroll only successfully applied settings. A rejected inheritance stays
        // rejected until an explicit command succeeds for that player and stat.
        private static void TrackRelativeStats(PlayerAvatar player, StatDefinition selected)
        {
            if (!relativeStats.TryGetValue(player.netId, out var observations))
            {
                observations = new Dictionary<StatDefinition, RelativeObservation>();
                relativeStats.Add(player.netId, observations);
            }
            foreach (StatDefinition stat in StatCatalog.All)
            {
                if (selected != null && selected != stat) continue;
                if (policy.IsRelativeStat(stat)) observations[stat] = new RelativeObservation(CaptureStat(player, stat), false);
                else observations.Remove(stat);
            }
            if (observations.Count == 0) relativeStats.Remove(player.netId);
        }

        private static void MaintainRelativeStats(PlayerAvatar player)
        {
            if (!relativeStats.TryGetValue(player.netId, out var observations)) return;
            foreach (StatDefinition stat in StatCatalog.All)
            {
                if (!observations.TryGetValue(stat, out RelativeObservation previous)) continue;
                StatSnapshot snapshot = CaptureStat(player, stat);
                if (previous.Matches(snapshot)) continue;
                bool success = policy.TryPlanRelativeStat(snapshot, out StatUpdate update, out string error);
                string recovery = "";
                if (!success)
                {
                    // Integer native stats cannot express every displayed offset.
                    // Remove our raw contribution while awaiting changed inputs.
                    if (StatPlanner.TryPlan(new StatCommand(stat, StatOperation.Reset, 0), new[] { snapshot },
                        out StatUpdate[] reset, out _))
                    {
                        update = reset[0];
                        recovery = "The addon contribution was removed.";
                    }
                    else
                    {
                        update = new StatUpdate(snapshot.Raw, snapshot.Contribution);
                        recovery = "The native baseline could not be restored; current values were left unchanged.";
                    }
                }
                if (snapshot.Raw != update.Raw) player.customStats[stat.Key] = update.Raw;
                if (snapshot.Contribution != update.Contribution)
                {
                    if (update.Contribution == 0) player.customStats.Remove(stat.Marker);
                    else player.customStats[stat.Marker] = update.Contribution;
                }
                observations[stat] = new RelativeObservation(CaptureStat(player, stat), !success);
                if (!success && !previous.Suspended)
                    Report($"Relative {stat.Name} offset suspended for player {player.netId}: {error.Replace(" Nobody was changed.", "")} {recovery} It will retry when native stat inputs change.", false);
            }
        }

        private static bool IsRelativeStatSuspended(PlayerAvatar player, StatDefinition stat) =>
            relativeStats.TryGetValue(player.netId, out var observations) &&
            observations.TryGetValue(stat, out RelativeObservation observation) && observation.Suspended;
    }
}
