#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SephiriaOne
{
    internal readonly struct MerchantSettings : IEquatable<MerchantSettings>
    {
        public bool Enabled { get; }
        public int Chance { get; }
        public int FirstFloor { get; }
        public int MaxPerRun { get; }

        public MerchantSettings(bool enabled, int chance, int firstFloor = 1, int maxPerRun = 0)
        {
            if (chance < 0 || chance > 100) throw new ArgumentOutOfRangeException(nameof(chance));
            if (firstFloor < 1 || firstFloor > 1000) throw new ArgumentOutOfRangeException(nameof(firstFloor));
            if (maxPerRun < 0 || maxPerRun > 1000) throw new ArgumentOutOfRangeException(nameof(maxPerRun));
            Enabled = enabled; Chance = chance; FirstFloor = firstFloor; MaxPerRun = maxPerRun;
        }

        public static MerchantSettings Defaults(MerchantDefinition definition) => new MerchantSettings(false, definition.DefaultChance);
        public bool Equals(MerchantSettings other) => Enabled == other.Enabled && Chance == other.Chance &&
            FirstFloor == other.FirstFloor && MaxPerRun == other.MaxPerRun;
        public override bool Equals(object? other) => other is MerchantSettings value && Equals(value);
        public override int GetHashCode() => (Enabled ? 1 : 0) ^ (Chance << 1) ^ (FirstFloor << 8) ^ (MaxPerRun << 18);
    }

    internal sealed class MerchantPolicy
    {
        private readonly Dictionary<string, MerchantSettings> values = new Dictionary<string, MerchantSettings>(StringComparer.Ordinal);
        private IReadOnlyDictionary<string, MerchantSettings>? snapshot;
        public bool HasChanges => values.Count != 0;
        public bool AnyEnabled
        {
            get { foreach (var value in values.Values) if (value.Enabled) return true; return false; }
        }
        public MerchantSettings Get(string id)
        {
            var definition = MerchantCatalog.Find(id) ?? throw new ArgumentException("Unknown merchant type.", nameof(id));
            return values.TryGetValue(definition.Id, out var value) ? value : MerchantSettings.Defaults(definition);
        }
        public IReadOnlyDictionary<string, MerchantSettings> Snapshot
        {
            get
            {
                if (snapshot == null)
                {
                    var copy = new Dictionary<string, MerchantSettings>(StringComparer.Ordinal);
                    foreach (var definition in MerchantCatalog.All) copy.Add(definition.Id, Get(definition.Id));
                    snapshot = new ReadOnlyDictionary<string, MerchantSettings>(copy);
                }
                return snapshot;
            }
        }
        public void Record(MerchantCommand command)
        {
            var definition = MerchantCatalog.Find(command.TypeId) ?? throw new ArgumentException("Unknown merchant type.", nameof(command));
            var current = Get(definition.Id);
            MerchantSettings next;
            switch (command.Option)
            {
                case MerchantOption.Toggle: next = new MerchantSettings(command.Enabled, current.Chance, current.FirstFloor, current.MaxPerRun); break;
                case MerchantOption.Chance: next = new MerchantSettings(current.Enabled, command.Chance, current.FirstFloor, current.MaxPerRun); break;
                case MerchantOption.FirstFloor: next = new MerchantSettings(current.Enabled, current.Chance, command.Value, current.MaxPerRun); break;
                case MerchantOption.MaxPerRun: next = new MerchantSettings(current.Enabled, current.Chance, current.FirstFloor, command.Value); break;
                case MerchantOption.Reset: next = MerchantSettings.Defaults(definition); break;
                default: throw new ArgumentOutOfRangeException(nameof(command));
            }
            if (next.Equals(MerchantSettings.Defaults(definition))) values.Remove(definition.Id);
            else values[definition.Id] = next;
            snapshot = null;
        }
        public void Clear() { values.Clear(); snapshot = null; }
    }
}
