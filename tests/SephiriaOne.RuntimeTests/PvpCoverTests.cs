using SephiriaOne;

internal static class PvpCoverTests
{
    internal static void Run(Action<bool, string> check)
    {
        var policy = new SessionPolicy();
        check(!policy.FriendlyFire.Cover.Enabled && policy.FriendlyFire.Cover.Count == 20, "Cover defaults off / 20");
        foreach (string text in new[] { "on", "off", "reset", "count 1", "count 32", "count reset" })
        {
            check(FriendlyFireCommand.Parse("/one friendlyfire cover " + text, out var command) == FriendlyFireParseResult.Valid, "Cover parses " + text);
            check(!command.TouchesMasterToggle, "Cover edits do not stop a match");
        }
        foreach (string text in new[] { "count 0", "count 33", "count -1", "count 2.5", "count +2", "on extra", "count", "" })
            check(FriendlyFireCommand.Parse("/one friendlyfire cover " + text, out _) == FriendlyFireParseResult.Invalid, "Invalid cover syntax: " + text);
        policy.Record(new FriendlyFireCommand(false, cover: new PvpCoverChange(true, 25)));
        check(policy.FriendlyFire.WithEnabled(true).Cover.Count == 25, "Match overlay preserves count");
        string preset = policy.ToPresetText();
        check(preset.StartsWith("SephiriaOne preset v56\n") && preset.Contains("friendlyfire cover 1\n") && preset.Contains("friendlyfire cover-count 25\n"), "Sparse v56 cover");
        check(SessionPolicy.TryReadPreset(preset, out var parsed, out _) && parsed.FriendlyFire.Cover.Enabled && parsed.FriendlyFire.Cover.Count == 25, "Cover preset round trip");
        foreach (string row in new[] { "cover 2", "cover-count 0", "cover-count 33", "cover-count 1.0", "cover-count 01", "cover-count 4\nfriendlyfire cover-count 5" })
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v56\nfriendlyfire " + row + "\n", out _, out _), "Malformed cover preset rejected");
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v55\n", out parsed, out _) && !parsed.FriendlyFire.Cover.Enabled, "Old preset keeps cover off");
        check(!SessionPolicy.TryReadPreset("SephiriaOne preset v55\nfriendlyfire cover 1\n", out _, out _), "Old schema rejects cover row");
        policy.Record(new FriendlyFireCommand(false, cover: new PvpCoverChange(reset: true)));
        check(!policy.FriendlyFire.Cover.HasChanges && policy.FriendlyFire.Cover.Count == 20, "Cover reset restores defaults");

        var rooms = new List<PvpCoverPlacement.Room> { new(0, 0, 50, 50), new(100, 100, 150, 150) };
        var plan = new PvpCoverPlacement(rooms, 17);
        var points = new List<(float X, float Y)>();
        int probes = 0;
        while (points.Count < 32 && !plan.Exhausted)
            if (plan.TryNext((x, y) => { probes++; return x < 60 || y > 110; }, out float x, out float y))
            {
                check(x >= 1 && x <= 49 || x >= 101 && x <= 149, "Reserve room boundary");
                check(x < 60 || y > 110, "Native safety veto respected");
                foreach (var p in points) check(Math.Abs(p.X - x) >= 1.55f || Math.Abs(p.Y - y) >= 1.2f, "Compact cover envelopes never overlap");
                points.Add((x, y));
            }
        check(points.Count == 32 && probes <= PvpCoverPlacement.MaximumAttempts, "Bounded spread can fill spacious rooms");
        check(points.Any(p => p.X < 60) && points.Any(p => p.X > 100), "Cover distributed across rooms");
        plan = new PvpCoverPlacement(new() { new(0, 0, 6, 6) }, 17);
        check(plan.TryNext((x, y) => true, out _, out _), "Compact props can fit a small open room without requiring an 8x8 room");
        int packed = 0;
        while (!plan.Exhausted) if (plan.TryNext((x, y) => true, out _, out _)) packed++;
        check(packed >= 3, "Compact footprint admits at least four props in a 6x6 room instead of one");
        plan = new PvpCoverPlacement(rooms, 3); probes = 0;
        while (!plan.Exhausted) check(!plan.TryNext((x, y) => { probes++; return false; }, out _, out _), "Unsafe room never spawns");
        check(probes == PvpCoverPlacement.MaximumAttempts && !plan.TryNext((x, y) => throw new Exception("extra probe"), out _, out _), "Search stops at hard attempt budget");
        plan = new PvpCoverPlacement(new() { new(0, 0, 1, 1) }, 1);
        while (!plan.Exhausted) check(!plan.TryNext((x, y) => throw new Exception("narrow corridor"), out _, out _), "Rooms smaller than a prop are never probed");
    }
}
