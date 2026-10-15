using System.Text;
using SephiriaOne;

internal static class CombatNameTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var (input, expected) in new (string, string)[]
        {
            (null, "?"), ("", "?"), ("Alice", "Alice"), ("프레코 🐇", "프레코 🐇"),
            (" \t\r\n", "?"), ("\u00a0Alice\u3000", "Alice"), ("A\0B\nC", "ABC"),
            ("<color=blue>Alice</color>", "Alice"), ("A<unfinished", "A"),
            ("A>BC", "ABC"), ("A<<tag>B", "AB"), ("<tag><nested>", "?"),
            (new string(' ', 32) + "Alice", "?"), (new string('A', 31) + " B", new string('A', 31)),
            (new string('A', 31) + "\nB", new string('A', 31) + "B"),
            (new string('A', 31) + "<b>B</b>C", new string('A', 31) + "B"),
            (new string('A', 32) + "<discarded>\n", new string('A', 32)),
            (new string('A', 31) + "🐇", new string('A', 31) + "\ud83d")
        })
            check(CombatNames.Safe(input) == expected, "Combat name filtering preserves its 32 UTF-16-unit limit and trim order");

        // Differential coverage pins the existing permissive tag rules, including
        // malformed markup and surrogate boundaries; this is not a new sanitizer.
        var random = new Random(540);
        const string chars = "AB 한글<>/ \t\n\0\u00a0\u2003\u200b\ud83d\udc07";
        for (int i = 0; i < 2000; i++)
        {
            var input = new char[random.Next(0, 150)];
            for (int j = 0; j < input.Length; j++) input[j] = chars[random.Next(chars.Length)];
            string name = new string(input);
            check(CombatNames.Safe(name) == PreviousFilter(name), "Combat names retain previous filtering for randomized Unicode/markup");
        }
        string[] plain = { "Alice", "프레코 🐇", new string('A', 32), "?", "" };
        for (int i = 0; i < 1000; i++) foreach (string name in plain) CombatNames.Safe(name);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) foreach (string name in plain) CombatNames.Safe(name);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Plain combat names: {bytes / 5000d:F0} bytes/name");
        check(bytes == 0, "Already plain combat names must not allocate a buffer or replacement string");
    }

    private static string PreviousFilter(string name)
    {
        var text = new StringBuilder(32); bool tag = false;
        foreach (char c in name)
        {
            if (c == '<') { tag = true; continue; }
            if (c == '>') { tag = false; continue; }
            if (!tag && !char.IsControl(c) && text.Length < 32) text.Append(c);
        }
        string plain = text.ToString().Trim(); return plain.Length == 0 ? "?" : plain;
    }
}
