using SephiriaOne;

internal static class AmplificationCurveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var defaults = default(PvpAmplificationSettings);
        check(!defaults.Enabled && defaults.SoftPercent == 50 && defaults.HardPercent == 100 && !defaults.HasChanges,
            "Amplification defaults are off, soft 50 and hard 100 bonus percent");
        check(defaults.Limit(300) == 300, "Disabled curve preserves native amplification");
        check(defaults.Equals(new PvpAmplificationSettings(false)), "Explicit and zero-initialized amplification defaults remain equal");
        var policy = new PvpAmplificationSettings(true);
        foreach (float value in new[] { -1000f, -100f, 0f, 1f, 49.9f, 50f })
            check(policy.Limit(value) == value, "Native amplification below soft limit is unchanged: " + value);
        check(policy.Limit(100) == 87.5f && policy.Limit(150) == 100 && policy.Limit(300) == 100,
            "Approved smooth-curve examples and effective hard cap");
        check(policy.Limit(int.MaxValue) == 100 && policy.Limit(float.MaxValue) == 100,
            "Extreme finite native bonuses cannot exceed hard cap");
        float previous = policy.Limit(50), previousIncrement = float.MaxValue;
        for (int i = 1; i <= 1200; i++)
        {
            float input = 50 + i * 0.25f, next = policy.Limit(input), increment = next - previous;
            check(next >= previous && next <= 100 && next <= input, "Curve remains monotone, bounded and never amplifies extra: " + input);
            check(increment <= previousIncrement + 0.00002f, "Additional investment has diminishing returns: " + input);
            previous = next; previousIncrement = increment;
        }
        const float h = 0.01f;
        check(Math.Abs((policy.Limit(50 + h) - policy.Limit(50)) / h - 1) < 0.002f,
            "Soft join has native unit slope");
        check((policy.Limit(150) - policy.Limit(150 - h)) / h < 0.002f,
            "Hard join reaches zero slope smoothly");
        var narrow = new PvpAmplificationSettings(true, 50, 50.01m);
        check(narrow.Limit(50) == 50 && Math.Abs(narrow.Limit(50.02f) - 50.01f) < 0.00001f,
            "Fractional limits preserve a narrow transition");
        var equal = new PvpAmplificationSettings(true, 75, 75);
        check(equal.Limit(74) == 74 && equal.Limit(100) == 75, "Equal limits explicitly form a hard cap without dividing by zero");
        var zero = new PvpAmplificationSettings(true, 0, 0);
        check(zero.Limit(100) == 0 && zero.Limit(-10) == -10, "Explicit zero cap never removes negative native penalties");
        foreach (var limits in new[] { (-1m, 100m), (101m, 100m), (0m, 10001m), (0.001m, 100m), (50m, 100.001m) })
        {
            bool rejected = false;
            try { _ = new PvpAmplificationSettings(true, limits.Item1, limits.Item2); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Invalid amplification policy rejected: " + limits);
        }
        float sink = 0;
        for (int i = 0; i < 1000; i++) sink += policy.Limit(60 + i % 100);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) sink += policy.Limit(60 + i % 100);
        check(GC.GetAllocatedBytesForCurrentThread() == before && sink > 0,
            "Curve performs no per-hit allocation");
    }
}
