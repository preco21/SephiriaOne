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
            var text = new StringBuilder(32); bool tag = false;
            foreach (char c in name ?? "")
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (!tag && !char.IsControl(c) && text.Length < 32) text.Append(c);
            }
            string plain = text.ToString().Trim(); return plain.Length == 0 ? "?" : plain;
        }
    }
}
