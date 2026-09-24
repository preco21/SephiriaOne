using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SephiriaOne
{
    internal sealed class PlayerSettingsSnapshot
    {
        public uint Id { get; }
        public string Name { get; }
        public int FountainPoints { get; }
        public int FountainContribution { get; }
        public IReadOnlyDictionary<string, decimal> Stats { get; }
        public IReadOnlyDictionary<string, int> ExtraChoices { get; }
        public IReadOnlyDictionary<string, string> Resources { get; }

        public PlayerSettingsSnapshot(uint id, string name, int fountainPoints, int fountainContribution,
            IDictionary<string, decimal> stats, IDictionary<string, int> extraChoices, IDictionary<string, string> resources = null)
        {
            Id = id; Name = name; FountainPoints = fountainPoints; FountainContribution = fountainContribution;
            Stats = new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>(stats));
            ExtraChoices = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(extraChoices));
            Resources = new ReadOnlyDictionary<string, string>(resources == null ? new Dictionary<string, string>() : new Dictionary<string, string>(resources));
        }
    }

    internal sealed class SettingsSnapshot
    {
        // Identity is an opaque lifetime token. Consumers must not use it to mutate game state.
        public object SessionIdentity { get; }
        public long Epoch { get; }
        public long RunGeneration { get; }
        public long Revision { get; }
        public bool HostActive { get; }
        public bool CanMutate { get; }
        public bool CanSave { get; }
        public bool CanForget { get; }
        public bool ChoicesAvailable { get; }
        public bool SavedValid { get; }
        public string AvailabilityReason { get; }
        public string FaultedFeature { get; }
        public IReadOnlyList<string> ActiveSettings { get; }
        public IReadOnlyList<string> SavedSettings { get; }
        public string SavedSummary { get; }
        public IReadOnlyList<string> Lines { get; }
        public IReadOnlyList<PlayerSettingsSnapshot> Players { get; }

        public SettingsSnapshot(object sessionIdentity, long epoch, long runGeneration, long revision,
            bool hostActive, bool canMutate, bool canSave, bool canForget, bool choicesAvailable, bool savedValid,
            string availabilityReason, string faultedFeature, IEnumerable<string> lines,
            IEnumerable<PlayerSettingsSnapshot> players, IEnumerable<string> activeSettings,
            IEnumerable<string> savedSettings, string savedSummary)
        {
            SessionIdentity = sessionIdentity; Epoch = epoch; RunGeneration = runGeneration; Revision = revision;
            HostActive = hostActive; CanMutate = canMutate; CanSave = canSave; CanForget = canForget;
            ChoicesAvailable = choicesAvailable; SavedValid = savedValid;
            AvailabilityReason = availabilityReason; FaultedFeature = faultedFeature;
            ActiveSettings = new List<string>(activeSettings).AsReadOnly();
            SavedSettings = new List<string>(savedSettings).AsReadOnly();
            SavedSummary = savedSummary;
            Lines = new List<string>(lines).AsReadOnly();
            Players = new List<PlayerSettingsSnapshot>(players).AsReadOnly();
        }
    }
}
