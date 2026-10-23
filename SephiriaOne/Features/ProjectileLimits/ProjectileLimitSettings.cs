#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SephiriaOne
{
    internal readonly struct ProjectileLimitSettings : IEquatable<ProjectileLimitSettings>
    {
        public const int DefaultPlayerLimit = 64, DefaultPerSourceLimit = 12;
        public const int DefaultPlayerRate = 64, DefaultSourceRate = 12;
        public const int MinimumLimit = 1, MaximumLimit = 4096;
        private static readonly IReadOnlyDictionary<string, int> Empty =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal));
        private readonly int playerLimit, defaultSourceLimit;
        private readonly int playerRate, sourceRate;
        private readonly IReadOnlyDictionary<string, int>? sourceLimits;
        public bool Enabled { get; }
        public bool RateEnabled { get; }
        public int PlayerRate => playerRate == 0 ? DefaultPlayerRate : playerRate;
        public int SourceRate => sourceRate == 0 ? DefaultSourceRate : sourceRate;
        public bool HasRateChanges => RateEnabled || PlayerRate != DefaultPlayerRate || SourceRate != DefaultSourceRate;
        public int PlayerLimit => playerLimit == 0 ? DefaultPlayerLimit : playerLimit;
        public int DefaultSourceLimit => defaultSourceLimit == 0 ? DefaultPerSourceLimit : defaultSourceLimit;
        public IReadOnlyDictionary<string, int> SourceLimits => sourceLimits ?? Empty;
        public bool HasChanges => Enabled || PlayerLimit != DefaultPlayerLimit ||
            DefaultSourceLimit != DefaultPerSourceLimit || SourceLimits.Count != 0 || HasRateChanges;
        public static bool ValidLimit(int value) => value >= MinimumLimit && value <= MaximumLimit;

        public ProjectileLimitSettings(bool enabled, int playerLimit = DefaultPlayerLimit,
            int defaultSourceLimit = DefaultPerSourceLimit, IReadOnlyDictionary<string, int>? sourceLimits = null,
            bool rateEnabled = false, int playerRate = DefaultPlayerRate, int sourceRate = DefaultSourceRate)
        {
            if (!ValidLimit(playerLimit)) throw new ArgumentOutOfRangeException(nameof(playerLimit));
            if (!ValidLimit(defaultSourceLimit)) throw new ArgumentOutOfRangeException(nameof(defaultSourceLimit));
            if (!ValidLimit(playerRate)) throw new ArgumentOutOfRangeException(nameof(playerRate));
            if (!ValidLimit(sourceRate)) throw new ArgumentOutOfRangeException(nameof(sourceRate));
            Enabled = enabled; this.playerLimit = playerLimit; this.defaultSourceLimit = defaultSourceLimit;
            RateEnabled = rateEnabled; this.playerRate = playerRate; this.sourceRate = sourceRate;
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
        public ProjectileLimitSettings WithEnabled(bool value) => new ProjectileLimitSettings(value, PlayerLimit, DefaultSourceLimit, SourceLimits, RateEnabled, PlayerRate, SourceRate);
        public ProjectileLimitSettings WithPlayerLimit(int value) => new ProjectileLimitSettings(Enabled, value, DefaultSourceLimit, SourceLimits, RateEnabled, PlayerRate, SourceRate);
        public ProjectileLimitSettings WithDefaultSourceLimit(int value) => new ProjectileLimitSettings(Enabled, PlayerLimit, value, SourceLimits, RateEnabled, PlayerRate, SourceRate);
        public ProjectileLimitSettings WithRateEnabled(bool value) => new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, SourceLimits, value, PlayerRate, SourceRate);
        public ProjectileLimitSettings WithPlayerRate(int value) => new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, SourceLimits, RateEnabled, value, SourceRate);
        public ProjectileLimitSettings WithSourceRate(int value) => new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, SourceLimits, RateEnabled, PlayerRate, value);
        public ProjectileLimitSettings WithSourceLimit(string source, int value)
        {
            if (!ValidLimit(value)) throw new ArgumentOutOfRangeException(nameof(value));
            string id = ProjectileLimitCatalog.Find(source)?.Id ?? throw new ArgumentException("Unknown projectile source.", nameof(source));
            var copy = CopyLimits(); copy[id] = value;
            return new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, copy, RateEnabled, PlayerRate, SourceRate);
        }
        public ProjectileLimitSettings WithoutSourceLimit(string source)
        {
            string id = ProjectileLimitCatalog.Find(source)?.Id ?? throw new ArgumentException("Unknown projectile source.", nameof(source));
            var copy = CopyLimits(); copy.Remove(id);
            return new ProjectileLimitSettings(Enabled, PlayerLimit, DefaultSourceLimit, copy, RateEnabled, PlayerRate, SourceRate);
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
                RateEnabled != other.RateEnabled || PlayerRate != other.PlayerRate || SourceRate != other.SourceRate ||
                DefaultSourceLimit != other.DefaultSourceLimit || SourceLimits.Count != other.SourceLimits.Count) return false;
            foreach (var entry in SourceLimits)
                if (!other.SourceLimits.TryGetValue(entry.Key, out int value) || value != entry.Value) return false;
            return true;
        }
        public override bool Equals(object? obj) => obj is ProjectileLimitSettings other && Equals(other);
        public override int GetHashCode()
        {
            int hash = (Enabled ? 1 : 0) ^ PlayerLimit * 397 ^ DefaultSourceLimit;
            hash = hash * 397 ^ (RateEnabled ? 1 : 0) ^ PlayerRate * 397 ^ SourceRate;
            foreach (var entry in SourceLimits) hash ^= entry.Key.GetHashCode() * 397 ^ entry.Value;
            return hash;
        }
    }
}
