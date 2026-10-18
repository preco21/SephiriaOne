using SephiriaOne;

internal static class PlayTagPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; }
        const string roundsPreset = "SephiriaOne preset v30\ntag rounds 3\n";
        Check(SessionPolicy.TryReadPreset(roundsPreset, out var roundsPolicy, out _) && roundsPolicy.ToPresetText() == roundsPreset,
            "Play Tag rounds round-trip in v30 without storing active round state");
        Check(roundsPolicy.PlayTagRounds == 3 && roundsPolicy.HasChanges, "Nondefault rounds participate in dirty policy");
        for (int version = 1; version <= 29; version++)
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out var oldRounds, out _) && oldRounds.PlayTagRounds == 1,
                "Old preset defaults to one round: v" + version);
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\ntag rounds 2\n", out _, out _),
                "Old format cannot silently accept rounds: v" + version);
        }
        foreach (int rounds in new[] { 1, 2, 100 })
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v30\ntag rounds " + rounds + "\n", out var parsed, out _) && parsed.PlayTagRounds == rounds,
                "Accept valid round count " + rounds);
        foreach (string body in new[] { "rounds 0", "rounds -1", "rounds 101", "rounds 1.5", "rounds 2.0", "rounds +2", "rounds 02",
            "rounds 999999999999999999999999999", "rounds 2\ntag rounds 3", "round 1", "rounds reset", "rounds x2" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v30\ntag " + body + "\n", out _, out _), "Reject invalid round row " + body);
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v30\ntag rounds 1\n", out var oneRound, out _) && !oneRound.HasChanges &&
            oneRound.ToPresetText() == "SephiriaOne preset v1\n", "Explicit one-round default uses the oldest compatible format");
        const string mixedRounds = "SephiriaOne preset v30\ntag duration 45\ntag rounds 3\ntag chaser-refill 4\nfriendlyfire hit-frequency 1\n";
        Check(SessionPolicy.TryReadPreset(mixedRounds, out var combinedRounds, out _) && combinedRounds.ToPresetText() == mixedRounds,
            "v30 retains previous timing, chaser and balance fields");
        foreach (int invalid in new[] { 0, -1, 101, int.MaxValue })
        {
            bool rejected = false;
            try { roundsPolicy.RecordPlayTagRounds(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && roundsPolicy.PlayTagRounds == 3, "Policy cannot store invalid round count " + invalid);
        }
        roundsPolicy.Clear();
        Check(roundsPolicy.PlayTagRounds == 1 && !roundsPolicy.HasChanges, "Policy clear resets round count");
        const string chaserPreset = "SephiriaOne preset v26\ntag chaser-refill-enabled 0\ntag chaser-refill 3.25\ntag chaser-damage-enabled 0\ntag chaser-damage 45.5\n";
        Check(SessionPolicy.TryReadPreset(chaserPreset, out var chaserPolicy, out _) &&
            chaserPolicy.ToPresetText() == chaserPreset, "Chaser toggles and numbers round-trip in preset v26");
        var defaultsChaser = default(CombatRoleSettings);
        Check(defaultsChaser.RefillEnabled && defaultsChaser.RefillMultiplier == 2 && defaultsChaser.RefillScale == 2 &&
            defaultsChaser.IncomingEnabled && defaultsChaser.IncomingPercent == 75 && defaultsChaser.IncomingScale == 0.75 &&
            !defaultsChaser.HasChanges && defaultsChaser.RefillNumber == "2" && defaultsChaser.IncomingNumber == "75",
            "Default-safe chaser values enable x2 refill and 75% incoming damage");
        var changedChaser = defaultsChaser.WithRefill(false, 3.25m).WithIncoming(false, 45.5m);
        Check(changedChaser.Equals(new CombatRoleSettings(false, 3.25m, false, 45.5m)) &&
            changedChaser.GetHashCode() == new CombatRoleSettings(false, 3.25m, false, 45.5m).GetHashCode() &&
            changedChaser.RefillScale == 3.25 && changedChaser.IncomingScale == 0.455 && changedChaser.HasChanges,
            "Disabled chaser options retain exact configured values and equality");
        Check(changedChaser.WithRefill(true).WithIncoming(true).RefillMultiplier == 3.25m &&
            changedChaser.WithRefill(true).WithIncoming(true).IncomingPercent == 45.5m,
            "Independent toggles retain both configured numbers");
        Check(changedChaser.WithRefill(true, 2).WithIncoming(true, 75).Equals(defaultsChaser),
            "Restoring both defaults removes chaser changes");
        foreach (decimal value in new[] { 0.1m, 0.11m, 2m, 9.99m, 10m })
            Check(CombatRoleSettings.ValidRefillMultiplier(value), "Accept chaser refill " + value);
        foreach (decimal value in new[] { -1m, 0m, 0.09m, 10.01m, 1.001m, decimal.MaxValue })
        {
            Check(!CombatRoleSettings.ValidRefillMultiplier(value), "Reject chaser refill " + value);
            bool rejected = false;
            try { _ = defaultsChaser.WithRefill(multiplier: value); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid refill cannot enter policy " + value);
        }
        foreach (decimal value in new[] { 0m, 0.01m, 75m, 99.99m, 100m })
            Check(CombatRoleSettings.ValidIncomingPercent(value), "Accept chaser incoming percent " + value);
        foreach (decimal value in new[] { -0.01m, 100.01m, 1.001m, decimal.MaxValue })
        {
            Check(!CombatRoleSettings.ValidIncomingPercent(value), "Reject chaser incoming percent " + value);
            bool rejected = false;
            try { _ = defaultsChaser.WithIncoming(percent: value); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid incoming percent cannot enter policy " + value);
        }
        Check(chaserPolicy.PlayTagChaser.Equals(changedChaser) && chaserPolicy.HasChanges,
            "Preset restores configured chaser intent");
        foreach (string field in new[] { "chaser-refill-enabled", "chaser-refill", "chaser-damage-enabled", "chaser-damage" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v26\ntag " + field + " 1\ntag " + field + " 1\n", out _, out _),
                "Reject duplicate chaser field " + field);
        foreach (string row in new[] { "chaser-refill 0", "chaser-refill 10.01", "chaser-refill 1.001", "chaser-refill 2.00",
            "chaser-refill +2", "chaser-refill 02", "chaser-refill x2", "chaser-damage -1", "chaser-damage 100.01",
            "chaser-damage 1.001", "chaser-damage 75%", "chaser-refill-enabled 2", "chaser-damage-enabled -1",
            "chaser-refill-enabled on", "chaser-damage-enabled 0.0", "carrier 1", "deadline 10", "allowance 2", "chaser-refill reset" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v26\ntag " + row + "\n", out _, out _),
                "Reject invalid or transient v26 row " + row);
        for (int version = 1; version <= 25; version++)
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out var oldChaser, out _) &&
                oldChaser.PlayTagChaser.Equals(defaultsChaser), "Legacy presets use enabled chaser defaults v" + version);
            foreach (string row in new[] { "chaser-refill 3", "chaser-damage 50", "chaser-refill-enabled 0", "chaser-damage-enabled 0" })
                Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\ntag " + row + "\n", out _, out _),
                    "Legacy preset rejects unsupported chaser row v" + version + " " + row);
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v26\ntag chaser-refill-enabled 1\ntag chaser-refill 2\ntag chaser-damage-enabled 1\ntag chaser-damage 75\n",
            out var defaultChaserPolicy, out _) && !defaultChaserPolicy.HasChanges && defaultChaserPolicy.ToPresetText() == "SephiriaOne preset v1\n",
            "Explicit default chaser settings use the lowest compatible preset");
        chaserPolicy.RecordPlayTagDuration(45);
        chaserPolicy.RecordPlayTagChaser(defaultsChaser);
        Check(chaserPolicy.ToPresetText() == "SephiriaOne preset v25\ntag duration 45\n", "Reset chaser settings retains v25 fuse intent");
        chaserPolicy.RecordPlayTagChaser(changedChaser);
        chaserPolicy.Clear();
        Check(!chaserPolicy.HasChanges && chaserPolicy.PlayTagChaser.Equals(defaultsChaser), "Clear restores both chaser options");
        const string preset = "SephiriaOne preset v25\ntag duration 45\n";
        Check(SessionPolicy.TryReadPreset(preset, out var policy, out _), "Play Tag duration accepts preset v25");
        Check(policy.HasChanges && policy.ToPresetText() == preset && policy.DescribeSettings().SequenceEqual(new[] { "tag duration 45" }),
            "Nondefault Play Tag fuse is the only persisted Tag state");
        foreach (string value in new[] { "3", "3600" })
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v25\ntag duration " + value + "\n", out _, out _),
                "Play Tag preset accepts duration bound " + value);
        foreach (string body in new[] { "tag duration 2", "tag duration 3601", "tag duration 30.0", "tag duration +30",
            "tag duration 030", "tag seconds 30", "tag duration 3\ntag duration 4", "tag carrier 1", "tag deadline 10",
            "tag allowance 2", "tag duration reset" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v25\n" + body + "\n", out _, out _),
                "Play Tag preset rejects invalid or transient state: " + body);
        for (int version = 1; version <= 24; version++)
        {
            Check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out var old, out _) &&
                !old.DescribeSettings().Any(x => x.StartsWith("tag ")), "Legacy preset preserves default Tag fuse: v" + version);
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\ntag duration 45\n", out _, out _),
                "Legacy preset rejects unsupported Tag duration: v" + version);
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v25\ntag duration 30\n", out var defaults, out _) &&
            !defaults.HasChanges && defaults.ToPresetText() == "SephiriaOne preset v1\n", "Default Tag fuse uses the existing empty preset");
        policy.Clear();
        Check(!policy.HasChanges && !policy.ToPresetText().Contains("tag"), "Clear restores default Tag fuse");
        const string combined = "SephiriaOne preset v25\ndeathmatch duration 45\ntournament duration 50\ntag duration 60\nfriendlyfire refill 25.5\n";
        Check(SessionPolicy.TryReadPreset(combined, out policy, out _) && policy.ToPresetText() == combined,
            "Tag v25 keeps existing match and PvP refill configuration");
        return checks;
    }
}
