namespace SephiriaOne
{
    internal static class RabbitDescriptionText
    {
        private const string Infinite = "- <indent=10>Healing potions are not consumed after a successful drink (requires one potion).</indent>";
        private const string Share = "- <indent=10>Nearby allies within 5 tiles receive the same potion healing.</indent>";

        internal static string Decorate(string original, string costumeId, bool infinite, bool share, bool compatible)
        {
            string native = original ?? "";
            if (native.Length == 0 || costumeId != "HolyRabbit" || !compatible) return native;
            if (infinite) native = Append(native, Infinite);
            if (share) native = Append(native, Share);
            return native;
        }

        private static string Append(string description, string line) =>
            description.Length == 0 ? line : description + "\n" + line;

    }
}
