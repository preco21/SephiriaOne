using SephiriaOne;

int checks = 0;
int failures = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Test(string name, Action run)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
}
ProjectileInstanceLedger<object>.Lease Reserve(
    ProjectileInstanceLedger<object> ledger, object owner, string source, int total = 64, int perSource = 16)
{
    Check(ledger.TryReserve(owner, source, total, perSource, out var lease), "Expected capacity to be available");
    Check(lease != null, "An admission returns its release lease");
    return lease;
}

Test("Reservations count before native initialization can re-enter", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var lease = Reserve(ledger, owner, "liberation", 1, 1);
    Check(ledger.Count(owner) == 1 && ledger.Count(owner, "liberation") == 1, "Reserved capacity is immediately visible");
    Check(!ledger.TryReserve(owner, "liberation", 1, 1, out var denied) && denied == null,
        "A nested spawn cannot spend the same final slot");
    lease.Dispose();
});

Test("Total cap applies across sources independently of source cap", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    Reserve(ledger, owner, "kunai", 2, 5);
    Reserve(ledger, owner, "meteor", 2, 5);
    Check(!ledger.TryReserve(owner, "ice-vine", 2, 5, out _), "A fresh source still obeys the total cap");
    Check(ledger.Count(owner) == 2 && ledger.Count(owner, "ice-vine") == 0, "Denial leaves counters unchanged");
});

Test("Source cap applies while other sources retain capacity", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    Reserve(ledger, owner, "kunai", 5, 1);
    Check(!ledger.TryReserve(owner, "kunai", 5, 1, out _), "A full source is denied below the total cap");
    Reserve(ledger, owner, "meteor", 5, 1);
    Check(ledger.Count(owner) == 2 && ledger.Count(owner, "kunai") == 1, "Independent sources retain independent counts");
});

Test("Nonpositive limits deny without creating owner state", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    foreach (var caps in new[] { (0, 1), (1, 0), (-1, 1), (1, -1) })
        Check(!ledger.TryReserve(owner, "kunai", caps.Item1, caps.Item2, out var lease) && lease == null,
            "Zero and negative limits admit no new instances");
    Check(ledger.OwnerCount == 0 && ledger.Count(owner) == 0, "Denied new owners do not occupy the ledger");
});

Test("Release and dispose return capacity exactly once", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var first = Reserve(ledger, owner, "kunai");
    var second = Reserve(ledger, owner, "kunai");
    first.Release();
    first.Release();
    first.Dispose();
    Check(ledger.Count(owner) == 1 && ledger.Count(owner, "kunai") == 1,
        "Duplicate native despawn and local destruction callbacks cannot undercount");
    second.Dispose();
    Check(ledger.Count(owner) == 0 && ledger.Count(owner, "kunai") == 0 && ledger.OwnerCount == 0,
        "Last release removes the empty owner");
});

Test("Null or throwing native creation can roll back its reservation", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    for (int attempt = 0; attempt < 2; attempt++)
    {
        var reservation = Reserve(ledger, owner, "meteor", 1, 1);
        try
        {
            if (attempt == 0) throw new InvalidOperationException("Native spawn failure");
            object nativeResult = null;
            if (nativeResult == null) reservation.Release();
        }
        catch (InvalidOperationException) { reservation.Dispose(); }
        Check(ledger.Count(owner) == 0 && ledger.OwnerCount == 0, "Failed creation restores the exact slot");
    }
    Reserve(ledger, owner, "meteor", 1, 1).Dispose();
});

Test("Existing instances can be adopted above both limits", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var first = ledger.Adopt(owner, "planet");
    var second = ledger.Adopt(owner, "planet");
    var third = ledger.Adopt(owner, "planet");
    Check(first != null && second != null && third != null && ledger.Count(owner) == 3 &&
        ledger.Count(owner, "planet") == 3, "Enable-time adoption accounts for every existing instance");
    Check(!ledger.TryReserve(owner, "planet", 2, 1, out _), "Over-cap existing instances prevent new admissions");
    first.Dispose();
    second.Dispose();
    Check(ledger.Count(owner) == 1 && !ledger.TryReserve(owner, "planet", 2, 1, out _),
        "Adopted instances finish naturally before the source has room");
    third.Dispose();
    Reserve(ledger, owner, "planet", 2, 1).Dispose();
});

