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
            bool reset = command.Operation == StatOperation.Reset || (command.Operation == StatOperation.Multiply && command.Amount == 1);
            if (!SessionSettings.PrepareCommand("stats", reset, out HostCommandContext context, out message, command.Stat == null)) return false;

            List<PlayerAvatar> players = context.Players;
            var owners = new List<PlayerAvatar>();
            var snapshots = new List<StatSnapshot>();
            foreach (PlayerAvatar player in players)
            {
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
            StateWriteBatch batch = context.CreateBatch();
            for (int i = 0; i < updates.Length; i++)
            {
                StatDefinition stat = snapshots[i].Stat;
                PlayerAvatar player = owners[i];
                NativeStateWrites.Stat(batch, player, stat.Key, stat.Marker, updates[i].Raw, updates[i].Contribution);
            }
            if (!SessionSettings.Commit("stats", batch, () => SessionSettings.Remember(command, players), out message)) return false;

            string name = command.Stat == null ? "all supported stats" : command.Stat.Name;
            if (reset)
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
