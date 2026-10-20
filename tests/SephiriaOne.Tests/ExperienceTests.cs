using SephiriaOne;

internal static class ExperienceTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        foreach (bool block in new[] { false, true })
        foreach (bool rabbit in new[] { false, true })
        {
            var settings = new ExperienceSettings(block, rabbit);
            Check(settings.Blocks("HolyRabbit") == (block && !rabbit), "Only enabled Rabbit exception permits Rabbit XP");
            Check(settings.Blocks("Mole") == block && settings.Blocks(null!) == block, "Other and unresolved costumes never inherit exemption");
            var policy = new SessionPolicy(); policy.RecordExperience(settings);
            string text = policy.ToPresetText();
            Check(SessionPolicy.TryReadPreset(text, out var parsed, out _) &&
                parsed.Experience.Equals(settings), "XP toggles round trip independently");
            Check(!settings.HasChanges || text.StartsWith("SephiriaOne preset v39\n"), "XP changes require v39");
            policy.Clear();
            Check(!policy.Experience.HasChanges, "Global reset clears XP intent");
        }
        foreach (string invalid in new[] {
            "SephiriaOne preset v38\nxp block 1\n", "SephiriaOne preset v39\nxp block 2\n",
            "SephiriaOne preset v39\nxp block 1\nxp block 0\n", "SephiriaOne preset v39\nxp unknown 1\n",
            "SephiriaOne preset v39\nxp rabbit-exempt 1.0\n" })
            Check(!SessionPolicy.TryReadPreset(invalid, out _, out _), "Reject malformed/version-incompatible XP policy");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v38\n", out var old, out _) &&
            !old.Experience.HasChanges, "Old presets retain native XP by default");
        Check(ExperienceCommand.Parse("/ONE XP BLOCK ON", out var command) == ExperienceParseResult.Valid &&
            default(ExperienceSettings).Apply(command).Block, "Case-insensitive XP commands");
        return checks;
    }
}
