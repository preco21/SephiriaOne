using SephiriaOne;

internal static class ItemRestrictionTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        var values = new Dictionary<string, string> { ["1/Bound"] = "2", ["1/OwnRestriction"] = "1", ["1/Enchant"] = "4", ["2/Bound"] = "3", ["3/OwnRestriction"] = "2" };
        var overlay = new ItemRestrictionOverlay(values);
        overlay.SetEnabled(false);
        Check(values["1/OwnRestriction"] == "1" && values["2/Bound"] == "3", "Default leaves native flags unchanged");
        overlay.SetEnabled(true);
        Check(values["1/Bound"] == "" && values["1/OwnRestriction"] == "" && values["2/Bound"] == "", "Starting and Fountain instances unlock");
        Check(values["1/Enchant"] == "4" && values["3/OwnRestriction"] == "2", "Unrelated metadata/restriction kinds survive");
        overlay.SetEnabled(true);
        Check(overlay.NativeSnapshot()["1/Bound"] == "2", "Repeated enable retains original owner");
        var save = overlay.NativeSnapshot();
        Check(save["1/OwnRestriction"] == "1" && save["2/Bound"] == "3" && values["1/Bound"] == "", "Save has native flags without changing live state");
        values["1/Bound"] = "5"; overlay.Changed("1/Bound", false);
        Check(values["1/Bound"] == "" && overlay.NativeSnapshot()["1/Bound"] == "5", "Native rebind replaces original owner");
        values.Remove("2/Bound"); overlay.Changed("2/Bound", true);
        Check(!overlay.NativeSnapshot().ContainsKey("2/Bound"), "Sale/native unbind cannot be resurrected by reset");
        values["4/Bound"] = "9"; overlay.Changed("4/Bound", false);
        Check(values["4/Bound"] == "", "Late join and new grant unlock immediately");
        values["3/OwnRestriction"] = "1"; overlay.Changed("3/OwnRestriction", false);
        Check(values["3/OwnRestriction"] == "", "New native starting flag unlocks");
        values["3/OwnRestriction"] = "7"; overlay.Changed("3/OwnRestriction", false);
        overlay.SetEnabled(false);
        Check(values["1/Bound"] == "5" && values["1/OwnRestriction"] == "1" && values["4/Bound"] == "9", "Off restores latest native values");
        Check(values["3/OwnRestriction"] == "7" && !values.ContainsKey("2/Bound"), "Off preserves external override and native unbind");
        overlay.SetEnabled(true); values.Clear(); overlay.Cleared();
        values["1/Bound"] = "8"; overlay.Changed("1/Bound", false);
        overlay.SetEnabled(false);
        Check(values.Count == 1 && values["1/Bound"] == "8", "Second run cannot reuse old item provenance");
        foreach (string key in new[] { "bad/Bound", "-1/Bound", "01/Bound", "1/Other", "1/Bound/extra" })
        { values[key] = "2"; overlay.SetEnabled(true); Check(values[key] == "2", "Malformed or unrelated key preserved: " + key); overlay.SetEnabled(false); }
        foreach (string value in new[] { "", "bad", "-1" })
        { values["5/Bound"] = value; overlay.SetEnabled(true); Check(values["5/Bound"] == value, "Unknown binding value preserved"); overlay.SetEnabled(false); }
        var policy = new SessionPolicy();
        Check(!policy.ItemUnlock && !policy.HasChanges, "Policy defaults off");
        policy.RecordItemUnlock(true);
        Check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var restored, out _) && restored.ItemUnlock, "v11 round trip");
        Check(policy.ToPresetText().StartsWith("SephiriaOne preset v11\n"), "New option uses versioned preset");
        Check(SessionPolicy.TryReadPreset("SephiriaOne preset v10\nrabbit infinite 1\n", out var old, out _) && !old.ItemUnlock && old.RabbitPotions.Infinite, "Older presets preserve existing options and default unlock off");
        foreach (string text in new[] { "SephiriaOne preset v10\nitems unlock 1\n", "SephiriaOne preset v11\nitems unlock 2\n", "SephiriaOne preset v11\nitems unlock 1\nitems unlock 0\n" })
            Check(!SessionPolicy.TryReadPreset(text, out _, out _), "Invalid item preset rejected");
        policy.Clear(); Check(!policy.ItemUnlock && !policy.HasChanges, "Policy clear resets item option");
        return checks;
    }
}
