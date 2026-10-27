using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SephiriaOne
{
    internal sealed class PvpCoverRooms
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo procedural = typeof(EnhancedProceduralFloorGenerator).GetField("roomList", Fields);
        private static readonly FieldInfo library = typeof(LibraryFloorGenerator).GetField("roomList", Fields);
        private static readonly FieldInfo fixedRooms = typeof(FixedFloorGenerator).GetField("roomInstances", Fields);
        private static readonly FieldInfo single = typeof(SingleRoomFloorGenerator).GetField("floorInstance", Fields);
        private readonly FloorGenerator floor;
        private readonly Tilemap ground, upperGround, wall, water, cliffCollider;
        private readonly List<Vector2> protectedPoints = new List<Vector2>();
        private readonly List<Transform> interactions = new List<Transform>();
        private int reservedRejected, playerRejected, colliderRejected, terrainRejected;
        internal string UnavailableReason { get; private set; }
        internal string Diagnostics => $"reserved={reservedRejected}; players={playerRejected}; colliders={colliderRejected}; terrain={terrainRejected}";
        internal readonly PvpCoverPlacement Plan;
        internal static bool Validate() => procedural?.FieldType == typeof(List<TileBasedRoomInstance>) &&
            library?.FieldType == typeof(List<LibraryFloorRoomInstance>) &&
            fixedRooms?.FieldType == typeof(Dictionary<Vector2Int, TileBasedRoomInstance>) && single?.FieldType == typeof(TileBasedRoomInstance);

        internal PvpCoverRooms(FloorGenerator floor, bool lobby, int salt)
        {
            this.floor = floor;
            var rooms = new List<PvpCoverPlacement.Room>();
            if (floor is TileFloorGenerator tiles)
            { ground = tiles.ground; upperGround = tiles.upperGround; wall = tiles.wall; water = tiles.water; cliffCollider = tiles.cliffCollider; }
            else if (floor is FullyDesignedFloorGenerator)
            {
                // Native waiting rooms are NOT TileFloorGenerator subclasses.
                // These explicit ground paths are audited in MultiZone/Rabbittown;
                // do not treat arbitrary decorative tilemaps or empty space as ground.
                ground = Map("Grid/Ground") ?? Map("Grid/Pit/Ground");
                upperGround = Map("Grid/Upper") ?? Map("Grid/Pit/UpperGround");
                wall = Map("Grid/WallCollider") ?? Map("Grid/Wall");
                water = Map("Grid/Water");
            }
            else UnavailableReason = "Cover does not support this level layout.";
            void Add(Vector2 min, Vector2 max, Vector2? teleport = null)
            { rooms.Add(new PvpCoverPlacement.Room(min.x, min.y, max.x, max.y)); if (teleport.HasValue) protectedPoints.Add(teleport.Value); }
            void TileRoom(TileBasedRoomInstance room)
            {
                if (room?.Metadata == null || !room.Metadata.spawnMonster && !lobby) return;
                Add(room.bottomLeft, room.topRight, room.bottomLeft + room.TeleportPoint);
            }
            if (floor.floorThreatType == EFloorThreatType.Boss || floor.DataOnServer.threatType == EFloorThreatType.Boss)
                UnavailableReason = "Cover is not placed in boss arenas.";
            else
            {
                if (floor is EnhancedProceduralFloorGenerator)
                    foreach (var room in (List<TileBasedRoomInstance>)procedural.GetValue(floor)) TileRoom(room);
                else if (floor is FixedFloorGenerator)
                    foreach (var room in ((Dictionary<Vector2Int, TileBasedRoomInstance>)fixedRooms.GetValue(floor)).Values) TileRoom(room);
                else if (floor is LibraryFloorGenerator)
                    foreach (var room in (List<LibraryFloorRoomInstance>)library.GetValue(floor))
                    {
                        if (room?.Metadata == null || !room.Metadata.spawnMonster) continue;
                        Vector2 origin = (Vector2)floor.transform.position + (Vector2)room.pos;
                        Add(origin, origin + room.Size, origin + room.TeleportPoint);
                    }
                else if (floor is SingleRoomFloorGenerator && lobby && single.GetValue(floor) is TileBasedRoomInstance room)
                { Vector2 origin = floor.transform.position; Add(origin, origin + room.size, origin + room.TeleportPoint); }
                else if (floor is FullyDesignedFloorGenerator designed)
                {
                    Vector2 origin = floor.transform.position;
                    Add(origin + designed.bottomLeft, origin + designed.topRight);
                    foreach (var connection in designed.mapTeleportConnections)
                        if (connection?.point) protectedPoints.Add(connection.point.GetTeleportTargetPosition());
                }
            }
            var seen = new HashSet<Transform>();
            void ProtectInteractions(IEnumerable<GameObject> props)
            {
                foreach (var prop in props)
                {
                    if (!prop) continue;
                    foreach (var interaction in prop.GetComponentsInChildren<Interactable>(true))
                        if (seen.Add(interaction.transform)) interactions.Add(interaction.transform);
                }
            }
            ProtectInteractions(PvpCoverNative.Props(floor));
            // Scene-authored NPCs, portals and doors live in this separate list.
            if (floor is FullyDesignedFloorGenerator authored) ProtectInteractions(authored.props);
            foreach (var spawn in floor.spawnPoints) if (spawn) protectedPoints.Add(spawn.SpawnPoint);
            // The bypass samples are one world unit apart, matching these native
            // tile grids. Do not infer safety for a differently scaled map.
            if (!ground) UnavailableReason = UnavailableReason ?? "Cover could not find a supported ground tilemap.";
            else if (ground.cellSize.x != 1 || ground.cellSize.y != 1 ||
                ground.transform.lossyScale.x != 1 || ground.transform.lossyScale.y != 1 ||
                ground.transform.rotation != Quaternion.identity) UnavailableReason = "Cover does not support this tile grid.";
            if (UnavailableReason != null) rooms.Clear();
            else if (rooms.Count == 0) UnavailableReason = "Cover found no eligible rooms on this level.";
            Plan = new PvpCoverPlacement(rooms, floor.seed ^ salt);
        }
        private Tilemap Map(string path)
        {
            var child = floor.transform.Find(path);
            return child ? child.GetComponent<Tilemap>() : null;
        }
        internal bool Safe(float x, float y)
        {
            Vector2 point = new Vector2(x, y);
            foreach (var reserved in protectedPoints) if ((reserved - point).sqrMagnitude < 16) { reservedRejected++; return false; }
            foreach (var interaction in interactions) if (interaction && ((Vector2)interaction.position - point).sqrMagnitude < 16) { reservedRejected++; return false; }
            foreach (var spawner in PlayerSpawner.MultiplayerList)
                if (spawner && spawner.PlayerAvatar && spawner.PlayerAvatar.currentFloorGuid == floor.guid &&
                    ((Vector2)spawner.PlayerAvatar.transform.position - point).sqrMagnitude < 16) { playerRejected++; return false; }
            // A continuous collider probe plus all ground cells of the bypass
            // square. No blocking props, pits, water or cliffs can hide between samples.
            if (Physics2D.OverlapBox(point, new Vector2(5, 5), 0,
                CombatManager.BlockCharacterLayerMask | (1 << LayerMask.NameToLayer("Blockable")) |
                (1 << LayerMask.NameToLayer("TopdownWorld_1F")))) { colliderRejected++; return false; }
            for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++)
            {
                Vector2 sample = point + new Vector2(dx, dy);
                TileBase tile = ground.GetTile(ground.WorldToCell(sample));
                if (!tile || Hazard(tile) || Occupied(wall, sample) || Occupied(water, sample) || Occupied(cliffCollider, sample) ||
                    upperGround && Hazard(upperGround.GetTile(upperGround.WorldToCell(sample)))) { terrainRejected++; return false; }
            }
            return true;
        }
        private static bool Occupied(Tilemap map, Vector2 point) => map && map.HasTile(map.WorldToCell(point));
        private static bool Hazard(TileBase tile)
        {
            if (!tile) return false;
            var ground = TileDatabase.FindGroundTile(tile);
            return ground && (ground.type == GroundTileEntity.Type.Pit || ground.type == GroundTileEntity.Type.Water);
        }
    }
}
