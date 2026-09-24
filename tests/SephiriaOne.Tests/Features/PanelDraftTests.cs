using SephiriaOne;

internal static class PanelDraftTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var draft = new PanelDraft();
        object first = new(), second = new();
        Check(!draft.IsCurrent(first, 1, 1), "An unbound panel cannot dispatch actions");
        Check(draft.Observe(first, 1, 1), "First scope is an explicit binding");
        draft.Edit("10");
        Check(!draft.Observe(first, 1, 1) && draft.Text == "10", "Live refresh preserves typed input");
        Check(!draft.IsCurrent(second, 1, 1), "Replacement session rejects a stale click before refresh");
        Check(draft.Observe(second, 1, 1) && draft.Text == "", "Session replacement discards stale input");
        draft.Edit("20");
        Check(!draft.IsCurrent(second, 1, 2), "Same-avatar run restart rejects a stale click");
        Check(draft.Observe(second, 1, 2) && draft.Text == "", "Run restart discards the previous draft");
        draft.Edit("5");
        Check(draft.Observe(second, 2, 2) && draft.Text == "", "Scope epoch reset discards input");
        draft.Edit("7"); draft.Clear();
        Check(draft.Text == "" && draft.IsCurrent(second, 2, 2), "Successful action clears only input");
        draft.Observe(null, 2, 2);
        Check(!draft.IsCurrent(null, 2, 2), "No session is never a valid dispatch scope");
        return checks;
    }
}
