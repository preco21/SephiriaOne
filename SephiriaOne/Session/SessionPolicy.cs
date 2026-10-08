#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class SessionPlayerSnapshot
    {
        public IReadOnlyDictionary<string, int> Raw { get; }
        public IReadOnlyDictionary<string, int> Bonus { get; }
        public IReadOnlyDictionary<string, int> Amplifiers { get; }
        public int FountainPoints { get; }
        public int FountainContribution { get; }
        public int? FountainLimit { get; }
        public int? OriginalLimit { get; }
        public int? AppliedLimit { get; }
        public bool ChoicesAvailable { get; }

        public SessionPlayerSnapshot(IReadOnlyDictionary<string, int> raw, IReadOnlyDictionary<string, int> bonus,
            IReadOnlyDictionary<string, int> amplifiers, int fountainPoints, int fountainContribution,
            int? fountainLimit, int? originalLimit, int? appliedLimit, bool choicesAvailable)
        {
            Raw = raw; Bonus = bonus; Amplifiers = amplifiers;
            FountainPoints = fountainPoints; FountainContribution = fountainContribution;
            FountainLimit = fountainLimit; OriginalLimit = originalLimit; AppliedLimit = appliedLimit;
            ChoicesAvailable = choicesAvailable;
        }

        public static int Read(IReadOnlyDictionary<string, int> values, string key) =>
            values.TryGetValue(key, out int value) ? value : 0;
    }

    internal readonly struct SessionStatWrite
    {
        public string Key { get; }
        public string Marker { get; }
        public int Raw { get; }
        public int Contribution { get; }
        public SessionStatWrite(string key, string marker, int raw, int contribution)
        {
            Key = key; Marker = marker; Raw = raw; Contribution = contribution;
        }
    }

    internal sealed class SessionPlan
    {
        public FountainPlan? Fountain { get; }
        public IReadOnlyList<SessionStatWrite> Stats { get; }
        public SessionPlan(FountainPlan? fountain, IReadOnlyList<SessionStatWrite> stats)
        {
            Fountain = fountain; Stats = stats;
        }
    }

    internal sealed partial class SessionPolicy
    {
        private readonly struct Setting
        {
            public bool Absolute { get; }
            public bool Multiplier { get; }
            public decimal Value { get; }
            public bool Empty => Multiplier ? Value == 1 : !Absolute && Value == 0;
            public Setting(bool absolute, decimal value, bool multiplier = false)
            { Absolute = absolute; Value = value; Multiplier = multiplier; }
            public Setting Add(decimal delta) => new Setting(Absolute, Value + delta);
        }

        private Setting? fountain;
        private readonly Dictionary<string, int> choices = new Dictionary<string, int>();
        private readonly Dictionary<StatDefinition, Setting> stats = new Dictionary<StatDefinition, Setting>();
        public ResourcePolicy Resources { get; } = new ResourcePolicy();
        public bool ItemUnlock { get; private set; }
        public FriendlyFireSettings FriendlyFire { get; private set; }
        public EventSpawnSettings EventSpawns { get; private set; }
        public void RecordEventSpawns(EventSpawnSettings settings) => EventSpawns = settings;
        public JarSpawnSettings JarSpawns { get; private set; }
        public void RecordJarSpawns(JarSpawnSettings settings) => JarSpawns = settings;
        public void Record(FriendlyFireCommand command) => FriendlyFire = FriendlyFire.Apply(command);
        public void RecordItemUnlock(bool enabled) => ItemUnlock = enabled;
        public RabbitPotionSettings RabbitPotions { get; private set; }
        public void Record(RabbitCommand command) => RabbitPotions = RabbitPotions.Apply(command);
        public MerchantPolicy Merchants { get; } = new MerchantPolicy();
        public bool MerchantSpawns => Merchants.Get(MerchantCatalog.DefaultId).Enabled;
        public int MerchantSpawnChance => Merchants.Get(MerchantCatalog.DefaultId).Chance;
        public void Record(MerchantCommand command) => Merchants.Record(command);
        public bool BatHpSteal { get; private set; }
        public void RecordBat(bool reduce) => BatHpSteal = reduce;
        public bool CollinStartingArtifact { get; private set; }
        public void RecordCollin(bool grant) => CollinStartingArtifact = grant;
        public bool HasChanges => fountain.HasValue || choices.Count != 0 || stats.Count != 0 || Resources.HasChanges || RabbitPotions.HasChanges || Merchants.HasChanges || ItemUnlock || FriendlyFire.HasChanges || JarSpawns.HasChanges || EventSpawns.HasChanges || BatHpSteal || CollinStartingArtifact;
        public bool HasFountainSetting => fountain.HasValue;
        public bool HasFountainMultiplier => fountain.HasValue && fountain.Value.Multiplier;

        // Call only after a host command succeeds. Failed requests never become policy.
        public void Record(FountainCommand command)
        {
            if (command.Operation == FountainOperation.Reset) { fountain = null; return; }
            Setting next = NextFountainSetting(command);
            fountain = next.Empty ? (Setting?)null : next;
        }

        public void Record(StatCommand command)
        {
            if (command.Operation == StatOperation.Reset)
            {
                if (command.Stat == null) stats.Clear();
                else stats.Remove(command.Stat);
                return;
            }
            if (command.Stat == null) throw new ArgumentException("A stat is required.", nameof(command));
            Setting next = NextStatSetting(command);
            if (next.Empty) stats.Remove(command.Stat);
            else stats[command.Stat] = next;
        }

        // The successful batch already calculated the active addon contribution.
        public void RecordChoice(string key, int contribution)
        {
            if (Array.IndexOf(ChoiceCommand.Keys, key) < 0 || contribution < 0 || contribution > ChoiceCommand.MaximumExtra)
                throw new ArgumentOutOfRangeException(nameof(contribution));
            if (contribution == 0) choices.Remove(key);
            else choices[key] = contribution;
        }

        public void Clear()
        {
            fountain = null;
            choices.Clear();
            stats.Clear();
            Resources.Clear();
            ItemUnlock = false;
            FriendlyFire = default;
            JarSpawns = default;
            EventSpawns = default;
            BatHpSteal = false;
            CollinStartingArtifact = false;
            RabbitPotions = default;
            Merchants.Clear();
        }

        public bool TryPlan(SessionPlayerSnapshot player, out SessionPlan plan, out string error)
        {
            plan = new SessionPlan(null, Array.Empty<SessionStatWrite>());
            var writes = new List<SessionStatWrite>();
            FountainPlan? fountainPlan = null;
            error = L.T("The joining player's Fountain baseline or session limit is unavailable.");
            if (fountain.HasValue)
            {
                if (!player.FountainLimit.HasValue || !TryPlanFountainSetting(fountain.Value,
                    new[] { player.FountainPoints }, new[] { player.FountainContribution }, player.FountainLimit.Value,
                    player.OriginalLimit, player.AppliedLimit, out FountainPlan pending, out error)) return false;
                fountainPlan = pending;
            }

            error = L.T("Candidate guards are unavailable; inherited settings were not applied.");
            if (choices.Count != 0 && !player.ChoicesAvailable) return false;
            foreach (var choice in choices)
            {
                int index = Array.IndexOf(ChoiceCommand.Keys, choice.Key);
                string marker = "SEPHIRIAONE_" + choice.Key;
                var command = new ChoiceCommand((ChoiceTarget)(1 << index), ChoiceOperation.Set, choice.Value);
                if (!command.TryPlan(SessionPlayerSnapshot.Read(player.Raw, choice.Key), SessionPlayerSnapshot.Read(player.Raw, marker),
                    SessionPlayerSnapshot.Read(player.Bonus, choice.Key), SessionPlayerSnapshot.Read(player.Amplifiers, choice.Key),
                    out int raw, out int applied, out error)) return false;
                writes.Add(new SessionStatWrite(choice.Key, marker, raw, applied));
            }

            foreach (var entry in stats)
            {
                StatDefinition stat = entry.Key;
                Setting setting = entry.Value;
                var snapshot = new StatSnapshot(stat, SessionPlayerSnapshot.Read(player.Raw, stat.Key),
                    SessionPlayerSnapshot.Read(player.Raw, stat.Marker), SessionPlayerSnapshot.Read(player.Bonus, stat.Key),
                    SessionPlayerSnapshot.Read(player.Amplifiers, stat.Key));
                if (!TryPlanStatSetting(setting, snapshot, out StatUpdate update, out error)) return false;
                writes.Add(new SessionStatWrite(stat.Key, stat.Marker, update.Raw, update.Contribution));
            }
            plan = new SessionPlan(fountainPlan, writes);
            error = "";
            return true;
        }
    }

}
