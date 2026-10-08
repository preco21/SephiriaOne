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
        coordinator.Register(ReconciliationRule<Subject>.ObserveValue("double", SyncDomain.Stats, SyncDomain.Limits,
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
        once.Register(ReconciliationRule<Subject>.ObserveValue("enrollment", SyncDomain.Identity, SyncDomain.Stats,
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
        ordered.Register(ReconciliationRule<Subject>.ObserveValue("consume", SyncDomain.Limits, SyncDomain.Choices,
            ReconcileMode.OnChange, s => true, s => s.Output, s => { order.Add("consume"); return ReconcileResult.Applied(); }));
        ordered.Register(ReconciliationRule<Subject>.ObserveValue("derive", SyncDomain.Stats, SyncDomain.Limits,
            ReconcileMode.OnChange, s => true, s => s.Input, s => { order.Add("derive"); s.Output = s.Input; return ReconcileResult.Applied(); }));
        ordered.Reconcile(subject);
        Check(string.Join(",", order) == "derive,consume", "Dependencies order rules independently of registration");
        bool cycle = false;
        try { ordered.Register(ReconciliationRule<Subject>.ObserveValue("cycle", SyncDomain.Choices, SyncDomain.Stats,
            ReconcileMode.OnChange, s => true, s => 0, s => ReconcileResult.Applied())); }
        catch (InvalidOperationException) { cycle = true; }
        Check(cycle, "Dependency cycles rejected at registration");

        var nested = new ReconciliationCoordinator<Subject>();
        bool nestedSuccess = true;
        int nestedCalls = 0;
        nested.Register(ReconciliationRule<Subject>.ObserveValue("nested", SyncDomain.Stats, SyncDomain.None,
            ReconcileMode.OnChange, s => true, s => s.Input, s =>
            {
                if (++nestedCalls == 1) { s.Input++; nested.Invalidate(s, SyncDomain.Stats); nestedSuccess = nested.Reconcile(s); }
                return ReconcileResult.Applied();
            }));
        Check(nested.Reconcile(subject) && !nestedSuccess && nestedCalls == 2,
            "Reentrant flush reports unavailable and preserves next-pass invalidation");

        var faults = new ReconciliationCoordinator<Subject>();
        int faultCalls = 0;
        faults.Register(ReconciliationRule<Subject>.ObserveValue("throw", SyncDomain.Stats, SyncDomain.None,
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

        var other = new Subject { Ready = true, Input = subject.Input, Output = subject.Output };
        before = writes;
        Check(coordinator.Reconcile(other) && writes == before + 1,
            "Typed observations belong to individual subjects even with identical values");
        coordinator.Reconcile(subject);
        Check(writes == before + 1, "Visiting a second subject does not replace the first observation");
        Check(coordinator.TryGetResult(subject, "double", out var outcome) && outcome.State == ReconcileState.Applied &&
            !coordinator.TryGetResult(subject, "absent", out _), "Direct diagnostics preserve outcomes without building status lists");

        int waits = 0;
        var pending = new ReconciliationCoordinator<Subject>();
        pending.Register(ReconciliationRule<Subject>.ObserveValue("pending", SyncDomain.Identity, SyncDomain.None,
            ReconcileMode.Once, s => s.Ready, s => s.Input, s => ++waits < 3 ? ReconcileResult.Waiting("native load") : ReconcileResult.Applied()));
        Check(!pending.Reconcile(subject) && !pending.Reconcile(subject) && pending.Reconcile(subject) && waits == 3,
            "Unchanged waiting observations still retry to completion");
        pending.Reconcile(subject);
        Check(waits == 3, "Completed once rule still stops retrying");
        int lateCalls = 0;
        coordinator.Register(ReconciliationRule<Subject>.ObserveValue("late", SyncDomain.Names, SyncDomain.None,
            ReconcileMode.OnChange, s => true, s => s.Input, s => { lateCalls++; return ReconcileResult.Applied(); }));
        coordinator.Reconcile(subject);
        Check(lateCalls == 1, "Late registration creates an observer for existing subjects");

        for (int i = 0; i < 1000; i++) coordinator.Reconcile(subject);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 5000; i++)
        { coordinator.Reconcile(subject); coordinator.TryGetResult(subject, "double", out _); }
        Check(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024,
            "Typed unchanged observations and direct diagnostics do not allocate per tick");

        var legacy = new ReconciliationCoordinator<Subject>();
        legacy.Register(new ReconciliationRule<Subject>("legacy", SyncDomain.Stats, SyncDomain.None,
            ReconcileMode.OnChange, s => true, s => s.Input, s => ReconcileResult.Applied()));
        Check(legacy.Reconcile(subject) && legacy.Reconcile(subject), "Object observation constructor remains compatible");
        foreach (var result in new[] { ReconcileResult.Applied(), ReconcileResult.NativeFallback("native restored"),
            ReconcileResult.Waiting("native loading"), ReconcileResult.Suspended("cannot restore"),
            ReconcileResult.Rejected("invalid inputs"), ReconcileResult.Faulted("partial write") })
        {
            var readiness = new ReconciliationCoordinator<Subject>();
            int calls = 0;
            readiness.Register(ReconciliationRule<Subject>.ObserveValue("readiness", SyncDomain.Stats, SyncDomain.None,
                ReconcileMode.OnChange, s => s.Ready, s => s.Input, s => { calls++; return result; }));
            bool expected = result.State == ReconcileState.Applied || result.State == ReconcileState.NativeFallback;
            Check(readiness.Reconcile(subject) == expected && readiness.Reconcile(subject) == expected,
                "Only verified application or native fallback is fresh: " + result.State);
            Check(calls == (result.State == ReconcileState.WaitingForReadiness ? 2 : 1),
                "Fallback and unresolved stable outcomes do not busy retry: " + result.State);
            var states = new List<ReconciliationStatus>();
            readiness.AppendStatuses(subject, states);
            var copied = readiness.Describe(subject)[0];
            Check(states.Count == 1 && states[0].Id == copied.Id && states[0].State == result.State &&
                states[0].Detail == result.Detail && states[0].Revision == copied.Revision,
                "Buffered diagnostics preserve complete state: " + result.State);
        }
        var diagnostics = new ReconciliationCoordinator<Subject>();
        int observations = 0, applications = 0;
        diagnostics.Register(ReconciliationRule<Subject>.ObserveValue("first", SyncDomain.Stats, SyncDomain.Limits,
            ReconcileMode.OnChange, s => true, s => { observations++; return s.Input; }, s =>
            { applications++; return ReconcileResult.Applied("original detail"); }));
        var inspected = new Subject();
        var buffer = new List<ReconciliationStatus>();
        diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 0 && observations == 0 && applications == 0, "Unknown subject inspection does not enroll or observe it");
        diagnostics.Invalidate(inspected, SyncDomain.Stats);
        diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 1 && buffer[0].State == ReconcileState.WaitingForReadiness && buffer[0].Revision == 0 &&
            buffer[0].Detail == "Not observed yet." && observations == 0 && applications == 0, "Pending diagnostics cannot acknowledge or apply invalidation");
        diagnostics.Reconcile(inspected);
        var retained = diagnostics.Describe(inspected);
        diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 2 && buffer[0].Revision == 0 && buffer[1].Revision == 1, "Append preserves caller entries and copies outcomes by value");
        diagnostics.Register(ReconciliationRule<Subject>.ObserveValue("later", SyncDomain.Limits, SyncDomain.None,
            ReconcileMode.OnChange, s => true, s => s.Input, s => ReconcileResult.Applied()));
        buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 1, "Diagnostics cannot initialize a newly registered rule");
        inspected.Input++;
        diagnostics.Reconcile(inspected);
        buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Select(s => s.Id).SequenceEqual(new[] { "first", "later" }) && buffer[0].Revision == 2 &&
            retained.Count == 1 && retained[0].Revision == 1 && retained[0].Detail == "original detail",
            "Buffered diagnostics preserve rule order and independent snapshots across changes");
        int previousObservations = observations, previousApplications = applications;
        for (int i = 0; i < 1000; i++) { buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer); }
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 5000; i++) { buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer); }
        Check(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024 && observations == previousObservations && applications == previousApplications,
            "Repeated buffered reads avoid per-status allocation and never invoke observers or writes");
        diagnostics.Forget(inspected);
        buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 0 && retained[0].Revision == 1, "Forget clears live diagnostics without altering earlier snapshots");
        diagnostics.Reconcile(inspected);
        buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 2 && buffer[0].Revision == 1, "Reentry creates fresh diagnostic revisions");
        diagnostics.Clear();
        buffer.Clear(); diagnostics.AppendStatuses(inspected, buffer);
        Check(buffer.Count == 0, "Scope teardown leaves no buffered live outcome");
        return checks;
    }
}
