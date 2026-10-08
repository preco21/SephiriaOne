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
            : this(id, name, fountainPoints, fountainContribution,
                new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>(stats)),
                new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(extraChoices)),
                new ReadOnlyDictionary<string, string>(resources == null ? new Dictionary<string, string>() : new Dictionary<string, string>(resources)))
        { }

        // Only the capture path may transfer its newly created dictionaries here.
        // It must not retain or mutate them afterward; other callers use the
        // defensive constructor above. No native dictionaries are transferred.
        internal static PlayerSettingsSnapshot FromOwnedCapture(uint id, string name, int fountainPoints, int fountainContribution,
            Dictionary<string, decimal> stats, Dictionary<string, int> extraChoices, Dictionary<string, string> resources) =>
            new PlayerSettingsSnapshot(id, name, fountainPoints, fountainContribution,
                new ReadOnlyDictionary<string, decimal>(stats), new ReadOnlyDictionary<string, int>(extraChoices),
                new ReadOnlyDictionary<string, string>(resources));

        private PlayerSettingsSnapshot(uint id, string name, int fountainPoints, int fountainContribution,
            ReadOnlyDictionary<string, decimal> stats, ReadOnlyDictionary<string, int> extraChoices, ReadOnlyDictionary<string, string> resources)
        {
            Id = id; Name = name; FountainPoints = fountainPoints; FountainContribution = fountainContribution;
            Stats = stats; ExtraChoices = extraChoices; Resources = resources;
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
        public bool BatHpSteal { get; }
        public bool BatAvailable { get; }
        public bool CollinStartingArtifact { get; }
        public bool CollinAvailable { get; }
        public EventSpawnSettings EventSpawns { get; }
        public bool EventSpawnsAvailable { get; }
        public JarSpawnSettings JarSpawns { get; }
        public bool JarSpawnsAvailable { get; }
        public FriendlyFireSettings FriendlyFire { get; }
        public bool FriendlyFireAvailable { get; }
        public bool ItemUnlock { get; }
        public bool ItemRestrictionsAvailable { get; }
        public RabbitPotionSettings RabbitPotions { get; }
        public bool RabbitPotionsAvailable { get; }
        public bool RabbitLevelUpPotionsAvailable { get; }
        public bool RabbitDescriptionAvailable { get; }
        public IReadOnlyDictionary<string, MerchantSettings> Merchants { get; }
        public bool MerchantSpawns => Merchants[MerchantCatalog.DefaultId].Enabled;
        public int MerchantSpawnChance => Merchants[MerchantCatalog.DefaultId].Chance;
        public bool MerchantsAvailable { get; }
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
            IEnumerable<string> savedSettings, string savedSummary, RabbitPotionSettings rabbitPotions = default,
            bool rabbitPotionsAvailable = false, bool rabbitDescriptionAvailable = false,
            bool merchantSpawns = false, bool merchantsAvailable = false, int merchantSpawnChance = MerchantCommand.DefaultChance,
            IReadOnlyDictionary<string, MerchantSettings> merchants = null, bool rabbitLevelUpPotionsAvailable = false,
            bool itemUnlock = false, bool itemRestrictionsAvailable = false,
            FriendlyFireSettings friendlyFire = default, bool friendlyFireAvailable = false,
            JarSpawnSettings jarSpawns = default, bool jarSpawnsAvailable = false,
            bool batHpSteal = false, bool batAvailable = false,
            bool collinStartingArtifact = false, bool collinAvailable = false,
            EventSpawnSettings eventSpawns = default, bool eventSpawnsAvailable = false)
        {
            SessionIdentity = sessionIdentity; Epoch = epoch; RunGeneration = runGeneration; Revision = revision;
            HostActive = hostActive; CanMutate = canMutate; CanSave = canSave; CanForget = canForget;
            ChoicesAvailable = choicesAvailable; SavedValid = savedValid;
            EventSpawns = eventSpawns; EventSpawnsAvailable = eventSpawnsAvailable;
            JarSpawns = jarSpawns; JarSpawnsAvailable = jarSpawnsAvailable;
            BatHpSteal = batHpSteal; BatAvailable = batAvailable;
            CollinStartingArtifact = collinStartingArtifact; CollinAvailable = collinAvailable;
            FriendlyFire = friendlyFire; FriendlyFireAvailable = friendlyFireAvailable;
            ItemUnlock = itemUnlock; ItemRestrictionsAvailable = itemRestrictionsAvailable;
            RabbitPotions = rabbitPotions; RabbitPotionsAvailable = rabbitPotionsAvailable;
            RabbitLevelUpPotionsAvailable = rabbitLevelUpPotionsAvailable;
            RabbitDescriptionAvailable = rabbitDescriptionAvailable;
            var merchantCopy = new Dictionary<string, MerchantSettings>();
            foreach (var definition in MerchantCatalog.All)
                merchantCopy.Add(definition.Id, merchants != null && merchants.TryGetValue(definition.Id, out var value) ? value :
                    definition.Id == MerchantCatalog.DefaultId ? new MerchantSettings(merchantSpawns, merchantSpawnChance) : MerchantSettings.Defaults(definition));
            Merchants = new ReadOnlyDictionary<string, MerchantSettings>(merchantCopy);
            MerchantsAvailable = merchantsAvailable;
            AvailabilityReason = availabilityReason; FaultedFeature = faultedFeature;
            ActiveSettings = new List<string>(activeSettings).AsReadOnly();
            SavedSettings = new List<string>(savedSettings).AsReadOnly();
            SavedSummary = savedSummary;
            Lines = new List<string>(lines).AsReadOnly();
            Players = new List<PlayerSettingsSnapshot>(players).AsReadOnly();
        }
    }
}
