using SephiriaOne;

internal static class ReconciliationTests
{
    private sealed class Subject { public bool Ready; public int Input; public int Output; }

    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        var coordinator = new ReconciliationCoordinator<Subject>();
        int writes = 0;
        coordinator.Register(new ReconciliationRule<Subject>("double", SyncDomain.Stats, SyncDomain.Limits,
            ReconcileMode.OnChange, s => s.Ready, s => (s.Input, s.Output), s =>
            { writes++; s.Output = 2 * s.Input; return ReconcileResult.Applied(); }));
        var subject = new Subject { Input = 3 };
        Check(!coordinator.Reconcile(subject) && writes == 0, "Readiness defers writes");
        subject.Ready = true;
        Check(coordinator.Reconcile(subject) && subject.Output == 6, "Readiness transition reconciles");
        coordinator.Reconcile(subject);
        Check(writes == 1, "Post-apply observation prevents feedback writes");
        subject.Input = 5;
        coordinator.Reconcile(subject);
        Check(subject.Output == 10 && writes == 2, "Missing event is caught by observation");
        subject.Output = 2;
        coordinator.Reconcile(subject);
        Check(subject.Output == 10, "External drift is reconciled");
        coordinator.Invalidate(subject, SyncDomain.Stats);
        coordinator.Invalidate(subject, SyncDomain.Stats);
        int before = writes;
        coordinator.Reconcile(subject);
        Check(writes == before + 1, "Duplicate invalidations coalesce");

        int attempts = 0;
        var once = new ReconciliationCoordinator<Subject>();
        once.Register(new ReconciliationRule<Subject>("enrollment", SyncDomain.Identity, SyncDomain.Stats,
            ReconcileMode.Once, s => s.Ready, s => s.Input, s =>
            { attempts++; return ReconcileResult.Rejected("Invalid baseline"); }));
        once.Reconcile(subject);
        subject.Input++;
        once.Invalidate(subject, SyncDomain.Identity);
        once.Reconcile(subject);
        Check(attempts == 1, "Rejected enrollment is not silently retried on native changes");
        var replacement = new Subject { Ready = true, Input = subject.Input };
        once.Reconcile(replacement);
        Check(attempts == 2, "Replacement subject has independent lifetime");
        once.Forget(subject);
        Check(once.Describe(subject).Count == 0, "Departure removes all tracking");

        var order = new List<string>();
        var ordered = new ReconciliationCoordinator<Subject>();
        ordered.Register(new ReconciliationRule<Subject>("view", SyncDomain.Limits, SyncDomain.Presentation,
            ReconcileMode.OnChange, s => true, s => s.Output, s => { order.Add("view"); return ReconcileResult.Applied(); }));
        ordered.Register(new ReconciliationRule<Subject>("derive", SyncDomain.Stats, SyncDomain.Limits,
            ReconcileMode.OnChange, s => true, s => s.Input, s => { order.Add("derive"); s.Output = s.Input; return ReconcileResult.Applied(); }));
        ordered.Reconcile(subject);
        Check(string.Join(",", order) == "derive,view", "Dependencies order rules independently of registration");
        bool cycle = false;
        try { ordered.Register(new ReconciliationRule<Subject>("cycle", SyncDomain.Presentation, SyncDomain.Stats,
            ReconcileMode.OnChange, s => true, s => 0, s => ReconcileResult.Applied())); }
        catch (InvalidOperationException) { cycle = true; }
        Check(cycle, "Dependency cycles rejected at registration");

        var nested = new ReconciliationCoordinator<Subject>();
        bool nestedSuccess = true;
        int nestedCalls = 0;
        nested.Register(new ReconciliationRule<Subject>("nested", SyncDomain.Stats, SyncDomain.None,
            ReconcileMode.OnChange, s => true, s => s.Input, s =>
            {
                if (++nestedCalls == 1) { s.Input++; nested.Invalidate(s, SyncDomain.Stats); nestedSuccess = nested.Reconcile(s); }
                return ReconcileResult.Applied();
            }));
        Check(nested.Reconcile(subject) && !nestedSuccess && nestedCalls == 2,
            "Reentrant flush reports unavailable and preserves next-pass invalidation");

        var faults = new ReconciliationCoordinator<Subject>();
        int faultCalls = 0;
        faults.Register(new ReconciliationRule<Subject>("throw", SyncDomain.Stats, SyncDomain.None,
            ReconcileMode.OnChange, s => true, s => s.Input, s => { faultCalls++; throw new InvalidOperationException("write failed"); }));
        Check(!faults.Reconcile(subject) && faults.Describe(subject)[0].State == ReconcileState.Faulted,
            "Exceptions are visible faults, not successful application");
        faults.Reconcile(subject);
        Check(faultCalls == 1, "Unchanged fault does not spin/replay");
        subject.Input++;
        faults.Reconcile(subject);
        Check(faultCalls == 2, "Changed observations can retry a change-driven rule");
        faults.Clear();
        Check(faults.Describe(subject).Count == 0, "Session teardown clears outcomes");
        coordinator.AcceptObservation(subject, "double");
        before = writes;
        Check(coordinator.Reconcile(subject) && writes == before, "Validated external command observation is acknowledged without replay");
        once.AcceptObservation(replacement, "enrollment");
        Check(once.Reconcile(replacement) && attempts == 2 && once.Describe(replacement)[0].State == ReconcileState.Applied,
            "Explicit recovery completes once-only enrollment without repeating writes");
        return checks;
    }
}
