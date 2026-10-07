using System;
using UnityEngine;

namespace SephiriaOne
{
    internal static class RabbitLevelUpCatalog
    {
        // Audited native potion effects. Random-stat potions (32/44) can roll MP
        // regeneration, so they are excluded along with healing, MP, HP and lifesteal.
        // Hidden 35/46 are deliberately included: the policy is all non-HP/MP potions.
        private static readonly int[] Ids = { 28, 29, 30, 31, 33, 34, 35, 38, 39, 40, 41, 42, 43, 46, 47, 48, 49, 50, 51 };
        private static readonly string[] Names = {
            "NebbiolosStubbornness", "UgniBlancsMist", "TempranillosPassion", "MalbecsDepth",
            "LargeDicePotion", "SmallDicePotion", "FinalDamagePotion", "PotionOfEnchant",
            "MushroomSoup", "PowerPotionMinor", "FlamePotionMinor", "FrostPotionMinor",
            "LightningPotionMinor", "RandomComboPotion", "DefensePotion_Big", "EvasionPotion_Big",
            "DefensePotion", "EvasionPotion", "ElementPotion"
        };
        public static bool Available { get; private set; }

        public static void Load()
        {
            Available = false;
            try
            {
                for (int i = 0; i < Ids.Length; i++) Validate(i);
                Available = true;
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Rabbit level-up potion catalog unavailable: " + error); }
        }

        public static void Clear() => Available = false;

        public static int Pick(System.Random random)
        {
            if (!Available) throw new InvalidOperationException("Rabbit potion catalog is not ready.");
            int index = random.Next(Ids.Length);
            // Other addons can replace database entries after initial validation.
            Validate(index);
            return Ids[index];
        }

        private static void Validate(int index)
        {
            int id = Ids[index];
            ItemEntity item = ItemDatabase.FindItemById(id);
            string effect = id == 33 || id == 34 ? "PotionEffect_DicePotion" :
                id == 38 ? "PotionEffect_Enchant" : id == 39 ? "PotionEffect_ElementalDamageBoost" :
                id == 46 ? "PotionEffect_RandomCombo" : "PotionEffect_StatusInstance";
            if (!item || item.id != id || item.type != EItemType.Potion || item.activeType == EItemActiveType.Disabled ||
                item.aName?.key != "Item_" + Names[index] + "_Name" || !item.resourcePrefab ||
                !item.resourcePrefab.TryGetComponent<WieldingPotion>(out var potion) || !potion ||
                !potion.effect || potion.effect.GetType().Name != effect)
                throw new InvalidOperationException("Native potion identity/effect changed: " + id);
        }
    }
}
