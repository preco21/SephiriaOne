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
        private int reservedRejected, playerRejected, colliderRejected, occupiedRejected, terrainRejected;
        private string firstBlocker;
        internal string UnavailableReason { get; private set; }
        internal string Diagnostics => $"reserved={reservedRejected}; players={playerRejected}; colliders={colliderRejected}; occupied={occupiedRejected}; terrain={terrainRejected}; firstBlocker={firstBlocker ?? "none"}";
        internal readonly PvpCoverPlacement Plan;
        internal static bool Validate() => procedural?.FieldType == typeof(List<TileBasedRoomInstance>) &&
            library?.FieldType == typeof(List<LibraryFloorRoomInstance>) &&
            fixedRooms?.FieldType == typeof(Dictionary<Vector2Int, TileBasedRoomInstance>) && single?.FieldType == typeof(TileBasedRoomInstance);

        internal PvpCoverRooms(FloorGenerator floor, int salt)
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
                // Cover follows PvP, not the monster-spawn policy. Entrances and
                // rest rooms have usable ground with spawnMonster=false.
                if (room?.Metadata == null) return;
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
                        if (room?.Metadata == null) continue;
                        Vector2 origin = (Vector2)floor.transform.position + (Vector2)room.pos;
                        Add(origin, origin + room.Size, origin + room.TeleportPoint);
                    }
                else if (floor is SingleRoomFloorGenerator && single.GetValue(floor) is TileBasedRoomInstance room && room.Metadata != null)
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
            // Preserve the full bypass around physical walls/props/hazards.
            // TopdownWorld_1F contains DAMAGE hitboxes; Blockable includes grounded
            // actor bodies (TopdownRigidbody.UpdateAirLayer), not movement walls.
            // Both must clear the barrel footprint plus a margin, not the entire
            // bypass ring. Character movement itself uses BlockCharacterLayerMask.
            // Stock costumes have a 0.5-unit movement diameter. Even the largest
            // audited cover prop (1.35 wide) retains a 0.825-unit side bypass.
            var blocker = Physics2D.OverlapBox(point, new Vector2(3, 3), 0, CombatManager.BlockCharacterLayerMask);
            if (blocker) { colliderRejected++; RecordBlocker(blocker); return false; }
            blocker = Physics2D.OverlapBox(point, new Vector2(1.85f, 1.5f), 0,
                (1 << LayerMask.NameToLayer("TopdownWorld_1F")) | (1 << LayerMask.NameToLayer("Blockable")));
            if (blocker) { occupiedRejected++; RecordBlocker(blocker); return false; }
            // Room origins need not align with the tile grid. Check every cell
            // touched by the full bypass, not only nine point samples that can
            // miss an unsupported edge on a fractionally offset map.
            var min = ground.WorldToCell(point - new Vector2(1.5f, 1.5f));
            var max = ground.WorldToCell(point + new Vector2(1.499f, 1.499f));
            for (int cx = min.x; cx <= max.x; cx++) for (int cy = min.y; cy <= max.y; cy++)
            {
                var cell = new Vector3Int(cx, cy, min.z);
                Vector2 sample = ground.CellToWorld(cell) + new Vector3(0.5f, 0.5f);
                TileBase tile = ground.GetTile(cell);
                if (!tile || Hazard(tile) || Occupied(wall, sample) || Occupied(water, sample) || Occupied(cliffCollider, sample) ||
                    upperGround && Hazard(upperGround.GetTile(upperGround.WorldToCell(sample)))) { terrainRejected++; return false; }
            }
            return true;
        }
        private void RecordBlocker(Collider2D blocker)
        {
            // Capture once per bounded search, not once per rejected candidate.
            if (firstBlocker == null) firstBlocker = $"{blocker.name}/{blocker.GetType().Name}/layer={blocker.gameObject.layer}";
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
