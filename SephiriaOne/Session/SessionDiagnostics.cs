using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static IReadOnlyList<string> DescribeDisconnect(uint id)
        {
            var lines = new List<string>
            {
                $"sync epoch={epoch}; run={runGeneration}; intent={intentRevision}; processing={synchronizing || applyingCommand}; " +
                $"fault={failedFeature ?? "none"}; trackedPlayers={subjects.Count}",
                "active settings: " + string.Join("; ", policy.DescribeSettings())
            };
            if (failedReason != null) lines.Add("last fault: " + failedReason);
            // Read stored observations and raw fields only. Never call Prepare,
            // Synchronize, resource capture, file reads, or native stat callbacks.
            if (id == 0) return lines;
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || spawner.PlayerAvatar.netId != id) continue;
                PlayerAvatar player = spawner.PlayerAvatar;
                if (subjects.TryGetValue(player, out HostPlayer subject))
                    DescribeRules(lines, "Player #" + id, players.Describe(subject));
                else lines.Add("Player #" + id + " has not been enrolled.");
                if (relativeStats.TryGetValue(player, out var targets))
                    foreach (var target in targets.Values)
                        DescribeRules(lines, "Player #" + id + " " + target.Stat.Name, relative.Describe(target));
                if (player.Inventory)
                    lines.Add($"native slots={player.Inventory.CurrentInventoryStorage}; fountain={player.Inventory.dimensionPocket}; talents={player.maxPassivePoint}");
                foreach (StatDefinition stat in StatCatalog.All) DescribeRawStat(lines, player, stat.Key, stat.Marker);
                foreach (string key in ChoiceCommand.Keys) DescribeRawStat(lines, player, key, "SEPHIRIAONE_" + key);
                DescribeRawStat(lines, player, "FRUITCOUNT", ResourceCatalog.Get(ResourceKind.Fruit).Marker);
                break;
            }
            return lines;
        }

        private static void DescribeRawStat(List<string> lines, PlayerAvatar player, string key, string marker)
        {
            player.customStats.TryGetValue(key, out int raw);
            player.customStats.TryGetValue(marker, out int owned);
            player.calculatedBonusStats.TryGetValue(key, out int bonus);
            player.customStatsAmp.TryGetValue(key, out int amplifier);
            lines.Add($"{key}: raw={raw}; addon={owned}; bonus={bonus}; amp={amplifier}");
        }

        private static void DescribeSynchronization(List<string> lines, bool sameSession)
        {
            lines.Add($"Synchronization: epoch {epoch}, run {runGeneration}, intent revision {intentRevision}; " +
                (sameSession ? "host scope active." : "awaiting host scope."));
            lines.Add("Native read guards: Fountain=" + SessionBoundaryFeature.Available + ", choices=" + ChoiceFeature.Available + ".");
            if (sameSession)
            {
                foreach (var subject in subjects.Values)
                    DescribeRules(lines, "Player #" + subject.Id, players.Describe(subject));
                foreach (var entry in relativeStats)
                    foreach (var target in entry.Value.Values)
                        DescribeRules(lines, "Player #" + entry.Key.netId + " " + target.Stat.Name, relative.Describe(target));
                DescribeRules(lines, "Session", session.Describe(dungeon));
                foreach (var boundary in boundaries)
                    lines.Add(boundary.Key + ": last boundary " + (boundary.Value ? "fresh." : "unavailable; native behavior continued."));
                if (failedBatch != null)
                {
                    lines.Add("Faulted " + failedFeature + ": " + failedReason);
                    lines.Add("Write journal: " + failedBatch.Describe());
                }
                else if (failedReason != null) lines.Add(failedReason);
            }
            lines.Add("Native name synchronization: " + MultiplayerNameController.Diagnostics + ".");
            lines.Add("Native replication carries gameplay state; peer delivery/rendering is not acknowledged by this status.");
        }

        private static void DescribeRules(List<string> lines, string subject, IReadOnlyList<ReconciliationStatus> states)
        {
            foreach (var state in states)
                lines.Add(subject + " / " + state.Id + ": " + state.State + " (revision " + state.Revision + ")" +
                    (string.IsNullOrEmpty(state.Detail) ? "." : ": " + state.Detail));
        }
    }
}