Test("Lowering limits preserves live leases and blocks new admissions", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var leases = new[]
    {
        Reserve(ledger, owner, "meteor", 4, 4),
        Reserve(ledger, owner, "meteor", 4, 4),
        Reserve(ledger, owner, "meteor", 4, 4)
    };
    Check(!ledger.TryReserve(owner, "meteor", 1, 1, out _) && ledger.Count(owner) == 3,
        "A lower cap does not delete or release live instances");
    leases[0].Dispose();
    leases[1].Dispose();
    Check(!ledger.TryReserve(owner, "meteor", 1, 1, out _) && ledger.Count(owner) == 1,
        "At the new limit no extra instance is admitted");
    leases[2].Dispose();
    Reserve(ledger, owner, "meteor", 1, 1).Dispose();
});

Test("Clear makes old leases unable to debit new state for the same owner", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var old = Reserve(ledger, owner, "ice-vine");
    ledger.Clear();
    Check(ledger.OwnerCount == 0 && ledger.Count(owner) == 0, "Clear retires all current counters");
    var current = Reserve(ledger, owner, "ice-vine");
    old.Dispose();
    Check(ledger.Count(owner) == 1 && ledger.Count(owner, "ice-vine") == 1,
        "A delayed old destruction cannot decrement a new same-owner/source counter");
    current.Dispose();
    Check(ledger.OwnerCount == 0, "New state still releases normally");
});

Test("Forget retires one connection without affecting other owners or replacement state", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var otherOwner = new object();
    var old = Reserve(ledger, owner, "kunai");
    var other = Reserve(ledger, otherOwner, "kunai");
    Check(ledger.Forget(owner) && !ledger.Forget(owner), "Forgetting a connection only succeeds once");
    Check(ledger.Count(owner) == 0 && ledger.Count(otherOwner) == 1 && ledger.OwnerCount == 1,
        "Connection retirement is scoped to its exact owner");
    var current = Reserve(ledger, owner, "kunai");
    old.Release();
    Check(ledger.Count(owner) == 1 && ledger.Count(otherOwner) == 1,
        "A forgotten lease cannot debit replacement state");
    current.Dispose();
    other.Dispose();
});

Test("Connection owners use reference identity even when equal by account ID", () =>
{
    var ledger = new ProjectileInstanceLedger<ConnectionReceipt>();
    var oldConnection = new ConnectionReceipt(7);
    var newConnection = new ConnectionReceipt(7);
    Check(oldConnection.Equals(newConnection), "Fixture receipts deliberately compare equal by reusable account ID");
    Check(ledger.TryReserve(oldConnection, "kunai", 1, 1, out var old), "Old connection has its own initial slot");
    Check(ledger.TryReserve(newConnection, "kunai", 1, 1, out var current), "Reconnected equal-ID receipt has separate capacity");
    Check(ledger.OwnerCount == 2 && ledger.Count(oldConnection) == 1 && ledger.Count(newConnection) == 1,
        "Distinct receipt objects never share counters");
    oldConnection.AccountId = 99;
    old.Dispose();
    Check(ledger.Count(oldConnection) == 0 && ledger.Count(newConnection) == 1,
        "Mutable value equality cannot strand a reference-keyed lease");
    current.Dispose();
});

Test("Child provenance survives parent destruction and carries both owner and source", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var parent = Reserve(ledger, owner, "phoenix-wing", 2, 2);
    parent.Dispose();
    Check(ReferenceEquals(parent.Owner, owner) && parent.Source == "phoenix-wing",
        "Released parent leases retain their captured provenance");
    var child = Reserve(ledger, parent.Owner, parent.Source, 2, 2);
    Check(ledger.Count(owner) == 1 && ledger.Count(owner, "phoenix-wing") == 1,
        "Delayed children are counted against the inherited owner/source");
    parent.Release();
    Check(ledger.Count(owner) == 1, "Late parent callbacks do not release a child's new state");
    child.Dispose();
});

Test("Source keys use ordinal value equality", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    Reserve(ledger, owner, "kunai", 4, 1);
    string sameSource = new string(new[] { 'k', 'u', 'n', 'a', 'i' });
    Check(!ledger.TryReserve(owner, sameSource, 4, 1, out _), "Distinct string instances share their exact source key");
    Reserve(ledger, owner, "Kunai", 4, 1);
    Check(ledger.Count(owner, "kunai") == 1 && ledger.Count(owner, "Kunai") == 1,
        "Canonical source casing is the caller's responsibility");
});

