using SephiriaOne;

internal static class RateTests
{
    internal static void Run(Action<bool, string> check)
    {
        var ledger = new ProjectileRateLedger<object>();
        var first = new object(); var second = new object();
        ledger.Configure(4, 2, 0);
        check(ledger.TryConsume(first, "a", 0) && ledger.TryConsume(first, "a", 0), "One second of source burst is available");
        check(!ledger.TryConsume(first, "a", 0), "Exhausted source rejects before spending player credit");
        check(ledger.TryConsume(first, "b", 0) && ledger.TryConsume(first, "b", 0), "Independent source can use remaining player credit");
        check(!ledger.TryConsume(first, "c", 0), "Fresh sources cannot bypass total rate");
        check(ledger.TryConsume(second, "a", 0), "Connections have independent credits");
        check(!ledger.TryConsume(first, "a", .25), "Partial source credit cannot create a full instance");
        check(ledger.TryConsume(first, "b", .5) && ledger.TryConsume(first, "a", .5), "Denied source does not spend player refill");
        check(!ledger.TryConsume(first, "a", .5), "Native reentrant emissions see spent credit immediately");
        check(ledger.TryConsume(first, "a", 10) && ledger.TryConsume(first, "a", 10) &&
            !ledger.TryConsume(first, "a", 10), "Idle time never accumulates more than one second of burst");

        ledger.Clear(); ledger.Configure(2, 2, 0);
        check(ledger.TryConsume(first, "a", 0) && ledger.TryConsume(first, "a", 0), "Reset begins fresh");
        ledger.Configure(10, 10, .25);
        check(!ledger.TryConsume(first, "a", .25), "Rate increase settles old refill without granting a new burst");
        check(ledger.TryConsume(first, "a", .31), "New rate applies only to subsequent elapsed time");
        ledger.Configure(1, 1, 2);
        check(ledger.TryConsume(first, "a", 2) && !ledger.TryConsume(first, "a", 2), "Rate decrease clamps existing credit");
        check(!ledger.TryConsume(first, "a", 1) && !ledger.TryConsume(first, "a", 2), "Clock rollback cannot mint credit");
        check(!ledger.TryConsume(first, "a", double.NaN) && !ledger.TryConsume(first, "a", double.PositiveInfinity), "Invalid times never admit or poison clocks");
        check(ledger.TryConsume(first, "a", 3), "Valid time still works after invalid observations");
        ledger.Configure(2, 2, double.NaN);
        check(!ledger.TryConsume(first, "a", 3), "Invalid configuration clock clamps values without refilling or poisoning clocks");
        ledger.Forget(first);
        check(ledger.TryConsume(first, "a", 3), "Retired connection state can be replaced");
        check(ledger.TryConsume(first, "a", 3), "Replacement has updated two-token capacity");

        var keyed = new ProjectileRateLedger<EqualOwner>();
        keyed.Configure(1, 1, 0);
        check(keyed.TryConsume(new EqualOwner(), "a", 0) && keyed.TryConsume(new EqualOwner(), "a", 0), "Owner keys use connection reference identity");

        for (int i = 0; i < 2000; i++) ledger.TryConsume(first, "a", 3);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int accepted = 0;
        for (int i = 0; i < 10000; i++) if (ledger.TryConsume(first, "a", 3)) accepted++;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        check(accepted == 0 && bytes == 0, $"Warmed rate denials allocate zero bytes ({bytes})");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 4; i < 10004; i++) if (ledger.TryConsume(first, "a", i)) accepted++;
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        check(accepted == 10000 && bytes == 0, $"Warmed successful rate accounting allocates zero bytes ({bytes})");
    }

    private sealed class EqualOwner
    {
        public override bool Equals(object other) => other is EqualOwner;
        public override int GetHashCode() => 1;
    }
}
