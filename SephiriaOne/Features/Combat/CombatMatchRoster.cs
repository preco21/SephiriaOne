using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static void CaptureRoster(List<RosterEntry> entries, Dictionary<PlayerAvatar, int> indices)
        {
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || !FriendlyFireParticipants.Includes(spawner.PlayerAvatar) || indices.ContainsKey(spawner.PlayerAvatar)) continue;
                var player = spawner.PlayerAvatar;
                var receipt = FriendlyFireParticipants.Capture(player);
                if (receipt == null) continue;
                indices.Add(player, entries.Count);
                entries.Add(new RosterEntry { Subject = new HostPlayer(spawner), Receipt = receipt });
            }
        }
    }
}
