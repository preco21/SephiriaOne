using SephiriaOne;

internal static class BatPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.BatHpSteal && !policy.HasChanges, "Bat defaults off");
        Check(BatCommand.Parse("/ONE BAT HP-STEAL ON", out bool enabled) == BatParseResult.Valid && enabled, "Case insensitive Bat toggle");
        foreach (string text in new[] { "/one bat reset", "/one bat hp-steal off" })
            Check(BatCommand.Parse(text, out enabled) == BatParseResult.Valid && !enabled, "Bat reset/off");
        Check(BatCommand.Parse("/one bat", out _) == BatParseResult.Help && BatCommand.Parse("/one bat status", out _) == BatParseResult.Status, "Bat help/status");
        foreach (string text in new[] { "/one bat hp-steal", "/one bat hp-steal 1", "/one bat hp-steal x2", "/one bat reset extra", "/one bat status extra" })
            Check(BatCommand.Parse(text, out _) == BatParseResult.Invalid, "Malformed Bat command rejected");
        Check(BatCommand.Parse("/mod bat hp-steal on", out _) == BatParseResult.NotCommand, "No generic namespace interception");
        policy.RecordBat(true); policy.RecordJarSpawns(new JarSpawnSettings(JarSpawnMode.Multiplier, 2));
        string preset = policy.ToPresetText();
        Check(preset.StartsWith("SephiriaOne preset v14\n") && preset.Contains("bat hp-steal 1\n"), "Bat uses v14");
        Check(SessionPolicy.TryReadPreset(preset, out var restored, out _) && restored.BatHpSteal && restored.JarSpawns.Value == 2, "Bat and Jar preset roundtrip");
        foreach (string row in new[] { "hp-steal 2", "hp-steal -1", "hp-steal 1.0", "hp-steal 01", "other 1", "hp-steal 1\nbat hp-steal 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v14\nbat " + row + "\n", out var invalid, out _) && !invalid.HasChanges, "Malformed Bat preset rejected atomically");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v13\nbat hp-steal 1\n", out _, out _), "Old schemas reject Bat row");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v13\njars chance 50\n", out restored, out _) && !restored.BatHpSteal, "Old preset keeps Bat off");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v14\nbat hp-steal 0\n", out restored, out _) && !restored.HasChanges, "Explicit off normalizes away");
        policy.RecordBat(false); Check(policy.ToPresetText().StartsWith("SephiriaOne preset v13\n"), "Off restores lowest schema for remaining settings");
        policy.RecordBat(true); policy.Clear(); Check(!policy.BatHpSteal && !policy.HasChanges, "Scope clear resets Bat intent");
        return checks;
    }
}
