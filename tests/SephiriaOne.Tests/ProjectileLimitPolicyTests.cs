using SephiriaOne;

internal static class ProjectileLimitPolicyTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        foreach (string text in new[] { "/one projectiles rate on", "/ONE PROJECTILES RATE OFF", "/one projectiles rate reset",
            "/one projectiles rate total 1", "/one projectiles rate source 4096" })
            Check(ProjectileLimitCommand.Parse(text, out _) == ProjectileLimitParseResult.Valid, "Valid independent rate command: " + text);
        foreach (string text in new[] { "/one projectiles rate", "/one projectiles rate on extra", "/one projectiles rate total 0",
            "/one projectiles rate total 4097", "/one projectiles rate source -1", "/one projectiles rate source 1.5",
            "/one projectiles rate total +2", "/one projectiles rate source default 2", "/one projectiles rate source meteor 2" })
            Check(ProjectileLimitCommand.Parse(text, out _) == ProjectileLimitParseResult.Invalid, "Reject invalid rate command: " + text);
        const string ratePreset = "SephiriaOne preset v50\nprojectiles total 40\nprojectiles source liberation 6\nprojectiles rate-enabled 1\nprojectiles rate-total 80\nprojectiles rate-source 20\n";
        Check(SessionPolicy.TryReadPreset(ratePreset, out var ratePolicy, out _) && ratePolicy.ToPresetText() == ratePreset,
            "Independent rates and active overrides round trip in v50");
        ProjectileLimitCommand.Parse("/one projectiles rate reset", out var rateReset);
        ratePolicy.RecordProjectiles(rateReset.Apply(ratePolicy.Projectiles));
        Check(ratePolicy.ToPresetText() == "SephiriaOne preset v42\nprojectiles total 40\nprojectiles source liberation 6\n",
            "Rate reset preserves active caps and downgrades the schema");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v50\nprojectiles rate-total 80\n", out ratePolicy, out _) &&
            ratePolicy.ToPresetText() == "SephiriaOne preset v50\nprojectiles rate-total 80\n", "Disabled custom rates persist without enabling active caps");
        foreach (string bad in new[] { "projectiles rate-enabled 2", "projectiles rate-enabled 01", "projectiles rate-total 0",
            "projectiles rate-source 4097", "projectiles rate-source 1.0", "projectiles rate-source +2",
            "projectiles rate-total 12\nprojectiles rate-total 13", "projectiles rate-enabled 1\nprojectiles rate-enabled 0" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v50\nprojectiles total 30\n" + bad + "\n", out var rejectedRate, out _) &&
                !rejectedRate.HasChanges, "Invalid rate presets fail atomically: " + bad);
        for (int version = 1; version <= 49; version++)
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nprojectiles rate-total 80\n", out _, out _),
                "Old schemas reject rate rows");
        const string preset = "SephiriaOne preset v42\nprojectiles enabled 1\nprojectiles total 64\nprojectiles default 16\nprojectiles source liberation 8\n";
        Check(SessionPolicy.TryReadPreset(preset, out var policy, out _), "Projectile limits load as v42");
        Check(policy.ToPresetText() == "SephiriaOne preset v42\nprojectiles enabled 1\nprojectiles default 16\nprojectiles source liberation 8\n",
            "Explicit saved 16 and source overrides survive the lower default");
        var defaults = default(ProjectileLimitSettings);
        Check(!defaults.RateEnabled && defaults.PlayerRate == 64 && defaults.SourceRate == 12 && !defaults.HasRateChanges,
            "Rate defaults are off with one-second budgets of 64/player and 12/source");
        var rated = new ProjectileLimitSettings(true, 40, 10, new Dictionary<string, int> { ["liberation"] = 6 }, true, 80, 20);
        foreach (var activeEdit in new[] { rated.WithEnabled(false), rated.WithPlayerLimit(41), rated.WithDefaultSourceLimit(11),
            rated.WithSourceLimit("meteor", 7), rated.WithoutSourceLimit("liberation") })
            Check(activeEdit.RateEnabled && activeEdit.PlayerRate == 80 && activeEdit.SourceRate == 20,
                "Every active-cap mutation preserves all rate fields");
        var rateEdited = rated.WithRateEnabled(false).WithPlayerRate(81).WithSourceRate(21);
        Check(rateEdited.Enabled && rateEdited.PlayerLimit == 40 && rateEdited.DefaultSourceLimit == 10 &&
            rateEdited.GetSourceLimit("liberation") == 6 && !rateEdited.RateEnabled && rateEdited.PlayerRate == 81 && rateEdited.SourceRate == 21,
            "Rate edits preserve active caps and source overrides");
        Check(rated.Equals(new ProjectileLimitSettings(true, 40, 10, new Dictionary<string, int> { ["liberation"] = 6 }, true, 80, 20)) &&
            !rated.Equals(rated.WithRateEnabled(false)) && !rated.Equals(rated.WithPlayerRate(81)) && !rated.Equals(rated.WithSourceRate(21)),
            "Equality observes all independent rate fields");
        Check(rated.GetHashCode() == new ProjectileLimitSettings(true, 40, 10,
            new Dictionary<string, int> { ["liberation"] = 6 }, true, 80, 20).GetHashCode(), "Equal rate settings have equal hashes");
        foreach (int invalid in new[] { 0, -1, 4097, int.MaxValue })
        {
            bool playerRejected = false, sourceRejected = false;
            try { defaults.WithPlayerRate(invalid); } catch (ArgumentOutOfRangeException) { playerRejected = true; }
            try { defaults.WithSourceRate(invalid); } catch (ArgumentOutOfRangeException) { sourceRejected = true; }
            Check(playerRejected && sourceRejected, "Rate constructors reject values outside 1..4096");
        }
        for (int version = 1; version <= 49; version++)
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var oldRatePolicy, out _) &&
                !oldRatePolicy.Projectiles.RateEnabled && oldRatePolicy.Projectiles.PlayerRate == 64 && oldRatePolicy.Projectiles.SourceRate == 12,
                "All previous preset schemas retain independent rate defaults");
        Check(rateReset.Apply(rated).Equals(new ProjectileLimitSettings(true, 40, 10, rated.SourceLimits)),
            "Rate reset resets exactly the three rate fields");
        Check(!defaults.Enabled && defaults.PlayerLimit == 64 && defaults.DefaultSourceLimit == 12 &&
            !defaults.HasChanges && defaults.GetSourceLimit("liberation") == 12, "Safe default settings");
        var input = new Dictionary<string, int> { ["liberation"] = 8 };
        var settings = new ProjectileLimitSettings(true, 48, 12, input);
        input["liberation"] = 999;
        Check(settings.GetSourceLimit("liberation") == 8, "Constructor copies caller overrides");
        Check(settings.SourceLimits is not Dictionary<string, int>, "Overrides are not externally mutable");
        bool writeBlocked = false;
        try { ((IDictionary<string, int>)settings.SourceLimits)["liberation"] = 2; }
        catch (NotSupportedException) { writeBlocked = true; }
        Check(writeBlocked, "Snapshot overrides reject writes");
        var changed = settings.WithSourceLimit("meteor", 7).WithDefaultSourceLimit(9).WithEnabled(false);
        Check(changed.GetSourceLimit("liberation") == 8 && changed.GetSourceLimit("meteor") == 7 &&
            settings.GetSourceLimit("meteor") == 12 && settings.Enabled, "Edits preserve earlier immutable snapshots");
        Check(changed.WithoutSourceLimit("liberation").GetSourceLimit("liberation") == 9 &&
            changed.GetSourceLimit("liberation") == 8, "Source reset resumes current default without mutating old settings");
        foreach (var source in ProjectileLimitCatalog.All)
        {
            Check(ProjectileLimitCommand.Parse("/ONE PROJECTILES SOURCE " + source.Id.ToUpperInvariant() + " 4096", out var command) ==
                ProjectileLimitParseResult.Valid && command.Apply(defaults).GetSourceLimit(source.Id) == 4096,
                "Known sources accept case-insensitive commands");
            var item = new SessionPolicy(); item.RecordProjectiles(defaults.WithSourceLimit(source.Id, 1));
            Check(SessionPolicy.TryReadPreset(item.ToPresetText(), out var roundtrip, out _) &&
                roundtrip.Projectiles.GetSourceLimit(source.Id) == 1, "Every source override round trips");
        }
        foreach (string text in new[] { "/one projectiles on", "/one projectiles off", "/one projectiles reset",
            "/one projectiles total 1", "/one projectiles source default 4096", "/one projectiles source liberation reset" })
            Check(ProjectileLimitCommand.Parse(text, out _) == ProjectileLimitParseResult.Valid, "Valid projectile command: " + text);
        foreach (string text in new[] { "/one projectiles on extra", "/one projectiles total 0", "/one projectiles total 4097",
            "/one projectiles total -1", "/one projectiles total 1.5", "/one projectiles source default reset",
            "/one projectiles source nope 3", "/one projectiles source liberation 0", "/one projectiles source liberation reset extra" })
            Check(ProjectileLimitCommand.Parse(text, out _) == ProjectileLimitParseResult.Invalid, "Reject invalid command: " + text);
        Check(ProjectileLimitCommand.Parse("/one projectiles status", out _) == ProjectileLimitParseResult.Status &&
            ProjectileLimitCommand.Parse("/one projectiles", out _) == ProjectileLimitParseResult.Help &&
            ProjectileLimitCommand.Parse("/one xp", out _) == ProjectileLimitParseResult.NotCommand, "Status/help routing");
        ProjectileLimitCommand.Parse("/one projectiles off", out var off);
        Check(off.Apply(settings).Equals(settings.WithEnabled(false)), "Off keeps configured limits");
        ProjectileLimitCommand.Parse("/one projectiles reset", out var reset);
        Check(reset.Apply(settings).Equals(defaults), "Reset clears all configured limits");
        Check(reset.Apply(rated).Equals(defaults), "Full reset clears rates and active caps together");
        foreach (string bad in new[] { "projectiles enabled 2", "projectiles enabled 01", "projectiles total 0",
            "projectiles total 4097", "projectiles default 1.0", "projectiles source nope 2", "projectiles source Liberation 2",
            "projectiles source liberation +2", "projectiles source liberation 2\nprojectiles source liberation 3",
            "projectiles total 12\nprojectiles total 13" })
            Check(!SessionPolicy.TryReadPreset("SephiriaOne preset v42\n" + bad + "\n", out var rejected, out _) &&
                !rejected.HasChanges, "Invalid presets fail atomically: " + bad);
        for (int version = 1; version <= 41; version++)
        {
            Check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var older, out _) &&
                older.Projectiles.Equals(defaults), "Old presets retain safe defaults");
            Check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nprojectiles total 20\n", out _, out _),
                "Old schemas reject projectile rows");
        }
        policy.RecordProjectiles(settings.WithEnabled(false));
        Check(policy.HasChanges && policy.ToPresetText().StartsWith("SephiriaOne preset v42\n"), "Disabled custom limits persist");
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var disabled, out _) && disabled.Projectiles.Equals(settings.WithEnabled(false)),
            "Disabled custom limits round trip");
        policy.Clear();
        Check(policy.Projectiles.Equals(defaults) && policy.ToPresetText() == "SephiriaOne preset v1\n", "Reset restores lowest schema");
        return checks;
    }
}
