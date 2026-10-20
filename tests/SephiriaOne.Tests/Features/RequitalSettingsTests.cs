using SephiriaOne;

internal static class RequitalSettingsTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var policy = new SessionPolicy();
        Check(!policy.Requital.Automatic && policy.Requital.Count == 1 && !policy.HasChanges, "Default auto off, count one");
        Check(RequitalCommand.Parse("/ONE SLATES AUTO ON", out var command) == RequitalParseResult.Valid, "Case insensitive command");
        policy.RecordRequital(policy.Requital.Apply(command));
        Check(policy.Requital.Automatic && policy.Requital.Count == 1, "Enable preserves count");
        for (int count = 0; count <= RequitalSettings.MaximumCount; count++)
        {
            foreach (bool automatic in new[] { false, true })
            {
                Check(RequitalCommand.Parse("/one slates count " + count, out command) == RequitalParseResult.Valid, "Valid count parsed");
                var setting = new RequitalSettings(automatic, 7).Apply(command);
                Check(setting.Count == count && setting.Automatic == automatic, "Count never changes auto toggle");
                policy.RecordRequital(setting);
                Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var saved, out _) &&
                    saved.Requital.Equals(setting), "Slate policy roundtrips at every valid value");
                Check(saved.FriendlyFire.InfiniteHpPotions && !saved.CollinStartingArtifact && !saved.ItemUnlock,
                    "Slate policy isolated from other gifts and restrictions");
                Check(RequitalCommand.Parse("/one slates auto reset", out command) == RequitalParseResult.Valid &&
                    !setting.Apply(command).Automatic && setting.Apply(command).Count == count, "Auto reset retains configured count");
                Check(RequitalCommand.Parse("/one slates count reset", out command) == RequitalParseResult.Valid &&
                    setting.Apply(command).Count == 1 && setting.Apply(command).Automatic == automatic, "Count reset retains toggle");
                Check(RequitalCommand.Parse("/one slates reset", out command) == RequitalParseResult.Valid &&
                    !setting.Apply(command).HasChanges, "Full reset restores defaults");
            }
        }
        foreach (string text in new[] { "/one slates count", "/one slates count -1", "/one slates count 21", "/one slates count 1.5",
            "/one slates count x2", "/one slates auto 1", "/one slates give all", "/one slates reset extra" })
            Check(RequitalCommand.Parse(text, out _) == RequitalParseResult.Invalid, "Reject invalid Slate action");
        Check(RequitalCommand.Parse("/one slates give", out _) == RequitalParseResult.Give, "Manual grant is an action, not saved intent");
        Check(RequitalCommand.Parse("/one slates status", out _) == RequitalParseResult.Status, "Status is read-only");
        Check(RequitalCommand.Parse("/one slates", out _) == RequitalParseResult.Help, "Bare command gives help");
        Check(RequitalCommand.Parse("/one slate auto on", out _) == RequitalParseResult.NotCommand, "Unrelated command ignored");
        for (int version = 1; version < 38; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var saved, out _) && !saved.Requital.HasChanges,
                "Legacy presets inherit inactive Slate defaults");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nslates auto 1\n", out var failed, out _) && !failed.HasChanges,
                "Legacy schemas reject new feature atomically");
        }
        foreach (string rows in new[] { "slates count -1", "slates count 21", "slates count 1.0", "slates count 01",
            "slates count 1\nslates count 2", "slates auto 1\nslates auto 0", "slates auto true", "slates give 1" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v38\nfriendlyfire enabled 1\n" + rows + "\n", out var failed, out _) &&
                !failed.HasChanges, "Malformed Slate preset rejected atomically");
        policy.Clear();
        Check(!policy.Requital.HasChanges && !policy.HasChanges, "Session clear removes all Slate intent");
        return checks;
    }
}
