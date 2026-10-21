using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // One-shot native movement requests. No transform writes, polling, queued
    // grants or dependency on PvP participation/compatibility.
    internal static class TeleportAllAction
    {
        private static bool executing;
        internal static string Usage => L.T("Host only: /one teleportall moves ready, living players on your current floor to your position. Loading players are skipped; it does not revive players or change floors.");

        private sealed class Target
        {
            internal readonly HostPlayer Subject;
            internal readonly NetworkConnectionToClient Connection;
            private readonly NetworkIdentity identity;
            internal Target(PlayerSpawner spawner)
            { Subject = new HostPlayer(spawner); Connection = spawner.connectionToClient; identity = spawner.netIdentity; }
            internal bool Ready(string floor) => Subject.IsReady && !Subject.Player.IsDead &&
                Subject.Player.loadingScreenType < 0 && Subject.Player.currentFloorGuid == floor &&
                PlayerSpawner.MultiplayerList.Contains(Subject.Spawner) &&
                ReferenceEquals(Subject.Player.spawner, Subject.Spawner) && Connection != null && identity &&
                ReferenceEquals(Subject.Spawner.connectionToClient, Connection) &&
                ReferenceEquals(Subject.Player.connectionToClient, Connection) &&
                ReferenceEquals(Subject.Spawner.netIdentity, identity) && ReferenceEquals(Connection.identity, identity) &&
                NetworkServer.connections.TryGetValue(Connection.connectionId, out var connected) && ReferenceEquals(connected, Connection);
        }

        internal static bool TryExecute(out string message)
        {
            message = L.T("Only the host can teleport all players.");
            if (!NetworkServer.active) return false;
            message = L.T("Teleport all is already running.");
            if (executing) return false;
            var dungeon = DungeonManager.Instance;
            var run = SaveManager.CurrentRun;
            message = L.T("Host session data is not ready. Enter town or a run first.");
            if (!dungeon || !dungeon.isServer || dungeon.netId == 0 || !ReviveAllAction.IsRunOpen(dungeon, run)) return false;
            Target host = null;
            var local = NetworkServer.localConnection;
            if (local != null)
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                    if (spawner && ReferenceEquals(spawner.connectionToClient, local))
                    { host = new Target(spawner); break; }
            message = L.T("The host must be alive and ready on a loaded floor to teleport the party.");
            if (host == null || !host.Subject.Player) return false;
            string floor = host.Subject.Player.currentFloorGuid;
            long generation = SessionSettings.ResourceGeneration;
            uint dungeonId = dungeon.netId;
            bool Current() => NetworkServer.active && dungeon && dungeon.isServer && dungeon.netId == dungeonId &&
                ReferenceEquals(DungeonManager.Instance, dungeon) && ReferenceEquals(SaveManager.CurrentRun, run) &&
                SessionSettings.ResourceGeneration == generation && ReviveAllAction.IsRunOpen(dungeon, run) &&
                !dungeon.IsHostTraveling && ReferenceEquals(NetworkServer.localConnection, local) && host.Ready(floor);
            if (!Current()) return false;
            Vector3 destination = host.Subject.Player.transform.position;
            message = L.T("The host's position is invalid. No teleport requests were sent.");
            if (!Finite(destination.x) || !Finite(destination.y) || !Finite(destination.z)) return false;
            var targets = new List<Target>();
            var seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance) { host.Subject.Player };
            int skipped = 0;
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || !seen.Add(spawner.PlayerAvatar)) continue;
                var target = new Target(spawner);
                if (target.Ready(floor)) targets.Add(target); else skipped++;
            }
            int sent = 0, failed = 0;
            executing = true;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    // Native position callbacks can disconnect/replace players,
                    // change floors or end the session. Never follow into it.
                    if (!Current()) { failed += targets.Count - i; break; }
                    var target = targets[i];
                    if (!target.Ready(floor)) { skipped++; continue; }
                    try
                    {
                        // Host sends the game's existing TargetRpc to the owning
                        // client; native transform sync broadcasts its movement.
                        // Remote completion is asynchronous: report requests,
                        // not unverified final positions, and never force retries.
                        target.Subject.Player.ReqSetPosition(destination, teleport: true);
                        sent++;
                    }
                    catch (Exception error)
                    { failed++; Debug.LogWarning("[SephiriaOne] Teleport request failed for player " + target.Subject.Id + ": " + error); }
                }
            }
            finally { executing = false; }
            message = L.F("Teleport requested for {0} player(s); {1} skipped; {2} failed. Destination: host position.", sent, skipped, failed);
            return failed == 0;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
