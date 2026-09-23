using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    public sealed class SessionSettingsController : MonoBehaviour
    {
        private void OnEnable() => SessionSettings.Start();
        private void LateUpdate() => SessionSettings.Synchronize();
        private void OnDisable() => SessionSettings.Stop();
    }

    internal static partial class SessionSettings
    {
        private static SessionPolicy policy = new SessionPolicy();
        private static readonly SessionJoinTracker joins = new SessionJoinTracker();
        private static DungeonManager dungeon;
        private static bool enabled;
        private static bool synchronizing;
        private static bool restoreFountainLimit;

        public static void Start()
        {
            Stop();
            store = new PresetStore(System.IO.Path.Combine(Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
            enabled = true;
            HorayModAPI.OnStartSessionServerside += OnStartSession;
        }

        public static void Stop()
        {
            enabled = false;
            HorayModAPI.OnStartSessionServerside -= OnStartSession;
            restoreFountainLimit = false;
            policy.Clear();
            joins.SetSession(null);
            dungeon = null;
            store = null;
        }

        private static void OnStartSession(bool isSaved)
        {
            // NewGame reloads dungeon constants before reinitializing the same
            // avatars. Defer to LateUpdate so their native inventory is ready.
            if (enabled && NetworkServer.active && dungeon && ReferenceEquals(dungeon, DungeonManager.Instance))
                restoreFountainLimit = true;
        }

        public static bool IsReady(PlayerSpawner spawner)
        {
            if (!spawner || !spawner.isServer || spawner.netId == 0 ||
                spawner.connectionToClient == null || !spawner.connectionToClient.isReady) return false;
            PlayerAvatar player = spawner.PlayerAvatar;
            if (!player || !player.isServer || player.netId == 0 || !player.Race ||
                string.IsNullOrEmpty(player.playerNameSource) || string.IsNullOrEmpty(player.currentFloorGuid)) return false;
            GridInventory inventory = player.Inventory;
            return inventory && inventory.isServer && inventory.netId != 0 && inventory.canBroadcast > 0;
        }

        public static bool Prepare(out string message)
        {
            // Process arrivals before a newly typed command, even if LateUpdate
            // has not run yet. The command then operates on their inherited state.
            if (!Synchronize())
            {
                message = "Host session data is not ready. Enter town or a run first.";
                return false;
            }
            message = "";
            return true;
        }

        public static bool Synchronize()
        {
            if (synchronizing) return dungeon;
            DungeonManager current = enabled && NetworkServer.active ? DungeonManager.Instance : null;
            if (!current || !current.isServer || current.netId == 0) current = null;
            if (joins.SetSession(current))
            {
                policy.Clear();
                restoreFountainLimit = false;
                if (current) LoadPreset();
            }
            dungeon = current;
            if (!dungeon) return false;

            synchronizing = true;
            try
            {
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                {
                    if (!IsReady(spawner)) continue;
                    PlayerAvatar player = spawner.PlayerAvatar;
                    if (!joins.TryBegin(player.netId, true) || !policy.HasChanges) continue;
                    try
                    {
                        SessionPlayerSnapshot snapshot = Capture(player);
                        if (!policy.TryPlan(snapshot, out SessionPlan plan, out string error))
                        {
                            Report($"Could not inherit session settings for player {player.netId}: {error}", false);
                            continue;
                        }
                        // All families are planned before writing any inherited value.
                        if (plan.Fountain != null) FountainPoints.ApplyPlan(dungeon, new[] { player }, plan.Fountain);
                        foreach (SessionStatWrite write in plan.Stats)
                        {
                            if (SessionPlayerSnapshot.Read(snapshot.Raw, write.Key) != write.Raw) player.customStats[write.Key] = write.Raw;
                            if (write.Contribution == 0) player.customStats.Remove(write.Marker);
                            else player.customStats[write.Marker] = write.Contribution;
                        }
                        Report($"Applied active session settings to joining player {player.netId}.", true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[SephiriaOne] Inheritance failed for player {player.netId}: {exception}");
                        Report($"Session settings could not finish applying to player {player.netId}. Check Player.log and use explicit commands to reset/configure the group.", false);
                    }
                }
                if (restoreFountainLimit)
                    restoreFountainLimit = !FountainPoints.RestoreCarryoverLimit(dungeon, policy.HasFountainSetting);
            }
            finally { synchronizing = false; }
            return true;
        }

        private static SessionPlayerSnapshot Capture(PlayerAvatar player)
        {
            player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution);
            int? limit = dungeon.constValueDictionary.TryGetValue(FountainPoints.LimitKey, out int current) ? current : (int?)null;
            int? original = dungeon.constValueDictionary.TryGetValue(FountainPoints.OriginalLimitKey, out int first) ? first : (int?)null;
            int? applied = dungeon.constValueDictionary.TryGetValue(FountainPoints.AppliedLimitKey, out int last) ? last : (int?)null;
            return new SessionPlayerSnapshot(new Dictionary<string, int>(player.customStats),
                new Dictionary<string, int>(player.calculatedBonusStats), new Dictionary<string, int>(player.customStatsAmp),
                player.Inventory.dimensionPocket, contribution, limit, original, applied, ChoiceFeature.Available);
        }

        public static void Remember(FountainCommand command) => policy.Record(command);
        public static void Remember(StatCommand command) => policy.Record(command);
        public static void RememberChoice(string key, int contribution) => policy.RecordChoice(key, contribution);

        private static void Report(string message, bool success)
        {
            string text = "[SephiriaOne] " + message;
            if (success) Debug.Log(text);
            else Debug.LogWarning(text);
            if (GameLogWriter.Instance) GameLogWriter.Instance.WriteLog(text, success ? Color.green : Color.yellow);
        }
    }
}
