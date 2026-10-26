using System.Collections.Generic;

namespace SephiriaOne
{
    // Presentation only. Keep clauses short enough for the stock chat receiver.
    internal static class CostumeRoleText
    {
        internal static bool Companion(string id) => id == "Mole" || id == "Squirrel" || id == "Turtle";
        internal static bool Supported(string id) => id == "HolyRabbit" || Companion(id);

        internal static List<string> Rabbit(RabbitPotionSettings settings, bool potions, bool levelUp)
        {
            var parts = new List<string> { L.T("Wing-Eared Rabbit · Healer") };
            if (potions)
            {
                if (settings.Infinite) parts.Add(L.T("Unlimited HP outside PvP; PvP uses its own option"));
                if (settings.ConsumeMp) parts.Add(L.F("HP potion: {0} MP", settings.MpCostPerDrink));
                if (settings.Share) parts.Add(L.F(settings.BlockPvpShare ? "Shared heal outside PvP: {0} tiles" : "Shared heal (including PvP): {0} tiles", RabbitPotionSettings.ShareRadiusTiles));
                if (settings.Infinite || settings.ConsumeMp || settings.Share) parts.Add(L.T("Sample excluded"));
            }
            if (levelUp && settings.LevelUpPotion) parts.Add(L.T("Level-up: random non-HP/MP potion"));
            return parts;
        }

        internal static List<string> CompanionParts(string costume, bool collin)
        {
            string name = costume == "Mole" ? L.T("Mole") : costume == "Squirrel" ? L.T("Farmer Squirrel") : L.T("Turtle");
            var parts = new List<string> { L.F("{0} · Companions", name) };
            if (collin) parts.Add(L.T("Starting artifact: Collin"));
            return parts;
        }

        internal static List<string> Pack(List<string> parts)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string part in parts)
            {
                // Custom catalogs can contain arbitrarily long translations.
                string clean = ChatTextSanitizer.Sanitize(part, 120);
                if (clean.Length == 0) continue;
                if (line.Length != 0 && line.Length + clean.Length + 3 > 120)
                { lines.Add(line); line = ""; }
                line = line.Length == 0 ? clean : line + " | " + clean;
            }
            if (line.Length != 0) lines.Add(line);
            return lines;
        }
    }
}
