using System.Text;

namespace SephiriaOne
{
    internal static class CombatNames
    {
        internal static string Player(PlayerAvatar player)
        {
            string name = Safe(player.playerNameSource);
            return name != "?" ? name : L.F("Player #{0}", player.spawner && ReferenceEquals(player.spawner.PlayerAvatar, player) && player.spawner.currentPlayerIdx >= 0
                ? (long)player.spawner.currentPlayerIdx + 1 : player.netId);
        }
        internal static string Safe(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            // Most participant-list refreshes receive plain native names. Return
            // their existing string instead of allocating a formatting buffer.
            int length = name.Length < 32 ? name.Length : 32;
            int index = 0;
            for (; index < length; index++)
                if (name[index] == '<' || name[index] == '>' || char.IsControl(name[index])) break;
            if (index == length)
            {
                string result = (name.Length == length ? name : name.Substring(0, length)).Trim();
                return result.Length == 0 ? "?" : result;
            }

            var text = new StringBuilder(32); bool tag = false;
            foreach (char c in name)
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (!tag && !char.IsControl(c))
                {
                    text.Append(c);
                    // Later characters cannot affect the already-full result.
                    if (text.Length == 32) break;
                }
            }
            string plain = text.ToString().Trim(); return plain.Length == 0 ? "?" : plain;
        }
    }
}
