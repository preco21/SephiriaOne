#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class MerchantDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string SocialId { get; }
        public string UnitTypeName { get; }
        public int SeedSalt { get; }
        public bool HasGuarantee { get; }
        public int DefaultChance { get; }
        public Func<MerchantSpawnContext, bool>? Condition { get; }

        public MerchantDefinition(string id, string name, string socialId, string unitTypeName,
            int seedSalt, bool hasGuarantee = false, int defaultChance = 25,
            Func<MerchantSpawnContext, bool>? condition = null)
        {
            Id = id; Name = name; SocialId = socialId; UnitTypeName = unitTypeName;
            SeedSalt = seedSalt; HasGuarantee = hasGuarantee; DefaultChance = defaultChance;
            Condition = condition;
        }
    }

    internal static class MerchantCatalog
    {
        public const string DefaultId = "wandering";
        public static IReadOnlyList<MerchantDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new MerchantDefinition(DefaultId, "Wandering Merchant", "Merchant_Papa", "Unit_BabaMerchantHard", 0, true),
            new MerchantDefinition("papyrus", "Papyrus", "Traveler_Merchant_Papyrus", "Unit_Soldier", 0x50415059, true),
            new MerchantDefinition("taz", "Taz", "Traveler_Merchant_Taz", "Unit_TurtlePotion", 0x54415A31, true)
        });

        public static MerchantDefinition? Find(string? id)
        {
            foreach (var definition in All)
                if (string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase)) return definition;
            return null;
        }
    }
}
