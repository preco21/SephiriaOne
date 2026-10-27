using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class PvpCoverRuntime
    {
        private static readonly List<GameObject> objects = new List<GameObject>(PvpCoverSettings.MaximumCount);
        private static DungeonManager dungeon;
        private static TileFloorGenerator floor;
        private static object run;
        private static long generation, match;
        private static PvpCoverRooms rooms;
        private static GameObject prefab;
        private static Bounds dirtyBounds;
        private static bool dirty, working, failed;
        private static int requested, issued, lastFrame = -1;
        internal static int ActiveCount => objects.Count;

        internal static void Tick()
        {
            if (working) return;
            working = true;
            try
            {
                var settings = SessionSettings.FriendlyFireForDisplay;
                var current = DungeonManager.Instance;
                if (!NetworkServer.active || !NetworkClient.active || !PvpCoverFeature.Available || !FriendlyFireFeature.Available ||
                    !settings.Cover.Enabled || !settings.Enabled && !CombatMatchRuntime.IsRunning || !current || !current.isServer ||
                    !ReviveAllAction.IsRunOpen(current, SaveManager.CurrentRun))
                { Clear(); return; }
                PlayerAvatar host = null;
                if (NetworkServer.localConnection != null)
                    foreach (var player in PlayerSpawner.MultiplayerList)
                        if (player && player.PlayerAvatar && ReferenceEquals(player.connectionToClient, NetworkServer.localConnection))
                        { host = player.PlayerAvatar; break; }
                var currentFloor = host && !string.IsNullOrEmpty(host.currentFloorGuid) ? FloorGenerator.FindByGuid(host.currentFloorGuid) as TileFloorGenerator : null;
                if (!host || host.loadingScreenType >= 0 || !currentFloor || !currentFloor.GenerateSuccess || !currentFloor.isServer ||
                    !currentFloor.ground || currentFloor.DataOnServer == null ||
                    !current.generatedFloors.TryGetValue(currentFloor.guid, out var data) || !ReferenceEquals(data, currentFloor.DataOnServer))
                { Clear(); return; }
                if (!ReferenceEquals(dungeon, current) || !ReferenceEquals(run, SaveManager.CurrentRun) ||
                    generation != SessionSettings.ResourceGeneration || match != CombatMatchRuntime.MatchToken || !ReferenceEquals(floor, currentFloor))
                {
                    Clear();
                    if (objects.Count != 0) return; // retain/retry failed native despawns
                    dungeon = current; run = SaveManager.CurrentRun; floor = currentFloor;
                    generation = SessionSettings.ResourceGeneration; match = CombatMatchRuntime.MatchToken;
                    bool lobby = host.Race && host.Race.lobbyStage && data.stageName == host.Race.lobbyStage.name;
                    rooms = new PvpCoverRooms(floor, lobby, UnityEngine.Random.Range(1, int.MaxValue));
                    prefab = PvpCoverNative.Prefab();
                }
                if (failed) { if (objects.Count != 0) Cleanup(false); return; }
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (!objects[i]) { PvpCoverNative.Remove(floor, objects[i]); objects.RemoveAt(i); }
                PvpCoverNative.PruneDestroyed();
                if (requested != settings.Cover.Count)
                {
                    requested = settings.Cover.Count;
                    while (objects.Count > requested) RemoveLast();
                    issued = Math.Min(issued, requested);
                    FlushPathfinding();
                }
                // Notifications may run repeatedly during a frame. Never multiply
                // the work budget or refresh cover due to unrelated settings.
                if (lastFrame == Time.frameCount || issued >= requested || rooms.Plan.Exhausted) return;
                lastFrame = Time.frameCount;
                for (int probe = 0; probe < 2 && issued < requested && !rooms.Plan.Exhausted; probe++)
                    if (rooms.Plan.TryNext(rooms.Safe, out float x, out float y))
                    {
                        var instance = PvpCoverNative.Spawn(floor, prefab, new Vector2(x, y), objects.Add);
                        issued++;
                        Dirty(new Bounds(new Vector3(x, y), new Vector3(3, 3, 1)));
                    }
                // Native erosion can visit the whole graph. Coalesce all placement
                // updates; do not perform one full erosion pass per barrel/frame.
                if (issued >= requested || rooms.Plan.Exhausted) FlushPathfinding();
            }
            catch (Exception error)
            {
                Cleanup(false); failed = true;
                Debug.LogWarning("[SephiriaOne] PvP cover stopped; cleanup requested: " + error);
            }
            finally { working = false; }
        }
        private static void Dirty(Bounds bounds)
        { if (dirty) dirtyBounds.Encapsulate(bounds); else { dirty = true; dirtyBounds = bounds; } }
        private static void FlushPathfinding()
        {
            if (!dirty) return;
            dirty = false;
            var grid = PathGrid.Current;
            // UpdateRegion queues updates during ScanAsync. Checking IsBuilt
            // here would discard removals made after a chunk was scanned.
            if (grid && grid.Width > 0 && grid.Height > 0)
            {
                var gridBounds = new Bounds((Vector3)grid.Center, new Vector3(grid.Width * grid.CellSize, grid.Height * grid.CellSize, 2));
                if (gridBounds.Intersects(dirtyBounds))
                    try { grid.UpdateRegion(dirtyBounds); }
                    catch (Exception error) { Debug.LogWarning("[SephiriaOne] Cover pathfinding refresh failed: " + error.Message); }
            }
        }
        private static void RemoveLast()
        {
            var instance = objects[objects.Count - 1];
            if (instance) Dirty(new Bounds(instance.transform.position + new Vector3(0, 0.4375f), new Vector3(3, 3, 1)));
            PvpCoverNative.Remove(floor, instance);
            objects.RemoveAt(objects.Count - 1);
        }
        internal static void Clear() => Cleanup(true);
        private static void Cleanup(bool resetScope)
        {
            // Keep any failed removal tracked. Retry on the next reconciliation
            // instead of silently orphaning a networked obstacle.
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                var instance = objects[i];
                try
                {
                    if (instance) Dirty(new Bounds(instance.transform.position + new Vector3(0, 0.4375f), new Vector3(3, 3, 1)));
                    PvpCoverNative.Remove(floor, instance); objects.RemoveAt(i);
                }
                catch (Exception error) { Debug.LogWarning("[SephiriaOne] PvP cover cleanup will retry: " + error.Message); }
            }
            FlushPathfinding();
            PvpCoverNative.PruneDestroyed();
            if (objects.Count != 0 || !resetScope) return;
            dungeon = null; floor = null; run = null; rooms = null; prefab = null;
            requested = issued = 0; lastFrame = -1; failed = false;
        }
    }
}
