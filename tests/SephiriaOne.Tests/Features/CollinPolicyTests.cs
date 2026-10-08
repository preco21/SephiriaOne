using SephiriaOne;

internal static class CollinPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.CollinStartingArtifact && !policy.HasChanges, "Collin defaults off");
        Check(CollinCommand.Parse("/ONE COLLIN ON", out bool enabled) == CollinParseResult.Valid && enabled, "Case insensitive Collin toggle");
        foreach (string text in new[] { "/one collin reset", "/one collin off" })
            Check(CollinCommand.Parse(text, out enabled) == CollinParseResult.Valid && !enabled, "Collin reset/off");
        Check(CollinCommand.Parse("/one collin", out _) == CollinParseResult.Help && CollinCommand.Parse("/one collin status", out _) == CollinParseResult.Status, "Collin help/status");
        foreach (string text in new[] { "/one collin 1", "/one collin x2", "/one collin reset extra", "/one collin status extra" })
            Check(CollinCommand.Parse(text, out _) == CollinParseResult.Invalid, "Malformed Collin command rejected");
        Check(CollinCommand.Parse("/mod collin on", out _) == CollinParseResult.NotCommand, "No generic namespace interception");
        policy.RecordCollin(true); policy.RecordBat(true);
        string preset = policy.ToPresetText();
        Check(preset.StartsWith("SephiriaOne preset v15\n") && preset.Contains("collin starting 1\n"), "Collin uses v15");
        Check(SessionPolicy.TryReadPreset(preset, out var restored, out _) && restored.CollinStartingArtifact && restored.BatHpSteal, "Collin and Bat preset roundtrip");
        foreach (string row in new[] { "starting 2", "starting -1", "starting 1.0", "starting 01", "other 1", "starting 1\ncollin starting 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v15\ncollin " + row + "\n", out var invalid, out _) && !invalid.HasChanges, "Malformed Collin preset rejected atomically");
        Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v14\ncollin starting 1\n", out _, out _), "Old schemas reject Collin row");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v14\nbat hp-steal 1\n", out restored, out _) && !restored.CollinStartingArtifact, "Old preset keeps Collin off");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v15\ncollin starting 0\n", out restored, out _) && !restored.HasChanges, "Explicit off normalizes away");
        policy.RecordCollin(false); Check(policy.ToPresetText().StartsWith("SephiriaOne preset v14\n"), "Off restores lowest schema for remaining settings");
        policy.RecordCollin(true); policy.Clear(); Check(!policy.CollinStartingArtifact && !policy.HasChanges, "Scope clear resets Collin intent");
        return checks;
    }
}
