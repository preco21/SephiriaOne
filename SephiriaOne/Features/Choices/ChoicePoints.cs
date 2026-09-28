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
        private static StateWriteBatch cleanup;
        private static DungeonManager cleanupDungeon;

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
                message = L.T("Only the host can change everyone's candidate choices.");
                return false;
            }
            if (!ChoiceFeature.Available && !command.IsReset)
            {
                message = L.T("Candidate commands are unavailable. Check Player.log for the compatibility error.");
                return false;
            }
            if (!SessionSettings.PrepareCommand("choices", command.IsReset, out HostCommandContext context, out message, command.Target == ChoiceTarget.All)) return false;

            List<PlayerAvatar> players = context.Players;
            var updates = new List<Update>();
            foreach (PlayerAvatar player in players)
            {
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
                message = L.T("No active players are ready. Enter town or a run first.");
                return false;
            }

            StateWriteBatch batch = context.CreateBatch();
            foreach (Update update in updates)
            {
                NativeStateWrites.Stat(batch, update.Player, update.Key, MarkerPrefix + update.Key, update.Raw, update.Applied);
            }
            if (!SessionSettings.Commit("choices", batch, () =>
            {
                var recorded = new HashSet<string>();
                foreach (Update update in updates)
                    if (recorded.Add(update.Key)) SessionSettings.RememberChoice(update.Key, update.Applied);
            }, out message)) return false;
            string target = command.Target.ToString().ToLowerInvariant();
            message = command.IsReset
                ? L.F("Reset addon bonuses for {0} extra choices for {1} player(s). Use new offers; existing offers stay cached. An already opened anvil can hide reroll after an increase.", target, players.Count)
                : L.F("Updated {0} extra choices for {1} player(s). Use new offers; existing offers stay cached. An already opened anvil can hide reroll after an increase.", target, players.Count);
            return true;
        }

        public static void RemoveContributions()
        {
            if (!NetworkServer.active) return;
            DungeonManager current = DungeonManager.Instance;
            if (!ReferenceEquals(cleanupDungeon, current)) cleanup = null;
            if (cleanup != null)
            {
                if (!SessionSettings.Recover(cleanup, out string recoveryError))
                    throw new InvalidOperationException(recoveryError + " " + cleanup.Describe());
                cleanup = null;
            }
            var participants = new List<PlayerAvatar>();
            var dictionaries = new List<object>();
            var batch = new StateWriteBatch(() =>
            {
                if (!NetworkServer.active || !ReferenceEquals(current, DungeonManager.Instance)) return false;
                for (int i = 0; i < participants.Count; i++)
                    if (!participants[i] || !participants[i].isServer || !ReferenceEquals(participants[i].customStats, dictionaries[i])) return false;
                return true;
            });
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = spawner ? spawner.PlayerAvatar : null;
                if (!player || !player.isServer || participants.Contains(player)) continue;
                participants.Add(player); dictionaries.Add(player.customStats);
                foreach (string key in Keys)
                {
                    if (!player.customStats.TryGetValue(MarkerPrefix + key, out int applied)) continue;
                    player.customStats.TryGetValue(key, out int raw);
                    long restored = (long)raw - applied;
                    if (restored < int.MinValue || restored > int.MaxValue)
                        throw new InvalidOperationException("Cannot restore candidate stat: " + key);
                    NativeStateWrites.Stat(batch, player, key, MarkerPrefix + key, (int)restored, 0);
                }
            }
            // Shutdown removes generation guards only after all cleanup readbacks succeed.
            if (!SessionSettings.Commit("choices", batch, () => { }, out string error))
            {
                if (batch.MayHaveWritten)
                {
                    cleanup = batch; cleanupDungeon = current;
                    SessionSettings.RecordFault("choices", batch, error, () => cleanup = null);
                }
                throw new InvalidOperationException(error + " " + batch.Describe());
            }
        }
    }
}
