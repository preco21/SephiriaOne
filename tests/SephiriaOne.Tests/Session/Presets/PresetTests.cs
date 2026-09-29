using System.Globalization;
using SephiriaOne;

internal static class PresetTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            checks++;
        }
        const string header = "SephiriaOne preset v1\n";
        Check(PresetCommand.Parse("/one status", out _) == PresetAction.Status &&
            PresetCommand.Parse("/mod status", out _) == PresetAction.NotCommand, "Addon namespace replaces the generic command without alias");
        var policy = new SessionPolicy();
        policy.Record(new FountainCommand(FountainOperation.Set, 100));
        policy.Record(new FountainCommand(FountainOperation.Add, 5));
        policy.Record(new StatCommand(StatCatalog.Find("luck"), StatOperation.Add, 10));
        policy.Record(new StatCommand(StatCatalog.Find("luck"), StatOperation.Subtract, 3));
        policy.Record(new StatCommand(StatCatalog.Find("critical"), StatOperation.Set, 12.25m));
        policy.RecordChoice("EXTRAITEMCHOICES", 5);
        policy.RecordChoice("EXTRAWEAPONCHOICES", 2);
        policy.RecordChoice("EXTRAMIRACLECHOICES", 3);
        string encoded = policy.ToPresetText();
        Check(encoded == header + "fountain set 105\nchoices item 5\nchoices weapon 2\nchoices miracle 3\nstats luck offset 7\nstats critical set 12.25\n",
            "Export composes command history in deterministic category order");
        Check(SessionPolicy.TryReadPreset(encoded, out var loaded, out _) && loaded.ToPresetText() == encoded,
            "All families round trip with set and relative intent");
        var snapshot = new SessionPlayerSnapshot(new Dictionary<string, int> { ["LUCK"] = 20 }, new Dictionary<string, int>(),
            new Dictionary<string, int>(), 4, 0, 12, null, null, true);
        Check(loaded.TryPlan(snapshot, out var plan, out _) && plan.Fountain!.Points[0] == 105 &&
            plan.Stats.Single(x => x.Key == "LUCK").Raw == 27 && plan.Stats.Single(x => x.Key == "CRITICAL").Raw == 1225,
            "Loaded policy retains semantics against a different native baseline");
        Check(SessionPolicy.TryReadPreset(header, out loaded, out _) && !loaded.HasChanges, "Empty preset is supported");
        Check(SessionPolicy.TryReadPreset(header + "fountain set 0\nstats luck set 0\n", out loaded, out _) && loaded.HasChanges,
            "Zero absolute values remain settings");
        Check(SessionPolicy.TryReadPreset(header + "fountain offset -3\nstats critical offset -1.25\n", out loaded, out _) &&
            loaded.ToPresetText().Contains("offset -1.25"), "Signed and fractional offsets round trip");
        Check(SessionPolicy.TryReadPreset(header + "fountain offset 0\nstats luck offset 0\nchoices item 0\n", out loaded, out _) && !loaded.HasChanges,
            "Zero relative adjustments normalize away");
        policy.Record(new FountainCommand(FountainOperation.Reset, 0));
        policy.Record(new StatCommand(null, StatOperation.Reset, 0));
        policy.RecordChoice("EXTRAITEMCHOICES", 0);
        Check(policy.ToPresetText() == header + "choices weapon 2\nchoices miracle 3\n", "Reset families stay out of saved data");
        foreach (string invalid in new[]
        {
            "", "SephiriaOne preset v9\n", header + "unknown set 1", header + "choices armor 3",
            header + "fountain set 1\nfountain offset 2", header + "stats luck set 10\nstats luck offset 1",
            header + "choices item 1\nchoices item 2", header + "stats unknown set 10", header + "stats luck arbitrary 10",
            header + "fountain set -1", header + "fountain offset 2147483648", header + "fountain set 1.5",
            header + "choices item 21", header + "choices item -1", header + "choices item 1.5",
            header + "stats luck set 10001", header + "stats attackspeed set 0", header + "stats luck offset 1.1",
            header + "stats critical set 1.234", header + "stats luck set 1e2", header + "stats luck set 1,5",
            header + "stats luck set 1 trailing", header + new string(' ', 5000)
        })
            Check(!SessionPolicy.TryReadPreset(invalid, out loaded, out _) && !loaded.HasChanges, "Reject entire malformed preset: " + invalid.Trim());
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(SessionPolicy.TryReadPreset(header + "stats critical offset 1.25\n", out loaded, out _) &&
                loaded.ToPresetText() == header + "stats critical offset 1.25\n", "Preset numbers are invariant across cultures");
        }
        finally { CultureInfo.CurrentCulture = before; }

        Check(PresetCommand.Parse("ordinary chat", out _) == PresetAction.NotCommand &&
            PresetCommand.Parse("/modish status", out _) == PresetAction.NotCommand, "Exact mod command token only");
        Check(PresetCommand.Parse("/one STATUS", out _) == PresetAction.Status &&
            PresetCommand.Parse("/one save", out _) == PresetAction.Save && PresetCommand.Parse("/one forget", out _) == PresetAction.Forget,
            "Case-insensitive preset commands parse");
        Check(PresetCommand.Parse("/one", out _) == PresetAction.Help && PresetCommand.Parse("/one help", out _) == PresetAction.Help,
            "Preset help is available");
        Check(PresetCommand.Parse("/one save extra", out _) == PresetAction.Invalid &&
            PresetCommand.Parse("/one delete", out _) == PresetAction.Invalid, "Invalid preset commands reject extra arguments");

        string root = Path.Combine(Path.GetTempPath(), "SephiriaOne-preset-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PresetStore(Path.Combine(root, "data", "session-preset.txt"));
            Check(store.TryLoad(out loaded, out bool exists, out _) && !exists && !loaded.HasChanges, "Missing preset is an empty success");
            Check(store.TrySave(policy, out _) && File.Exists(store.FilePath), "Save creates a missing data directory");
            Check(store.TryLoad(out loaded, out exists, out _) && exists && loaded.ToPresetText() == policy.ToPresetText(), "Saved data loads");
            Check(store.TrySave(new SessionPolicy(), out _) && store.TryLoad(out loaded, out exists, out _) && exists && !loaded.HasChanges,
                "Atomic overwrite can store an empty preset");
            Check(store.TrySave(policy, out _), "Restore valid preset for failure tests");
            var badPolicy = new SessionPolicy();
            badPolicy.Record(new StatCommand(StatCatalog.Find("luck"), StatOperation.Set, -10));
            Check(!store.TrySave(badPolicy, out _) && File.ReadAllText(store.FilePath) == policy.ToPresetText(),
                "Invalid policy cannot overwrite an existing saved preset");
            using (var locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                Check(!store.TrySave(new SessionPolicy(), out _), "Locked destination reports failure");
            Check(File.ReadAllText(store.FilePath) == policy.ToPresetText() && Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!).Length == 1,
                "Failed replacement preserves old preset and cleans temporary files");
            File.WriteAllText(store.FilePath, header + "stats luck set 10\ninvalid");
            Check(!store.TryLoad(out loaded, out exists, out _) && exists && !loaded.HasChanges, "Bad file loads no partial policy");
            File.WriteAllText(store.FilePath, new string('x', 5000));
            Check(!store.TryLoad(out loaded, out exists, out _), "Oversized disk file is rejected");
            Check(store.TryForget(out _) && !File.Exists(store.FilePath) && store.TryForget(out _), "Forget is idempotent");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        return checks;
    }
}
