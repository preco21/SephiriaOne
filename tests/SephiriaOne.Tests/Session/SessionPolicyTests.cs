using SephiriaOne;

internal static class SessionPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        {
            if (!condition) throw new Exception(scenario);
            checks++;
        }
        FountainCommand Fountain(string text)
        {
            if (FountainCommand.Parse("/fountain " + text, out var command, out _) != FountainParseResult.Valid) throw new Exception(text);
            return command;
        }
        StatCommand Stat(string text)
        {
            if (StatCommand.Parse("/stats " + text, out var command, out _) != StatParseResult.Valid) throw new Exception(text);
            return command;
        }
        SessionPlayerSnapshot Snapshot(int luck = 5, int points = 4, int fountainContribution = 0,
            int? limit = 12, int? originalLimit = null, int? appliedLimit = null,
            Dictionary<string, int>? raw = null, Dictionary<string, int>? bonus = null,
            Dictionary<string, int>? amplifiers = null, bool choicesAvailable = true)
        {
            raw ??= new Dictionary<string, int> { ["LUCK"] = luck };
            return new SessionPlayerSnapshot(raw, bonus ?? new(), amplifiers ?? new(), points,
                fountainContribution, limit, originalLimit, appliedLimit, choicesAvailable);
        }
        SessionStatWrite Write(SessionPlan plan, string key) => plan.Stats.Single(write => write.Key == key);
        var policy = new SessionPolicy();
        Check(!policy.HasChanges && policy.TryPlan(Snapshot(), out var plan, out _) &&
            plan.Stats.Count == 0 && plan.Fountain == null, "Empty session leaves a new player untouched");
        policy.Record(Fountain("+10"));
        policy.Record(Fountain("-3"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && plan.Fountain!.Points[0] == 11 &&
            plan.Fountain!.Contributions[0] == 7, "Relative Fountain settings use newcomer's own baseline");
        Check(policy.TryPlan(Snapshot(points: 20), out plan, out _) && plan.Fountain!.Points[0] == 27,
            "Different joining baselines remain distinct");
        policy.Record(Fountain("100"));
        policy.Record(Fountain("+10"));
        policy.Record(Fountain("-5"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && plan.Fountain!.Points[0] == 105 &&
            plan.Fountain!.Contributions[0] == 101 && plan.Fountain!.Limit == 105 &&
            plan.Fountain!.OriginalLimit == 12 && plan.Fountain!.AppliedLimit == 105,
            "Set replaces relative history; later deltas adjust absolute target and carryover cap");
        Check(policy.TryPlan(Snapshot(points: 109, fountainContribution: 101, limit: 109, originalLimit: 12, appliedLimit: 109), out plan, out _) &&
            plan.Fountain!.Points[0] == 105 && plan.Fountain!.Contributions[0] == 97 &&
            plan.Fountain!.OriginalLimit == 12, "Restored Fountain markers are not applied twice; preserve native +4");
        policy.Record(Fountain("reset"));
        Check(!policy.HasChanges && policy.TryPlan(Snapshot(), out plan, out _) && plan.Fountain == null,
            "Fountain reset removes setting for future joins");
        policy.Record(Fountain("0"));
        Check(policy.HasChanges && policy.TryPlan(Snapshot(), out plan, out _) && plan.Fountain!.Points[0] == 0,
            "Fountain zero remains an inherited absolute target");
        policy.Clear();
        policy.Record(Fountain("+10"));
        policy.Record(Fountain("-10"));
        Check(!policy.HasChanges, "Canceling relative Fountain commands leaves no inherited offset");

        policy.Record(Stat("luck +10"));
        policy.Record(Stat("luck -3"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && Write(plan, "LUCK").Raw == 12 &&
            Write(plan, "LUCK").Contribution == 7, "Stat relative history uses newcomer's native value");
        Check(policy.TryPlan(Snapshot(luck: 40), out plan, out _) && Write(plan, "LUCK").Raw == 47,
            "Joining player does not copy the host's stats");
        policy.Record(Stat("luck set 100"));
        policy.Record(Stat("luck +10"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && Write(plan, "LUCK").Raw == 15 &&
            Write(plan, "LUCK").Contribution == 10, "Relative stat command after set starts a new offset from native baseline");
        Check(policy.TryPlan(Snapshot(raw: new() { ["LUCK"] = 115, ["SEPHIRIAONE_STAT_LUCK"] = 100 }), out plan, out _) &&
            Write(plan, "LUCK").Raw == 25 && Write(plan, "LUCK").Contribution == 10,
            "Stat inheritance replaces existing addon adjustment, preserving reset baseline");
        Check(policy.TryPlan(Snapshot(bonus: new() { ["LUCK"] = 5 }, amplifiers: new() { ["LUCK"] = 100 }), out plan, out _) &&
            Write(plan, "LUCK").Raw == 10 && Write(plan, "LUCK").Contribution == 5,
            "Inherited relative stat compensates for guest bonus and multiplier");
        policy.Record(Stat("critical +1.25"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && Write(plan, "CRITICAL").Raw == 125,
            "Inherited decimal stat uses display units");
        policy.Record(Stat("luck reset"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && plan.Stats.Count == 1 && Write(plan, "CRITICAL").Raw == 125,
            "Stat reset removes only its selected future setting");
        policy.Record(Stat("reset"));
        Check(!policy.HasChanges, "Reset-all clears every inherited stat");
        policy.Record(Stat("luck 0"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && Write(plan, "LUCK").Raw == 0 &&
            Write(plan, "LUCK").Contribution == -5, "Stat zero remains a set, not reset");
        policy.Clear();
        policy.Record(Stat("luck +5"));
        policy.Record(Stat("luck -5"));
        Check(!policy.HasChanges, "Canceling stat offsets leaves future players unchanged");

        policy.RecordChoice("EXTRAITEMCHOICES", 5);
        policy.RecordChoice("EXTRAWEAPONCHOICES", 5);
        policy.RecordChoice("EXTRAMIRACLECHOICES", 7);
        Check(policy.TryPlan(Snapshot(raw: new() { ["EXTRAITEMCHOICES"] = 2 }), out plan, out _) &&
            Write(plan, "EXTRAITEMCHOICES").Raw == 7 && Write(plan, "EXTRAITEMCHOICES").Contribution == 5 &&
            Write(plan, "EXTRAMIRACLECHOICES").Raw == 7, "Candidate categories inherit their active extra contribution");
        Check(policy.TryPlan(Snapshot(raw: new() { ["EXTRAITEMCHOICES"] = 7, ["SEPHIRIAONE_EXTRAITEMCHOICES"] = 5 }), out plan, out _) &&
            Write(plan, "EXTRAITEMCHOICES").Raw == 7, "Candidate contribution is not doubled on restored avatar");
        policy.RecordChoice("EXTRAITEMCHOICES", 0);
        Check(policy.TryPlan(Snapshot(), out plan, out _) && plan.Stats.All(write => write.Key != "EXTRAITEMCHOICES") && plan.Stats.Count == 2,
            "Candidate reset removes only selected category");
        policy.Record(Fountain("10"));
        policy.Record(Stat("luck 11"));
        Check(!policy.TryPlan(Snapshot(amplifiers: new() { ["LUCK"] = 100 }), out plan, out _) &&
            plan.Stats.Count == 0 && plan.Fountain == null, "One unrepresentable inherited value rejects every planned change");
        Check(!policy.TryPlan(Snapshot(choicesAvailable: false), out plan, out _) && plan.Stats.Count == 0 && plan.Fountain == null,
            "Unavailable candidate guards prevent partial inheritance");
        Check(!policy.TryPlan(Snapshot(limit: null), out plan, out _), "Missing Fountain session limit rejects inheritance");
        policy.Record(Fountain("reset"));
        policy.Record(Stat("reset"));
        Check(policy.HasChanges && policy.TryPlan(Snapshot(), out plan, out _) && plan.Stats.Count == 2,
            "Resetting Fountain and stats does not clear candidate settings");
        policy.Clear();
        Check(!policy.HasChanges && policy.TryPlan(Snapshot(), out plan, out _) && plan.Stats.Count == 0,
            "Session end/unload clears every family");
        policy.Record(Fountain("-5"));
        Check(!policy.TryPlan(Snapshot(), out plan, out _) && plan.Fountain == null, "Newcomer Fountain underflow is rejected");
        policy.Clear();
        policy.Record(Stat("luck +10"));
        Check(!policy.TryPlan(Snapshot(raw: new() { ["LUCK"] = int.MinValue, ["SEPHIRIAONE_STAT_LUCK"] = 1 }), out plan, out _) &&
            plan.Stats.Count == 0, "Restored stat baseline overflow rejects inheritance");
        policy.Clear();
        policy.Record(Fountain("0"));
        policy.Record(Fountain("-5"));
        Check(!policy.TryPlan(Snapshot(), out plan, out _) && plan.Fountain == null,
            "An absolute target made negative by later commands is rejected, never sign-flipped");
        policy.Clear();
        policy.Record(Stat("luck 0"));
        policy.Record(Stat("luck -5"));
        Check(policy.TryPlan(Snapshot(), out plan, out _) && Write(plan, "LUCK").Raw == 0,
            "Subtract after a zero set switches to the native baseline rather than making a negative absolute target");

        return checks;
    }
}
