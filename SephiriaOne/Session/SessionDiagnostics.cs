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
                    DescribeRules(lines, L.T("Player #") + id, players.Describe(subject));
                else lines.Add(L.T("Player #") + id + " has not been enrolled.");
                if (relativeStats.TryGetValue(player, out var targets))
                    foreach (var target in targets.Values)
                        DescribeRules(lines, L.T("Player #") + id + " " + target.Stat.Name, relative.Describe(target));
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
            lines.Add(L.F("Synchronization: epoch {0}, run {1}, intent revision {2}; ", epoch, runGeneration, intentRevision) +
                (sameSession ? L.T("host scope active.") : L.T("awaiting host scope.")));
            lines.Add(L.T("Native read guards: Fountain=") + L.T(SessionBoundaryFeature.Available ? "True" : "False") +
                L.T(", choices=") + L.T(ChoiceFeature.Available ? "True" : "False") + ".");
            if (sameSession)
            {
                foreach (var subject in subjects.Values)
                    DescribeRules(lines, L.T("Player #") + subject.Id, players.Describe(subject));
                foreach (var entry in relativeStats)
                    foreach (var target in entry.Value.Values)
                        DescribeRules(lines, L.T("Player #") + entry.Key.netId + " " + target.Stat.Name, relative.Describe(target));
                DescribeRules(lines, L.T("Session"), session.Describe(dungeon));
                foreach (var boundary in boundaries)
                    lines.Add(L.T(boundary.Key) + L.T(": last boundary ") + (boundary.Value ? L.T("fresh.") : L.T("unavailable; native behavior continued.")));
                if (failedBatch != null)
                {
                    lines.Add(L.T("Faulted ") + failedFeature + ": " + failedReason);
                    lines.Add(L.T("Write journal: ") + failedBatch.Describe());
                }
                else if (failedReason != null) lines.Add(failedReason);
            }
            lines.Add(L.T("Native name synchronization: ") + MultiplayerNameController.Diagnostics + ".");
            lines.Add(L.T("Native replication carries gameplay state; peer delivery/rendering is not acknowledged by this status."));
        }

        private static void DescribeRules(List<string> lines, string subject, IReadOnlyList<ReconciliationStatus> states)
        {
            foreach (var state in states)
            {
                // Translate the coordinator's two stable readiness labels at read
                // time. Previously captured fault details remain historical text.
                string detail = state.Detail == "Not observed yet." ? L.T("Not observed yet.") :
                    state.Detail == "Required state or authority is not ready." ? L.T("Required state or authority is not ready.") : state.Detail;
                lines.Add(subject + " / " + state.Id + ": " + L.T(state.State.ToString()) + L.T(" (revision ") + state.Revision + ")" +
                    (string.IsNullOrEmpty(detail) ? "." : ": " + detail));
            }
        }
    }
}