Test("Missing count reads allocate no state", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    Check(ledger.Count(owner) == 0 && ledger.Count(owner, "absent") == 0 && !ledger.Forget(owner),
        "Missing owners return empty counters");
    Check(ledger.OwnerCount == 0, "Read-only queries leave the ledger empty");
});

Test("Invalid owner or source cannot enter the ledger", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    void Reject(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentNullException) { rejected = true; }
        Check(rejected, "Null provenance must be rejected");
    }
    Reject(() => ledger.TryReserve(null, "kunai", 1, 1, out _));
    Reject(() => ledger.TryReserve(owner, null, 1, 1, out _));
    Reject(() => ledger.Adopt(null, "kunai"));
    Reject(() => ledger.Adopt(owner, null));
    Reject(() => ledger.Count(null));
    Reject(() => ledger.Count(null, "kunai"));
    Reject(() => ledger.Count(owner, null));
    Reject(() => ledger.Forget(null));
    Check(ledger.OwnerCount == 0, "Invalid operations cannot create state");
});

Test("Warmed denied admissions and count reads allocate no managed memory", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var absentOwner = new object();
    var lease = Reserve(ledger, owner, "kunai", 1, 1);
    int ObserveHotPath()
    {
        int result = 0;
        if (ledger.TryReserve(owner, "kunai", 1, 1, out _)) result++;
        if (ledger.TryReserve(owner, "kunai", 2, 1, out _)) result++;
        if (ledger.TryReserve(absentOwner, "absent", 0, 1, out _)) result++;
        result += ledger.Count(owner) + ledger.Count(owner, "kunai");
        result += ledger.Count(owner, "absent") + ledger.Count(absentOwner) + ledger.Count(absentOwner, "absent");
        return result;
    }
    for (int i = 0; i < 20_000; i++) ObserveHotPath();
    long before = GC.GetAllocatedBytesForCurrentThread();
    int checksum = 0;
    for (int i = 0; i < 100_000; i++) checksum += ObserveHotPath();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Check(checksum == 200_000, "The measured loop exercises both denial gates and live/missing counts");
    Check(allocated == 0, $"Warmed denial/read loop allocated {allocated} bytes");
    lease.Dispose();
});

Test("Short-lived sequential projectiles do not recreate counter storage", () =>
{
    long Measure(bool keepOwnerLive)
    {
        var ledger = new ProjectileInstanceLedger<object>();
        var owner = new object();
        var anchor = keepOwnerLive ? Reserve(ledger, owner, "anchor") : null;
        for (int i = 0; i < 2_000; i++)
        { ledger.TryReserve(owner, "kunai", 64, 12, out var warm); warm.Release(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        { ledger.TryReserve(owner, "kunai", 64, 12, out var lease); lease.Release(); }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        anchor?.Release();
        Check(ledger.OwnerCount == 0, "No empty owner remains registered after the workload");
        return bytes;
    }
    long live = Measure(true), empty = Measure(false);
    Console.WriteLine($"Ledger turnover: {empty} bytes / 10,000 sequential admissions; retained-owner reference {live} bytes.");
    Check(empty <= live, "Returning to zero active instances must not allocate fresh counter dictionaries per spawn");
});

Test("Reused empty counter storage cannot be debited by retired leases", () =>
{
    var ledger = new ProjectileInstanceLedger<object>();
    var owner = new object();
    var retired = Reserve(ledger, owner, "kunai");
    ledger.Clear();
    var released = Reserve(ledger, owner, "kunai");
    released.Release();
    var current = Reserve(ledger, owner, "kunai");
    retired.Release(); released.Release();
    Check(ledger.Count(owner) == 1, "Clear-retired and already-released leases cannot debit a reused empty counter");
    ledger.Forget(owner);
    var replacement = Reserve(ledger, owner, "meteor");
    current.Release();
    Check(ledger.Count(owner) == 1 && ledger.Count(owner, "meteor") == 1,
        "Forget-retired leases cannot debit replacement storage");
    replacement.Release();
});

Console.WriteLine($"Projectile ledger: {checks} checks, {failures} failed cases.");
return failures == 0 ? 0 : 1;

sealed class ConnectionReceipt
{
    public int AccountId;
    public ConnectionReceipt(int accountId) { AccountId = accountId; }
    public override bool Equals(object other) => other is ConnectionReceipt receipt && receipt.AccountId == AccountId;
    public override int GetHashCode() => AccountId;
}
