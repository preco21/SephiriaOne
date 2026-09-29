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
    }
}
