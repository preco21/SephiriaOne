using SephiriaOne;

internal static class PvpDotDurationTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool valid, string why) { if (!valid) throw new Exception(why); count++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire dot-duration 1.2", out var command) == FriendlyFireParseResult.Valid,
            "Shared PvP duration command accepts the new 1.2-second default");
        var defaults = default(FriendlyFireSettings);
        Check(defaults.DotMaxDurationSeconds == 1.2m && defaults.DotMaxDuration == 1.2f && defaults.DotMaxDurationNumber == "1.2" &&
            !defaults.HasDotDurationChanges && !defaults.HasChanges && defaults.Equals(new FriendlyFireSettings(false, 100)), "Default struct and constructor use 1.2 seconds");
        var policy = new SessionPolicy();
        foreach (string value in new[] { "0.05", "0.75", "1.2", "2", "3.33", "10" })
        {
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE DOT-DURATION " + value, out command) == FriendlyFireParseResult.Valid && !command.TouchesMasterToggle,
                "Duration uses shared case-insensitive parser without touching master");
            policy.Record(command);
            var settings = policy.FriendlyFire;
            Check(settings.DotMaxDurationNumber == value && !settings.Enabled && !settings.Balance &&
                settings.WithEnabled(true).DotMaxDurationSeconds == settings.DotMaxDurationSeconds, "Configured duration preserves off state and mode overlays");
            string saved = policy.ToPresetText();
            Check(SessionPolicy.TryReadPreset(saved, out var restored, out _) && restored.FriendlyFire.Equals(settings), "Duration saves and restores exactly");
            Check(value == "1.2" ? !policy.HasChanges : saved == "SephiriaOne preset v46\nfriendlyfire dot-duration " + value + "\n", "Only nondefault duration requires v46");
        }
        foreach (string invalid in new[] { "0", "-1", "0.049", "10.01", "1.201", "1.200", "NaN", "Infinity", "1,2", "1e0", "x2", "+2", "1%", "reset extra", "" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire dot-duration " + invalid, out _) == FriendlyFireParseResult.Invalid, "Reject malformed duration: " + invalid);
        foreach (decimal invalid in new[] { 0m, -1m, 10.01m, 1.201m, decimal.MaxValue })
        {
            bool rejected = false;
            try { _ = new FriendlyFireSettings(true, 100, dotMaxDurationSeconds: invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Typed settings reject invalid duration");
        }
        foreach (var edit in new[] { new FriendlyFireCommand(true), new FriendlyFireCommand(false, balance: true),
            new FriendlyFireCommand(false, dot: new(PvpDotEffect.Burn, 0.5m)), new FriendlyFireCommand(false, dotSchedule: new(PvpDotEffect.PlasmaBurn, false)),
            new FriendlyFireCommand(false, perfectGuard: new(true, 3)), new FriendlyFireCommand(false, rapid: new(windowSeconds: 2)),
            new FriendlyFireCommand(false, guardMpPercent: 8), new FriendlyFireCommand(false, amplification: new(true, 50, 100)) })
        {
            policy.Record(edit);
            Check(policy.FriendlyFire.DotMaxDurationSeconds == 10, "Sibling commands preserve custom duration");
        }
        var before = policy.FriendlyFire;
        Check(FriendlyFireCommand.Parse("/one friendlyfire dot-duration reset", out command) == FriendlyFireParseResult.Valid && command.IsReset && !command.TouchesMasterToggle,
            "Duration reset is a recovery edit, not a master reset");
        policy.Record(command);
        Check(policy.FriendlyFire.DotMaxDurationSeconds == 1.2m && policy.FriendlyFire.DotIntervals.Equals(before.DotIntervals) &&
            policy.FriendlyFire.PerfectGuard.Equals(before.PerfectGuard) && policy.FriendlyFire.Rapid.Equals(before.Rapid) &&
            policy.FriendlyFire.Enabled == before.Enabled && policy.FriendlyFire.Balance == before.Balance, "Duration reset preserves sibling controls");
        for (int version = 1; version <= 46; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && old.FriendlyFire.DotMaxDurationSeconds == 1.2m,
                "Missing duration inherits current default in schema " + version);
            if (version < 46)
                Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire dot-duration 2\n", out _, out _), "Old schemas reject new row");
        }
        foreach (string row in new[] { "dot-duration 0", "dot-duration 11", "dot-duration 1.200", "dot-duration 1.001", "dot-duration x2",
            "dot-duration NaN", "DOT-DURATION 2", "dot-duration 2\nfriendlyfire dot-duration 3" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v46\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) && !bad.HasChanges,
                "Malformed duration preset rejects atomically: " + row);
        policy.Record(new FriendlyFireCommand(false, dotMaxDurationSeconds: 3));
        policy.Record(new FriendlyFireCommand(false, reset: true));
        Check(!policy.HasChanges && policy.FriendlyFire.DotMaxDurationSeconds == 1.2m, "Full reset restores duration default");
        return count;
    }
}
