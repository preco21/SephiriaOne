using System.Collections.Generic;

namespace SephiriaOne
{
    // Basic ASCII markers use existing chat/UI glyphs. Pack whole names before
    // the stock receiver truncates messages to 120 characters.
    internal static class ZombieSurvivalMessages
    {
        internal static List<string> Starting(List<string> names)
        {
            var lines = new List<string>();
            if (names.Count == 1)
            {
                lines.Add(L.F("[Z] \"{0}\" is a Zombie!", QuoteName(names[0])));
                return lines;
            }
            string prefix = L.T("[Z] Zombies:") + " ", line = prefix;
            foreach (string name in names)
            {
                string quoted = "\"" + QuoteName(name) + "\"";
                if (line.Length > prefix.Length && line.Length + quoted.Length + 2 > 120)
                { lines.Add(line); line = prefix; }
                line += (line.Length == prefix.Length ? "" : ", ") + quoted;
            }
            if (line.Length > prefix.Length) lines.Add(line);
            return lines;
        }

        internal static string Conversion(string infector, int seconds) => string.IsNullOrEmpty(infector)
            ? L.F("[Z] Infected! Reviving as Zombie in {0}s - hunt remaining [H] Humans!", seconds)
            : L.F("[Z] \"{0}\" infected you! Reviving as Zombie in {1}s - hunt remaining [H] Humans!", QuoteName(infector), seconds);

        private static string QuoteName(string name) => CombatNames.Safe(name).Replace('"', '\'');
    }
}
