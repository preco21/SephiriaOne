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

        long maximumAllocation = 0;
        foreach (string profile in new[]
        {
            "<color=#123456>A</color>",
            "<color=#123456>AB</color>",
            "<noparse><color=#123456>A</color></noparse>B"
        })
        {
            state = new NetworkNameState();
            desired = NameGradient.Format(profile);
            Check(NameGradient.Plain(profile) == profile);
            Check(state.Next("native", profile, true, 0) == desired);
            for (int i = 0; i < 2000; i++) state.Next("native", profile, true, 1);
            const int iterations = 10000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool repeatedRequest = false;
            for (int i = 0; i < iterations; i++)
                repeatedRequest |= state.Next("native", profile, true, 1) != null;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            maximumAllocation = Math.Max(maximumAllocation, allocated);
            Console.WriteLine($"Pending marked-up name: {allocated / (double)iterations:F0} bytes/tick ({profile})");
            Check(!repeatedRequest);

            Check(state.Next("native", profile, true, 2) == desired);
            Check(state.Next("native", profile, true, 4) == desired);
            Check(state.Next("native", profile, true, 6) == null && state.Exhausted);
            Check(state.Next(desired, profile, true, 7) == null && !state.Exhausted);
            string edited = profile + "C";
            Check(state.Next(desired, edited, true, 8) == NameGradient.Format(edited));
            Check(state.Next(desired, edited, true, 8.5) == null);
            Check(state.Next(desired, edited, false, 9) == edited);
            Check(state.Next(edited, edited, false, 9.5) == null);
            state.Reset();
            Check(state.Next("native", profile, true, 0) == desired);
        }
        if (maximumAllocation > 1024)
            throw new Exception($"Unchanged pending name repeatedly normalizes preserved markup: up to {maximumAllocation} bytes for 10000 ticks");
        checks++;
        return checks;
    }
}
