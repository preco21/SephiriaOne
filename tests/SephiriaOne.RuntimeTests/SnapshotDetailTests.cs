using SephiriaOne;

internal static class SnapshotDetailTests
{
    // Exercise both projections throughout the existing pending-join, fault,
    // native-edit, saved-file, host-replacement and authority-loss scenarios.
    internal static void AssertSameValues(SettingsSnapshot full, SettingsSnapshot compact)
    {
        if (compact.Lines.Count != 0) throw new Exception("Non-Status panel snapshots must not build diagnostic lines.");
        foreach (var property in typeof(SettingsSnapshot).GetProperties())
        {
            if (property.Name is "Lines" or "Players" or "ActiveSettings" or "SavedSettings" or "Merchants") continue;
            if (!Equals(property.GetValue(full), property.GetValue(compact)))
                throw new Exception("Compact snapshot changed " + property.Name);
        }
        if (!full.ActiveSettings.SequenceEqual(compact.ActiveSettings) ||
            !full.SavedSettings.SequenceEqual(compact.SavedSettings) || full.Players.Count != compact.Players.Count ||
            full.Merchants.Count != compact.Merchants.Count || full.Merchants.Any(pair =>
                !compact.Merchants.TryGetValue(pair.Key, out var value) || !pair.Value.Equals(value)))
            throw new Exception("Compact snapshot lost settings or ready players.");
        for (int i = 0; i < full.Players.Count; i++)
        {
            var left = full.Players[i];
            var right = compact.Players[i];
            if (left.Id != right.Id || left.Name != right.Name || left.FountainPoints != right.FountainPoints ||
                left.FountainContribution != right.FountainContribution || !left.Stats.SequenceEqual(right.Stats) ||
                !left.ExtraChoices.SequenceEqual(right.ExtraChoices) || !left.Resources.SequenceEqual(right.Resources))
                throw new Exception("Compact snapshot changed live values for player " + left.Id);
        }
        foreach (var content in new[] { SnapshotContent.None, SnapshotContent.Stats, SnapshotContent.Choices,
            SnapshotContent.Resources, SnapshotContent.Presets, SnapshotContent.Stats | SnapshotContent.Resources,
            SnapshotContent.Diagnostics, SnapshotContent.All })
            AssertSelectedValues(full, SessionSettings.ReadSnapshot(content), content);
    }

    private static void AssertSelectedValues(SettingsSnapshot full, SettingsSnapshot selected, SnapshotContent content)
    {
        bool diagnostics = (content & SnapshotContent.Diagnostics) != 0;
        if (diagnostics) content = SnapshotContent.All;
        foreach (var property in typeof(SettingsSnapshot).GetProperties())
        {
            if (property.Name is "Lines" or "Players" or "ActiveSettings" or "SavedSettings" or "SavedSummary" or "Merchants") continue;
            if (!Equals(property.GetValue(full), property.GetValue(selected)))
                throw new Exception("Selected snapshot changed " + property.Name + ": " + content);
        }
        bool presets = (content & SnapshotContent.Presets) != 0;
        if (!(presets ? full.ActiveSettings.SequenceEqual(selected.ActiveSettings) &&
                full.SavedSettings.SequenceEqual(selected.SavedSettings) && full.SavedSummary == selected.SavedSummary :
                selected.ActiveSettings.Count == 0 && selected.SavedSettings.Count == 0 && selected.SavedSummary == "") ||
            !(diagnostics ? full.Lines.SequenceEqual(selected.Lines) : selected.Lines.Count == 0) ||
            !full.Merchants.SequenceEqual(selected.Merchants) || full.Players.Count != selected.Players.Count)
            throw new Exception("Selected snapshot lost required settings/roster or built unused descriptions: " + content);
        for (int i = 0; i < full.Players.Count; i++)
        {
            var left = full.Players[i]; var right = selected.Players[i];
            if (left.Id != right.Id || left.Name != right.Name || left.FountainPoints != right.FountainPoints ||
                left.FountainContribution != right.FountainContribution ||
                !Matches(left.Stats, right.Stats, (content & SnapshotContent.Stats) != 0) ||
                !Matches(left.ExtraChoices, right.ExtraChoices, (content & SnapshotContent.Choices) != 0) ||
                !Matches(left.Resources, right.Resources, (content & SnapshotContent.Resources) != 0))
                throw new Exception("Selected snapshot changed live values or built unused sections: " + content);
        }
    }

    private static bool Matches<T>(IReadOnlyDictionary<string, T> full, IReadOnlyDictionary<string, T> selected, bool included) =>
        included ? full.SequenceEqual(selected) : selected.Count == 0;

    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        var host = start();
        check(SettingsActions.Execute("/stats luck +10").Success, "Prepare selected snapshot fixture");
        var before = SessionSettings.ReadSnapshot(SnapshotContent.Stats);
        int writes = 0;
        host.PlayerAvatar.customStats.BeforeWrite = (_, _) => writes++;
        host.PlayerAvatar.customStats["LUCK"] += 7;
        writes = 0;
        PlayerSpawner.MultiplayerList.Add(host);
        var basic = SessionSettings.ReadSnapshot(SnapshotContent.None);
        var fresh = SessionSettings.ReadSnapshot(SnapshotContent.Stats);
        check(basic.CanMutate && basic.Players.Count == 1 && fresh.Players.Count == 1 &&
            fresh.Players[0].Stats["luck"] == 22 && before.Players[0].Stats["luck"] == 15 &&
            before.Revision == fresh.Revision && writes == 0,
            "Switching snapshot sections reads fresh native edits without writes, duplicate players or stale/mutated snapshots");
        check(ReadOnly(basic.Players[0].Stats) && ReadOnly(basic.Players[0].ExtraChoices) &&
            ReadOnly(basic.Players[0].Resources) && ReadOnly(fresh.Players[0].Stats),
            "Both omitted and captured player dictionaries reject mutations");
        AssertSameValues(SessionSettings.ReadSnapshot(), SessionSettings.ReadSnapshot(includeDiagnostics: false));
        host.PlayerAvatar.customStats.BeforeWrite = null;
        SessionSettings.Stop();
        AssertSameValues(SessionSettings.ReadSnapshot(), SessionSettings.ReadSnapshot(includeDiagnostics: false));
    }

    private static bool ReadOnly<T>(IReadOnlyDictionary<string, T> values)
    {
        try { ((IDictionary<string, T>)values).Add("unexpected mutation", default); }
        catch (NotSupportedException) { return true; }
        return false;
    }
}
