using SephiriaOne;

internal static class StateWriteBatchTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        int a = 1, b = 2, writes = 0;
        var batch = new StateWriteBatch(() => true);
        batch.Add("a", () => a, value => { a = value; writes++; }, 4);
        batch.Add("b", () => b, value => { b = value; writes++; }, 5);
        b = 3;
        Check(!batch.TryCommit(out _) && a == 1 && writes == 0, "Preflight rejects entire stale batch");
        b = 2;
        Check(batch.TryCommit(out _) && a == 4 && b == 5 && writes == 2, "Valid batch commits targets");
        Check(batch.TryCommit(out _) && writes == 2, "Same batch replay is idempotent");
        bool fail = true;
        var partial = new StateWriteBatch(() => true);
        partial.Add("a", () => a, value => a = value, 8);
        partial.Add("b", () => b, value => { if (fail) throw new InvalidOperationException("fixture failure"); b = value; }, 9);
        Check(!partial.TryCommit(out string error) && partial.MayHaveWritten && a == 8 && b == 5 && error.Contains("fixture failure"),
            "Partial failure is retained and never called a no-op");
        fail = false;
        b = 7;
        Check(!partial.TryRecover(out _) && b == 7, "Recovery preserves external edits");
        b = 5;
        Check(partial.TryRecover(out _) && b == 9, "Explicit recovery completes only known before/target values");
        var unread = new StateWriteBatch(() => true);
        unread.Add("a", () => a, value => { }, 99);
        Check(!unread.TryCommit(out _), "Ignored native writes fail readback");
        var denied = new StateWriteBatch(() => false);
        denied.Add("b", () => b, value => b = value, 0);
        Check(!denied.TryCommit(out _) && b == 9, "Authority/identity guard applies before writes");
        bool authority = true;
        var lastCallback = new StateWriteBatch(() => authority);
        lastCallback.Add("a", () => a, value => { a = value; authority = false; }, 10);
        Check(!lastCallback.TryCommit(out _) && lastCallback.MayHaveWritten, "Final native callback cannot bypass authority readback");
        bool inputValid = true;
        var changedInput = new StateWriteBatch(() => true);
        changedInput.RequireAfter(() => inputValid);
        changedInput.Add("a", () => a, value => { a = value; inputValid = false; }, 11);
        Check(!changedInput.TryCommit(out _) && changedInput.MayHaveWritten, "Changing an unwritten planning input prevents verified success");
        inputValid = true;
        Check(changedInput.TryRecover(out _) && a == 11, "Explicit recovery validates final inputs without replaying a completed write");
        return checks;
    }
}
