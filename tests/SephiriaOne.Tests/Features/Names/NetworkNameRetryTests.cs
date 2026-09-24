using SephiriaOne;

internal static class NetworkNameRetryTests
{
    internal static int Run()
    {
        var state = new NetworkNameState();
        int checks = 0;
        void Check(bool value) { if (!value) throw new Exception("Name retry regression"); checks++; }
        string desired = NameGradient.Format("Alice");
        Check(state.Next("Alice", "Alice", true, 0) == desired);
        Check(state.Next("Alice", "Alice", true, 1.9) == null);
        Check(state.Next("Alice", "Alice", true, 2) == desired);
        Check(state.Next("Alice", "Alice", true, 4) == desired);
        Check(state.Next("Alice", "Alice", true, 6) == null && state.Exhausted);
        Check(state.Next("Alice", "Alice", true, 600) == null);
        Check(state.Next(desired, "Alice", true, 601) == null && !state.Exhausted);
        Check(state.Next("Alice", "Alice", true, 602) == desired);
        Check(state.Next("Alice", "Bob", true, 602) == NameGradient.Format("Bob"));
        Check(state.Next(desired, "Bob", false, 602) == "Bob");
        state.Reset();
        Check(state.Next("Alice", "Alice", true, 602) == desired);
        return checks;
    }
}
