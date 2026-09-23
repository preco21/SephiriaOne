using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ChoicePoints
    {
        private static readonly string[] Keys = ChoiceCommand.Keys;
        private const string MarkerPrefix = "SEPHIRIAONE_";

        private readonly struct Update
        {
            public readonly PlayerAvatar Player;
            public readonly string Key;
            public readonly int Raw;
            public readonly int Applied;
            public Update(PlayerAvatar player, string key, int raw, int applied)
            {
                Player = player; Key = key; Raw = raw; Applied = applied;
            }
        }

        public static bool TryExecute(ChoiceCommand command, out string message)
        {
            if (!NetworkServer.active)
            {
                message = "Only the host can change everyone's candidate choices.";
                return false;
            }
            if (!ChoiceFeature.Available && !command.IsReset)
            {
                message = "Candidate commands are unavailable. Check Player.log for the compatibility error.";
                return false;
            }
            if (!SessionSettings.Prepare(out message)) return false;

            var players = new HashSet<PlayerAvatar>();
            var updates = new List<Update>();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || spawner.netId == 0) continue;
                PlayerAvatar player = spawner.PlayerAvatar;
                if (!SessionSettings.IsReady(spawner))
                {
                    message = "A player is still initializing. Retry in a moment; nobody was changed.";
                    return false;
                }
                if (!players.Add(player)) continue;
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (((int)command.Target & (1 << i)) == 0) continue;
                    string key = Keys[i];
                    player.customStats.TryGetValue(key, out int raw);
                    player.customStats.TryGetValue(MarkerPrefix + key, out int applied);
                    player.calculatedBonusStats.TryGetValue(key, out int bonus);
                    player.customStatsAmp.TryGetValue(key, out int amplifier);
                    if (!command.TryPlan(raw, applied, bonus, amplifier, out int updatedRaw, out int updatedApplied, out message))
                        return false;
                    updates.Add(new Update(player, key, updatedRaw, updatedApplied));
                }
            }
            if (players.Count == 0)
            {
                message = "No active players are ready. Enter town or a run first.";
                return false;
            }

            // Plan every category for every player before touching synchronized state.
            foreach (Update update in updates)
            {
                update.Player.customStats[update.Key] = update.Raw;
                if (update.Applied == 0) update.Player.customStats.Remove(MarkerPrefix + update.Key);
                else update.Player.customStats[MarkerPrefix + update.Key] = update.Applied;
            }
            var recorded = new HashSet<string>();
            foreach (Update update in updates)
                if (recorded.Add(update.Key)) SessionSettings.RememberChoice(update.Key, update.Applied);
            string action = command.IsReset ? "Reset addon bonuses for" : "Updated";
            message = $"{action} {command.Target.ToString().ToLowerInvariant()} extra choices for {players.Count} player(s). Applies to new offers and normal rerolls; available content limits the count.";
            return true;
        }

        public static void RemoveContributions()
        {
            if (!NetworkServer.active) return;
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = spawner ? spawner.PlayerAvatar : null;
                if (!player || !player.isServer) continue;
                foreach (string key in Keys)
                {
                    if (!player.customStats.TryGetValue(MarkerPrefix + key, out int applied)) continue;
                    player.customStats.TryGetValue(key, out int raw);
                    long restored = (long)raw - applied;
                    if (restored < int.MinValue || restored > int.MaxValue)
                        throw new InvalidOperationException("Cannot restore candidate stat: " + key);
                    player.customStats[key] = (int)restored;
                    player.customStats.Remove(MarkerPrefix + key);
                }
            }
        }
    }
}
