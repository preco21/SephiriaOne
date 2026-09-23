using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;

namespace SephiriaOne
{
    internal static class CharacterStats
    {
        public static bool TryExecute(StatCommand command, out string message)
        {
            // Only local input dispatches here; received chat is never a command.
            if (!NetworkServer.active)
            {
                message = "Only the host can change everyone's character stats.";
                return false;
            }
            if (!SessionSettings.Prepare(out message)) return false;

            var players = new List<PlayerAvatar>();
            var owners = new List<PlayerAvatar>();
            var snapshots = new List<StatSnapshot>();
            var seen = new HashSet<PlayerAvatar>();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || spawner.netId == 0) continue;
                PlayerAvatar player = spawner.PlayerAvatar;
                if (!SessionSettings.IsReady(spawner))
                {
                    message = "A player is still initializing. Wait a moment and retry; nobody was changed.";
                    return false;
                }
                if (!seen.Add(player)) continue;
                players.Add(player);
                foreach (StatDefinition stat in StatCatalog.All)
                {
                    if (command.Stat != null && command.Stat != stat) continue;
                    player.customStats.TryGetValue(stat.Key, out int raw);
                    player.customStats.TryGetValue(stat.Marker, out int contribution);
                    player.calculatedBonusStats.TryGetValue(stat.Key, out int bonus);
                    player.customStatsAmp.TryGetValue(stat.Key, out int amplifier);
                    snapshots.Add(new StatSnapshot(stat, raw, contribution, bonus, amplifier));
                    owners.Add(player);
                }
            }

            if (!SessionSettings.TryPlanStats(command, snapshots, out StatUpdate[] updates, out message)) return false;
            // No writes until every selected stat on every player has been validated.
            // Mirror's existing SyncDictionary carries both values and reset markers.
            for (int i = 0; i < updates.Length; i++)
            {
                StatDefinition stat = snapshots[i].Stat;
                PlayerAvatar player = owners[i];
                if (snapshots[i].Raw != updates[i].Raw) player.customStats[stat.Key] = updates[i].Raw;
                if (updates[i].Contribution == 0) player.customStats.Remove(stat.Marker);
                else player.customStats[stat.Marker] = updates[i].Contribution;
            }
            SessionSettings.Remember(command, players);

            string name = command.Stat == null ? "all supported stats" : command.Stat.Name;
            if (command.Operation == StatOperation.Reset)
            {
                message = $"Reset addon adjustments to {name} for {players.Count} player(s), preserving native stat changes.";
                return true;
            }

            StatDefinition selected = command.Stat;
            decimal minimum = decimal.MaxValue;
            decimal maximum = decimal.MinValue;
            foreach (PlayerAvatar player in players)
            {
                decimal value = selected.Display(player.GetCustomStatUnsafe(selected.Key));
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            string minimumText = minimum.ToString("0.##", CultureInfo.InvariantCulture);
            string values = minimum == maximum ? minimumText : minimumText + ".." + maximum.ToString("0.##", CultureInfo.InvariantCulture);
            message = $"Updated {name} for {players.Count} player(s). Value now: {values} {selected.Unit}.";
            return true;
        }
    }
}
