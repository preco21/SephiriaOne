#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class ProjectileLimitSource
    {
        public string Id { get; }
        public string Label { get; }
        internal ProjectileLimitSource(string id, string label) { Id = id; Label = label; }
    }

    internal static class ProjectileLimitCatalog
    {
        public static IReadOnlyList<ProjectileLimitSource> All { get; } = Array.AsReadOnly(new[]
        {
            new ProjectileLimitSource("pallas-card", "Palas' Card"),
            new ProjectileLimitSource("liberation", "Liberation"),
            new ProjectileLimitSource("ice-vine", "Ice Vine"),
            new ProjectileLimitSource("ice-air-slash", "Scabbard of Exorcism"),
            new ProjectileLimitSource("ice-spear", "Voluspa"),
            new ProjectileLimitSource("ice-bow", "Frozen Bow"),
            new ProjectileLimitSource("ice-hammer", "Blizzard Hammer"),
            new ProjectileLimitSource("thunder-earring", "Thunder's Earring"),
            new ProjectileLimitSource("phoenix-wing", "Phoenix Wing"),
            new ProjectileLimitSource("meteor", "Meteor"),
            new ProjectileLimitSource("flame-root", "Flame Herb Root"),
            new ProjectileLimitSource("rock-elephant", "Rock Elephant"),
            new ProjectileLimitSource("kunai", "Kunai"),
            new ProjectileLimitSource("planets", "Planets (each)"),
            new ProjectileLimitSource("weapon-spike", "Weapon needles"),
            new ProjectileLimitSource("flame-ball", "Flame Ball"),
            new ProjectileLimitSource("yearning-robe", "Yearning Robe"),
            new ProjectileLimitSource("bolt-magic", "Bolt Magic (all elements)")
        });

        public static ProjectileLimitSource? Find(string? id)
        {
            foreach (var source in All)
                if (source.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return source;
            return null;
        }
    }
}
