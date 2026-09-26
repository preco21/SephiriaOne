namespace SephiriaOne
{
    internal static class RabbitDescriptionText
    {
        private const string Infinite = "- <indent=10>HP potions are not consumed after a successful drink (requires one potion).</indent>";
        private const string Share = "- <indent=10>Nearby allies within 5 tiles receive the same HP potion healing.</indent>";
        private const string Survival = "- <indent=10>HP potion drinks do not trigger Survival rank 5 random-stat gains.</indent>";

        internal static string Decorate(string original, string costumeId, bool infinite, bool share, bool compatible,
            bool consumeMp = false, bool suppressSurvival = false, int mpCost = RabbitPotionSettings.DefaultMpCostPerDrink)
        {
            string native = original ?? "";
            if (native.Length == 0 || costumeId != "HolyRabbit" || !compatible) return native;
            if (infinite) native = Append(native, Infinite);
            if (share) native = Append(native, Share);
            if (consumeMp) native = Append(native, "- <indent=10>Each Wing-Eared Rabbit HP potion drink costs " +
                mpCost + " MP; insufficient MP blocks the drink.</indent>");
            if (suppressSurvival) native = Append(native, Survival);
            return native;
        }

        private static string Append(string description, string line) =>
            description.Length == 0 ? line : description + "\n" + line;

    }
}
