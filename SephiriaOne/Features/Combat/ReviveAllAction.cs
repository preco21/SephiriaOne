using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // A one-shot recovery action, independent of policy reconciliation/faults.
    // Native revival owns HP, inventory, controls, camera and guest replication.
    internal static class ReviveAllAction
    {
        private static bool executing;
        internal static bool IsExecuting => executing;
        [ThreadStatic] private static PlayerAvatar reviving;
        private static Func<bool> currentScope;
        private static int callbackErrors;
        internal static bool IsRecovering(UnitAvatar player) => executing && ReferenceEquals(reviving, player);
        internal static void CheckScope()
        { if (currentScope == null || !currentScope()) throw new InvalidOperationException("Revival session changed during a callback."); }
        internal static void CallbackFailed(Exception error)
        { callbackErrors++; Debug.LogWarning("[SephiriaOne] Revive-all callback failed; continuing native recovery: " + error); }
        internal static string Usage => L.T("Host only: /one reviveall restores all dead players at full HP in the current session.");
        // victoryType describes the eventual result, not whether play has ended:
        // chapter progression sets it to 2/6 while the current run stays playable.
        // UI_GameOverLabel.OnOpened disables the run save on actual settlement.
        // Share this gate with deathmatch and recheck it after native callbacks.
        internal static bool IsRunOpen(DungeonManager dungeon, SaveData run) =>
            dungeon && run != null && run.enableSave && !dungeon.requestLeaveOnHost;
        internal static bool CanExecute
        {
            get
            {
                var dungeon = DungeonManager.Instance;
                return ReviveAllFeature.Available && !executing && NetworkServer.active && dungeon && dungeon.isServer && dungeon.netId != 0 &&
                    IsRunOpen(dungeon, SaveManager.CurrentRun);
            }
        }

        internal static bool TryExecute(out string message) => Execute(null, null, out message);

        // Timed recovery shares the same native callback containment and exact
        // avatar validation as the manual party action. Never revive a new life
        // or a replacement avatar on behalf of an expired match timer.
        internal static bool TryRevive(HostPlayer player, Func<bool> scope, out string message) => Execute(player, scope, out message);

        private static bool Execute(HostPlayer only, Func<bool> scope, out string message)
        {
            message = L.T("Only the host can revive all players.");
            if (!NetworkServer.active) return false;
            message = L.T("Revive all is already running.");
            if (executing) return false;
            message = L.T("Revive-all compatibility checks failed. Inspect Player.log; no players were changed.");
            if (!ReviveAllFeature.Available) return false;
            var dungeon = DungeonManager.Instance;
            message = L.T("Host session data is not ready. Enter town or a run first.");
            if (!dungeon || !dungeon.isServer || dungeon.netId == 0 || SaveManager.CurrentRun == null) return false;
            message = L.T("This run is ending or has already ended. Revive all cannot undo game-over settlement; start a new run.");
            if (!CanExecute) return false;
            var run = SaveManager.CurrentRun;
            long generation = SessionSettings.ResourceGeneration;
            uint dungeonId = dungeon.netId;
            bool CurrentScope() => NetworkServer.active && dungeon && dungeon.isServer && dungeon.netId == dungeonId &&
                ReferenceEquals(dungeon, DungeonManager.Instance) && ReferenceEquals(run, SaveManager.CurrentRun) &&
                generation == SessionSettings.ResourceGeneration && IsRunOpen(dungeon, run) &&
                (scope == null || scope());
            if (!CurrentScope()) return false;
            var candidates = new List<HostPlayer>();
            var seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
            if (only != null) candidates.Add(only);
            else
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                if (spawner && spawner.isServer && spawner.PlayerAvatar && spawner.PlayerAvatar.IsDead && seen.Add(spawner.PlayerAvatar))
                    candidates.Add(new HostPlayer(spawner));
            if (candidates.Count == 0) { message = L.T("No dead players to revive."); return true; }

            executing = true;
            callbackErrors = 0;
            int restored = 0, failed = 0;
            try
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    HostPlayer candidate = candidates[i];
                    // Revival callbacks may end the run, disconnect someone or
                    // replace an avatar. Never carry the operation into new state.
                    if (!CurrentScope()) { failed += candidates.Count - i; break; }
                    if (!candidate.IsReady || !PlayerSpawner.MultiplayerList.Contains(candidate.Spawner) ||
                        !ReferenceEquals(candidate.Player.spawner, candidate.Spawner)) { failed++; continue; }
                    var player = candidate.Player;
                    if (!player.IsDead) continue; // Another native revival already handled this player.
                    float hp = player.MaxHp;
                    if (float.IsNaN(hp) || float.IsInfinity(hp) || hp <= 0) { failed++; continue; }
                    try
                    {
                        reviving = player;
                        currentScope = () => CurrentScope() && candidate.IsReady && PlayerSpawner.MultiplayerList.Contains(candidate.Spawner) &&
                            ReferenceEquals(player.spawner, candidate.Spawner);
                        player.Revive(hp);
                        if (!CurrentScope()) { failed += candidates.Count - i; break; }
                        if (!player || player.IsDead || !candidate.IsReady || !PlayerSpawner.MultiplayerList.Contains(candidate.Spawner) ||
                            player.hp <= 0 || float.IsNaN(player.hp) || float.IsInfinity(player.hp)) { failed++; continue; }
                        FriendlyFireKda.ForgetLife(player);
                        restored++;
                    }
                    catch (Exception error)
                    { failed++; Debug.LogWarning("[SephiriaOne] Revive all failed for player " + candidate.Id + ": " + error); }
                    finally { reviving = null; currentScope = null; }
                }
            }
            finally { executing = false; currentScope = null; reviving = null; }
            message = failed == 0 ? L.F("Revived {0} player(s) at full HP.", restored) :
                L.F("Revived {0} player(s); {1} could not be restored. Retry once they are ready; inspect Player.log if this persists.", restored, failed);
            if (callbackErrors > 0) message += " " + L.F("{0} revival callback(s) failed. Native recovery continued; inspect Player.log.", callbackErrors);
            return failed == 0 && callbackErrors == 0;
        }
    }
}
