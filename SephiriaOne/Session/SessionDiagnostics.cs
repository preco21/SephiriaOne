using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
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
