using SephiriaOne;

internal static class HitFrequencySettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        Check(FriendlyFireCommand.Parse("/one friendlyfire hit-frequency on", out var command) == FriendlyFireParseResult.Valid &&
            !command.TouchesMasterToggle && !command.IsReset, "Frequency toggle is accepted without touching the combat master");
        var policy = new SessionPolicy(); policy.Record(command);
        Check(policy.FriendlyFire.HitFrequency && !policy.FriendlyFire.Enabled && !policy.FriendlyFire.Balance,
            "Independent preference does not enable either prerequisite");
        Check(policy.FriendlyFire.WithEnabled(true).HitFrequency && policy.FriendlyFire.WithEnabled(false).HitFrequency,
            "Temporary match overlays preserve frequency preference");
        Check(policy.ToPresetText().StartsWith("SephiriaOne preset v29\n") &&
            policy.ToPresetText().Contains("friendlyfire hit-frequency 1\n"), "Frequency preference is saved under v29");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var loaded, out _) &&
            loaded.FriendlyFire.Equals(policy.FriendlyFire), "Frequency preference roundtrips without enabling combat");
        foreach (string value in new[] { "OFF", "RESET" })
        {
            Check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE HIT-FREQUENCY " + value, out var off) == FriendlyFireParseResult.Valid &&
                off.IsReset && !off.TouchesMasterToggle, "Off/reset remain safe while a match is running");
            policy.Record(off); Check(!policy.HasChanges, "Off/reset restore default and require no saved row");
            policy.Record(command);
        }
        foreach (string value in new[] { "", "true", "1", "on extra", "reset extra" })
            Check(FriendlyFireCommand.Parse("/one friendlyfire hit-frequency " + value, out _) == FriendlyFireParseResult.Invalid, "Invalid frequency command rejected");
        for (int version = 1; version <= 28; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.HasChanges, "Old preset keeps default off");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire hit-frequency 1\n", out _, out _), "Older schema rejects new row");
        }
        foreach (string row in new[] { "hit-frequency 2", "hit-frequency 1.0", "hit-frequency -1", "hit-frequency 1\nfriendlyfire hit-frequency 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v29\nfriendlyfire enabled 1\nfriendlyfire " + row + "\n", out var bad, out _) &&
                !bad.HasChanges, "Malformed preset remains atomic");
        foreach (var edit in new FriendlyFireCommand[] { new(false), new(true), new(false, percent: 0.5m), new(false, balance: true),
            new(false, blockLobbyRegen: true), new(false, refillPercent: 125), new(false, guardMpPercent: 15),
            new(false, dot: new(PvpDotEffect.Burn, 0.25m)) })
        {
            policy.Record(edit);
            Check(policy.ToPresetText().Contains("friendlyfire hit-frequency 1\n"), "Sibling edits preserve frequency preference");
        }
        policy.Record(new FriendlyFireCommand(false, reset: true)); Check(!policy.HasChanges, "Full combat reset restores default off");
        return checks;
    }
}
