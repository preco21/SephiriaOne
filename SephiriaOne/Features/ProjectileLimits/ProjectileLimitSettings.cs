#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SephiriaOne
{
    internal readonly struct ProjectileLimitSettings : IEquatable<ProjectileLimitSettings>
    {
        public const int DefaultPlayerLimit = 64, DefaultPerSourceLimit = 12;
        public const int MinimumLimit = 1, MaximumLimit = 4096;
        private static readonly IReadOnlyDictionary<string, int> Empty =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal));
        private readonly int playerLimit, defaultSourceLimit;
        private readonly IReadOnlyDictionary<string, int>? sourceLimits;
        public bool Enabled { get; }
        public int PlayerLimit => playerLimit == 0 ? DefaultPlayerLimit : playerLimit;
        public int DefaultSourceLimit => defaultSourceLimit == 0 ? DefaultPerSourceLimit : defaultSourceLimit;
        public IReadOnlyDictionary<string, int> SourceLimits => sourceLimits ?? Empty;
        public bool HasChanges => Enabled || PlayerLimit != DefaultPlayerLimit ||
            DefaultSourceLimit != DefaultPerSourceLimit || SourceLimits.Count != 0;
        public static bool ValidLimit(int value) => value >= MinimumLimit && value <= MaximumLimit;

        public ProjectileLimitSettings(bool enabled, int playerLimit = DefaultPlayerLimit,
            int defaultSourceLimit = DefaultPerSourceLimit, IReadOnlyDictionary<string, int>? sourceLimits = null)
        {
            if (!ValidLimit(playerLimit)) throw new ArgumentOutOfRangeException(nameof(playerLimit));
            if (!ValidLimit(defaultSourceLimit)) throw new ArgumentOutOfRangeException(nameof(defaultSourceLimit));
            Enabled = enabled; this.playerLimit = playerLimit; this.defaultSourceLimit = defaultSourceLimit;
            if (sourceLimits == null || sourceLimits.Count == 0) this.sourceLimits = null;
            else
            {
                var copy = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var entry in sourceLimits)
                {
                    var source = ProjectileLimitCatalog.Find(entry.Key);
                    if (source == null || source.Id != entry.Key || !ValidLimit(entry.Value))
                        throw new ArgumentException("Invalid projectile source limit.", nameof(sourceLimits));
                    copy.Add(entry.Key, entry.Value);
                }
                this.sourceLimits = new ReadOnlyDictionary<string, int>(copy);
            }
        }

        public int GetSourceLimit(string source) =>
            SourceLimits.TryGetValue(source, out int value) ? value : DefaultSourceLimit;
        public ProjectileLimitSettings WithEnabled(bool value) => new ProjectileLimitSettings(value, PlayerLimit, DefaultSourceLimit, SourceLimits);
        public ProjectileLimitSettings WithPlayerLimit(int value) => new ProjectileLimitSettings(Enabled, value, DefaultSourceLimit, SourceLimits);
        public ProjectileLimitSettings WithDefaultSourceLimit(int value) => new ProjectileLimitSettings(Enabled, PlayerLimit, value, SourceLimits);
        public ProjectileLimitSettings WithSourceLimit(string source, int value)
        {
            if (!ValidLimit(value)) throw new ArgumentOutOfRangeException(nameof(value));
            string id = ProjectileLimitCatalog.Find(source)?.Id ?? throw new ArgumentException("Unknown projectile source.", nameof(source));
            var copy = CopyLimits(); copy[id] = value;
            return new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, copy);
        }
        public ProjectileLimitSettings WithoutSourceLimit(string source)
        {
            string id = ProjectileLimitCatalog.Find(source)?.Id ?? throw new ArgumentException("Unknown projectile source.", nameof(source));
            var copy = CopyLimits(); copy.Remove(id);
            return new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, copy);
        }
        private Dictionary<string, int> CopyLimits()
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in SourceLimits) copy.Add(entry.Key, entry.Value);
            return copy;
        }
        public bool Equals(ProjectileLimitSettings other)
        {
            if (Enabled != other.Enabled || PlayerLimit != other.PlayerLimit ||
                DefaultSourceLimit != other.DefaultSourceLimit || SourceLimits.Count != other.SourceLimits.Count) return false;
            foreach (var entry in SourceLimits)
                if (!other.SourceLimits.TryGetValue(entry.Key, out int value) || value != entry.Value) return false;
            return true;
        }
        public override bool Equals(object? obj) => obj is ProjectileLimitSettings other && Equals(other);
        public override int GetHashCode()
        {
            int hash = (Enabled ? 1 : 0) ^ PlayerLimit * 397 ^ DefaultSourceLimit;
            foreach (var entry in SourceLimits) hash ^= entry.Key.GetHashCode() * 397 ^ entry.Value;
            return hash;
        }
    }
}
