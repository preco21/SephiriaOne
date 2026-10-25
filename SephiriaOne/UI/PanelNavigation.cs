namespace SephiriaOne
{
    // Stable IDs preserve form dispatch, snapshot scope and remembered selection.
    internal static class PanelNavigation
    {
        internal const int PageCount = 21;
        internal sealed class Section
        {
            internal readonly string Label;
            internal readonly int Icon;
            internal readonly bool Secondary;
            internal readonly int[] Pages;
            internal Section(string label, int icon, bool secondary, params int[] pages)
            { Label = label; Icon = icon; Secondary = secondary; Pages = pages; }
        }
        internal static readonly Section[] Sections =
        {
            new Section("Stats & resources", 0, false, 0, 1, 2, 3, 8),
            new Section("Costume features", 1, false, 6, 11),
            new Section("Fun", 2, false, 7, 10),
            new Section("PvP", 3, false, 9, 13, 14, 15, 16, 18, 19),
            new Section("Growth", 4, false, 17),
            new Section("Optimization", 5, false, 20),
            new Section("System", 6, true, 5, 4, 12)
        };
        private static readonly string[] labels =
        {
            "Character stats", "Fountain", "Reward choices", "Resources", "Settings presets", "Logs",
            "Wing-Eared Rabbit healer options", "Golden Goblin merchants", "Items", "PvP combat", "Event probabilities",
            "Other costume options", "Updates", "Deathmatch", "Tournament", "Play Tag", "Zombie Survival",
            "XP gain restrictions", "PvP balance (basic)", "PvP balance (sustained hits)", "Projectile optimization"
        };
        internal static string Label(int page) => labels[page];
        internal static bool Secondary(int page) => page == 5 || page == 4 || page == 12;
    }
}
