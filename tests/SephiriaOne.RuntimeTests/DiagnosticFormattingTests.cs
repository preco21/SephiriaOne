using System.Reflection;
using System.Text;
using SephiriaOne;

internal static class DiagnosticFormattingTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Exercise the real shared status/disconnect formatter against all native
        // outcomes, including deferred readiness and historical fault details.
        var format = typeof(SessionSettings).GetMethod("DescribeRules", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(object))
            .CreateDelegate<Action<List<string>, string, ReconciliationCoordinator<object>, object, List<ReconciliationStatus>, StringBuilder>>();
        var lines = new List<string>(); var states = new List<ReconciliationStatus>(); var text = new StringBuilder();
        foreach (var result in new[] {
            ReconcileResult.Applied(), ReconcileResult.NativeFallback("historical detail"),
            ReconcileResult.Waiting("Not observed yet."),
            ReconcileResult.Waiting("Required state or authority is not ready."),
            ReconcileResult.Suspended("historical detail"), ReconcileResult.Rejected("historical detail"),
            ReconcileResult.Faulted("historical detail") })
        {
            int captures = 0, writes = 0;
            var subject = new object(); var coordinator = new ReconciliationCoordinator<object>();
            coordinator.Register(ReconciliationRule<object>.ObserveValue("test-rule", SyncDomain.Stats, SyncDomain.None,
                ReconcileMode.Once, _ => true, _ => ++captures, _ => { writes++; return result; }));
            coordinator.Reconcile(subject);
            int priorCaptures = captures, priorWrites = writes;
            lines.Clear(); format(lines, "Player #1", coordinator, subject, states, text);
            string detail = result.Detail == "Not observed yet." || result.Detail == "Required state or authority is not ready."
                ? L.T(result.Detail) : result.Detail;
            string expected = "Player #1 / test-rule: " + L.T(result.State.ToString()) + L.T(" (revision ") + states[0].Revision + ")" +
                (string.IsNullOrEmpty(detail) ? "." : ": " + detail);
            check(lines.Count == 1 && lines[0] == expected, "Status text preserves the previous format: " + L.Language + " " + result.State);
            check(captures == priorCaptures && writes == priorWrites, "Formatting never reobserves or writes native state");
        }
        var key = typeof(SessionSettings).GetMethod("ReconcileStateKey", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<ReconcileState, string>>();
        foreach (var state in Enum.GetValues<ReconcileState>())
            check(key(state) == state.ToString(), "Every current state retains its localization key");
        foreach (int unknown in new[] { -1, 6, int.MaxValue })
            check(key((ReconcileState)unknown) == ((ReconcileState)unknown).ToString(), "Unknown states retain enum fallback text");
        for (int i = 0; i < 100; i++) key(ReconcileState.Applied);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) key((ReconcileState)(i % 6));
        check(GC.GetAllocatedBytesForCurrentThread() == before, "Known diagnostic labels allocate no boxes or temporary text");
    }
}
