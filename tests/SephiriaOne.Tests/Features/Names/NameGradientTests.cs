using SephiriaOne;

internal static class NameGradientTests
{
    internal static int Run()
    {
        int checks = 0;
        void Equal(string expected, string actual, string scenario)
        {
            if (expected != actual) throw new Exception($"{scenario}: expected '{expected}', got '{actual}'");
            checks++;
        }
        const string one = "<color=#74B1F6>A</color>";
        const string two = "<color=#5A9DF3>A</color><color=#8EC4F8>B</color>";
        const string three = "<color=#5197F3>A</color><color=#74B1F6>B</color><color=#97CAF9>C</color>";
        Equal("", NameGradient.Format(""), "Empty names remain empty");
        Equal(one, NameGradient.Format("A"), "One letter samples the middle of the gradient");
        Equal(two, NameGradient.Format("AB"), "Two letters sample one-quarter and three-quarters");
        Equal(three, NameGradient.Format("ABC"), "Three midpoint colors round RGB channels upwards at halves");
        Equal("  <color=#5A9DF3>A</color> <color=#8EC4F8>B</color> ", NameGradient.Format("  A B "), "Spaces preserved without color steps");
        Equal(" \t\n", NameGradient.Format(" \t\n"), "Whitespace-only name stays unchanged");
        Equal("<color=#5A9DF3>프</color><color=#8EC4F8>레</color>", NameGradient.Format("프레"), "Korean letters get separate colors");
        Equal("<color=#5A9DF3>🐇</color><color=#8EC4F8>A</color>", NameGradient.Format("🐇A"), "Surrogate pair stays together");
        Equal("<color=#5A9DF3>e\u0301</color><color=#8EC4F8>A</color>", NameGradient.Format("e\u0301A"), "Combining accent stays with base letter");
        Equal("<b>" + two + "</b>", NameGradient.Format("<b>AB</b>"), "Style tags remain intact and do not consume steps");
        Equal("<color=red>" + two + "</color>", NameGradient.Format("<color=red>AB</color>"), "Existing tags stay outside per-letter color overrides");
        Equal(two, NameGradient.Format(two), "Repeated formatting is idempotent");
        Equal("AB", NameGradient.Plain(two), "Own gradient can be removed");
        Equal("<b>AB</b>", NameGradient.Plain("<b>" + two + "</b>"), "Removal preserves unrelated markup");
        Equal("<color=red>AB</color>", NameGradient.Plain("<color=red>" + two + "</color>"), "Removal preserves unrelated color tag");
        Equal("<color=#123456>A</color>", NameGradient.Plain("<color=#123456>A</color>"), "Different color is not ours");
        Equal("<color=#5A9DF3>A</color><color=#000000>B</color>", NameGradient.Plain("<color=#5A9DF3>A</color><color=#000000>B</color>"), "Partial matching gradient is not stripped");
        Equal(two, NameGradient.Format("<color=#0000FF>AB</color>"), "Upgrade removes old blue wrapper");
        Equal("AB", NameGradient.Plain("<color=#0000FF><color=#0000FF>AB</color></color>"), "Legacy nested wrappers normalize");
        Equal("AB", NameGradient.Plain("<color=#0000FF>" + two + "</color>"), "Legacy wrapper around gradient normalizes");
        Equal("<b>AB</b>", NameGradient.Plain("<b>AB</b>"), "Plain styled name untouched");
        string literalAngle = NameGradient.Format("A<B");
        Equal("A<B", NameGradient.Plain(literalAngle), "Literal angle bracket survives round-trip");
        Equal(literalAngle, NameGradient.Format(literalAngle), "Literal angle bracket does not cause nesting");
        string foreignHex = "<color=#123456>AB</color>";
        Equal(foreignHex, NameGradient.Plain(NameGradient.Format(foreignHex)), "Foreign hexadecimal color wrapper is retained");
        Equal("<noparse>AB</noparse>", NameGradient.Format("<noparse>AB</noparse>"), "Do not expose generated markup in no-parse text");
        Equal("<noparse>AB", NameGradient.Format("<noparse>AB"), "Unclosed no-parse block also stays intact");
        const string angles = "<color=#5197F3><</color><color=#74B1F6>x</color><color=#97CAF9>></color>";
        Equal(angles, NameGradient.Format("<x>"), "Unknown tags are visible letters in TMP");
        Equal("<x>", NameGradient.Plain(angles), "Literal paired angles round-trip");
        string escapedTags = "<noparse><color=#123456>A</color></noparse>B";
        string escapedGradient = NameGradient.Format(escapedTags);
        Equal(escapedTags, NameGradient.Plain(escapedGradient), "Canonical removal skips literal no-parse colors");
        Equal(escapedGradient, NameGradient.Format(escapedGradient), "No-parse colors cannot trigger nested gradients");

        return checks;
    }
}
