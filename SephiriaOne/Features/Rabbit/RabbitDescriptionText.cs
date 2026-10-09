namespace SephiriaOne
{
    internal static class RabbitDescriptionText
    {
        private const string Infinite = "- <indent=10>HP potions, except Potion of Regeneration (Sample), are not consumed after a successful drink (requires one potion). These potions can be used during boss combat with Tension.</indent>";
        private const string Share = "- <indent=10>Nearby allies within 5 tiles receive the same healing from HP potions, except Potion of Regeneration (Sample).</indent>";
        private const string Survival = "- <indent=10>HP potion drinks, except Potion of Regeneration (Sample), do not trigger Survival rank 5 random-stat gains.</indent>";

        internal static string Decorate(string original, string costumeId, bool infinite, bool share, bool compatible,
            bool consumeMp = false, bool suppressSurvival = false, int mpCost = RabbitPotionSettings.DefaultMpCostPerDrink,
            bool levelUpPotion = false, bool levelUpCompatible = false)
        {
            string native = original ?? "";
            if (native.Length == 0 || costumeId != "HolyRabbit") return native;
            if (compatible)
            {
                if (infinite) native = Append(native, L.T(Infinite));
                if (share) native = Append(native, L.T(Share));
                if (consumeMp) native = Append(native, L.F("- <indent=10>Each Wing-Eared Rabbit HP potion drink, except Potion of Regeneration (Sample), costs {0} MP; insufficient MP blocks the drink.</indent>", mpCost));
                if (suppressSurvival) native = Append(native, L.T(Survival));
            }
            if (levelUpPotion && levelUpCompatible)
                native = Append(native, L.T("- <indent=10>Each earned level grants one random non-HP/MP potion while wearing Wing-Eared Rabbit.</indent>"));
            return native;
        }

        private static string Append(string description, string line) =>
            description.Length == 0 ? line : description + "\n" + line;

    }
}
