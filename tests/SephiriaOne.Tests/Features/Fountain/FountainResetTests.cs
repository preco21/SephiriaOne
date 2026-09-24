using SephiriaOne;

internal static class FountainResetTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }
        FountainCommand.Parse("/fountain 100", out var set, out _);
        FountainCommand.Parse("/fountain +10", out var add, out _);
        FountainCommand.Parse("/fountain -5", out var subtract, out _);
        FountainCommand.Parse("/fountain reset", out var reset, out _);
        FountainCommand.Parse("/fountain 0", out var zero, out _);
        int[] initial = { 4, 8 };
        Check(set.TryPlanTracked(initial, new[] { 0, 0 }, 12, null, null, out var plan, out _) &&
            plan.Points.SequenceEqual(new[] { 100, 100 }) && plan.Contributions.SequenceEqual(new[] { 96, 92 }) &&
            plan.Limit == 100 && plan.OriginalLimit == 12 && plan.AppliedLimit == 100, "Track each player's set delta and original cap");
        Check(initial.SequenceEqual(new[] { 4, 8 }), "Planning leaves input intact");
        Check(add.TryPlanTracked(plan.Points, plan.Contributions, plan.Limit, plan.OriginalLimit, plan.AppliedLimit, out plan, out _) &&
            plan.Points.SequenceEqual(new[] { 110, 110 }) && plan.Contributions.SequenceEqual(new[] { 106, 102 }) &&
            plan.OriginalLimit == 12 && plan.AppliedLimit == 110, "Successive commands retain baseline");
        Check(subtract.TryPlanTracked(new[] { 112, 110 }, plan.Contributions, plan.Limit, plan.OriginalLimit, plan.AppliedLimit, out plan, out _) &&
            plan.Points.SequenceEqual(new[] { 107, 105 }) && plan.Contributions.SequenceEqual(new[] { 101, 97 }), "Track subtraction after native stat gain");
        Check(reset.TryPlanTracked(plan.Points, plan.Contributions, plan.Limit, plan.OriginalLimit, plan.AppliedLimit, out plan, out _) &&
            plan.Points.SequenceEqual(new[] { 6, 8 }) && plan.Contributions.All(x => x == 0) &&
            plan.Limit == 12 && plan.OriginalLimit == null && plan.AppliedLimit == null, "Restore distinct native points including later stat gains");
        Check(reset.TryPlanTracked(plan.Points, plan.Contributions, plan.Limit, null, null, out plan, out _) &&
            plan.Points.SequenceEqual(new[] { 6, 8 }) && plan.Limit == 12, "Repeated reset is a no-op");
        Check(reset.TryPlanTracked(new[] { 100, 8 }, new[] { 96, 0 }, 100, 12, 100, out plan, out _) &&
            plan.Points.SequenceEqual(new[] { 4, 8 }), "New/unmodified players stay unchanged");
        Check(zero.TryPlanTracked(new[] { 4 }, new[] { 0 }, 12, null, null, out plan, out _) &&
            plan.Contributions[0] == -4 && plan.OriginalLimit == null, "Track reductions below natural points");
        Check(reset.TryPlanTracked(new[] { 1 }, plan.Contributions, 12, null, null, out plan, out _) &&
            plan.Points[0] == 5, "Reset reverses reductions while retaining native gains");
        Check(set.TryPlanTracked(new[] { 8 }, new[] { 0 }, 150, null, null, out plan, out _) &&
            plan.Limit == 150 && plan.OriginalLimit == null, "Do not claim another source's higher cap");
        Check(reset.TryPlanTracked(plan.Points, plan.Contributions, 150, null, null, out plan, out _) &&
            plan.Limit == 150 && plan.Points[0] == 8, "Reset preserves pre-existing cap");
        Check(reset.TryPlanTracked(new[] { 100 }, new[] { 96 }, 200, 12, 100, out plan, out _) &&
            plan.Limit == 200 && plan.OriginalLimit == null, "Preserve independently replaced cap");
        FountainCommand.Parse("/fountain 250", out var high, out _);
        Check(high.TryPlanTracked(new[] { 100 }, new[] { 96 }, 200, 12, 100, out plan, out _) &&
            plan.OriginalLimit == 200 && plan.AppliedLimit == 250, "A later raise captures independently replaced cap");
        Check(reset.TryPlanTracked(plan.Points, plan.Contributions, plan.Limit, plan.OriginalLimit, plan.AppliedLimit, out plan, out _) &&
            plan.Limit == 200 && plan.Points[0] == 4, "Reset after later raise restores independent cap");
        Check(!add.TryPlanTracked(new[] { 4, int.MaxValue }, new[] { 0, 0 }, 12, null, null, out plan, out _) &&
            plan.Points.Length == 0 && plan.Contributions.Length == 0 && plan.Limit == 12, "Late failure yields no partial batch");
        Check(!add.TryPlanTracked(new[] { 0 }, new[] { int.MaxValue }, 12, null, null, out plan, out _) &&
            plan.Points.Length == 0, "Reject accumulated contribution overflow");
        Check(!reset.TryPlanTracked(new[] { 4, 0 }, new[] { 0, 5 }, 100, 12, 100, out plan, out _) &&
            plan.Points.Length == 0 && plan.Limit == 100, "Invalid reset rejects entire batch and cap change");
        Check(!reset.TryPlanTracked(new[] { 8 }, new[] { 0 }, 12, 12, null, out _, out _), "Reject incomplete limit tracking");
        Check(!set.TryPlanTracked(new[] { 8 }, Array.Empty<int>(), 12, null, null, out _, out _), "Reject mismatched tracking arrays");
        Check(!reset.TryPlanTracked(Array.Empty<int>(), Array.Empty<int>(), 12, null, null, out _, out _), "No players is not a successful reset");
        return checks;
    }
}
