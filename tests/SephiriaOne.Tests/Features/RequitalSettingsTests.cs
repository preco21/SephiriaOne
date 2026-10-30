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
                Check(!saved.FriendlyFire.InfiniteHpPotions && !saved.CollinStartingArtifact && !saved.ItemUnlock,
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
        Check(RequitalCommand.Parse("/one slates type HEAVEN", out command) == RequitalParseResult.Valid,
            "Host can select Heaven by name");
        var heaven = new RequitalSettings(true, 3).Apply(command);
        Check(heaven.Kind == SlateKind.Heaven && heaven.Automatic && heaven.Count == 3,
            "Selection preserves the automatic toggle and count");
        foreach (string change in new[] { "/one slates count 2", "/one slates auto off", "/one slates count reset", "/one slates auto reset" })
        {
            Check(RequitalCommand.Parse(change, out command) == RequitalParseResult.Valid &&
                heaven.Apply(command).Kind == SlateKind.Heaven, "Other Slate edits preserve the selected type");
        }
        foreach (var kind in new[] { SlateKind.Requital, SlateKind.Heaven })
        foreach (bool auto in new[] { false, true })
        {
            policy.RecordRequital(new RequitalSettings(auto, 3, kind));
            Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var roundtrip, out _) &&
                roundtrip.Requital.Equals(policy.Requital), "Selected type roundtrips with either automatic state");
        }
        Check(RequitalCommand.Parse("/one slates type reset", out command) == RequitalParseResult.Valid &&
            heaven.Apply(command).Equals(new RequitalSettings(true, 3)), "Type reset selects Requital without changing count or toggle");
        foreach (string type in new[] { "hell", "2099", "1", "heaven extra" })
            Check(RequitalCommand.Parse("/one slates type " + type, out _) == RequitalParseResult.Invalid,
                "Only audited named Slate choices are accepted");
        foreach (string rows in new[] { "slates type 2", "slates type -1", "slates type 01", "slates type heaven",
            "slates type 1\nslates type 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v40\n" + rows + "\n", out var rejected, out _) && !rejected.HasChanges,
                "Invalid type rows fail atomically");
        foreach (int version in new[] { 38, 39 })
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nslates auto 1\nslates count 3\n",
                out var legacy, out _) && legacy.Requital.Equals(new RequitalSettings(true, 3)),
                "Existing saved loadouts retain Requital");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nslates type 1\n", out _, out _),
                "Older schemas reject a selection field they cannot represent");
        }
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v40\nslates type 1\nslates auto 1\nslates count 2\n",
            out var reordered, out _) && reordered.Requital.Equals(new RequitalSettings(true, 2, SlateKind.Heaven)),
            "Preset row order cannot erase a selected type");
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
