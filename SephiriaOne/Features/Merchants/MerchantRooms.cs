using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SephiriaOne
{
    internal static class MerchantRooms
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo proceduralRooms = typeof(EnhancedProceduralFloorGenerator).GetField("roomList", Fields);
        private static readonly FieldInfo libraryRooms = typeof(LibraryFloorGenerator).GetField("roomList", Fields);
        private static readonly FieldInfo fixedRooms = typeof(FixedFloorGenerator).GetField("roomInstances", Fields);

        internal static bool ValidateContracts() =>
            proceduralRooms?.FieldType == typeof(List<TileBasedRoomInstance>) &&
            libraryRooms?.FieldType == typeof(List<LibraryFloorRoomInstance>) &&
            fixedRooms?.FieldType == typeof(Dictionary<Vector2Int, TileBasedRoomInstance>);

        private struct Room
        {
            internal Vector2 Min, Max;
            internal Room(Vector2 min, Vector2 max) { Min = min; Max = max; }
        }

        internal static bool TryChoose(FloorGenerator floor, out Vector2 position)
            => TryChoose(floor, 0, out position);

        internal static bool TryChoose(FloorGenerator floor, int variantSalt, out Vector2 position)
        {
            position = default;
            if (!(floor is TileFloorGenerator tiles) || !tiles.ground ||
                floor.floorThreatType == EFloorThreatType.Boss || floor.DataOnServer.threatType == EFloorThreatType.Boss)
                return false;
            var rooms = new List<Room>();
            if (floor is EnhancedProceduralFloorGenerator)
                foreach (var room in (List<TileBasedRoomInstance>)proceduralRooms.GetValue(floor)) Add(rooms, room);
            else if (floor is LibraryFloorGenerator)
            {
                foreach (var room in (List<LibraryFloorRoomInstance>)libraryRooms.GetValue(floor))
                {
                    var metadata = room.Metadata;
                    if (metadata == null || !metadata.spawnMonster) continue;
                    Vector2 origin = (Vector2)floor.transform.position + (Vector2)room.pos;
                    rooms.Add(new Room(origin + (metadata.isCustomSpawnAreaEnabled ? metadata.customSpawnArea_LB : Vector2.zero),
                        origin + (metadata.isCustomSpawnAreaEnabled ? metadata.customSpawnArea_RT : (Vector2)room.Size)));
                }
            }
            else if (floor is FixedFloorGenerator)
                foreach (var room in ((Dictionary<Vector2Int, TileBasedRoomInstance>)fixedRooms.GetValue(floor)).Values) Add(rooms, room);
            else return false; // SingleRoom and unknown generators include boss/special maps.

            var random = new System.Random(floor.seed ^ 0x4D524F4F ^ variantSalt);
            // Shuffle rooms, then select the first with a safe point: room area cannot bias
            // the selection. Both loops are bounded, run only at generation/settings events.
            for (int i = rooms.Count - 1; i > 0; i--)
            { int other = random.Next(i + 1); Room swap = rooms[i]; rooms[i] = rooms[other]; rooms[other] = swap; }
            foreach (Room room in rooms)
            {
                if (room.Max.x - room.Min.x < 3 || room.Max.y - room.Min.y < 3) continue;
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    var point = new Vector2(
                        room.Min.x + 1 + (float)random.NextDouble() * (room.Max.x - room.Min.x - 2),
                        room.Min.y + 1 + (float)random.NextDouble() * (room.Max.y - room.Min.y - 2));
                    if (!Walkable(tiles, point) || Physics2D.OverlapCircle(point, 0.75f, CombatManager.BlockCharacterLayerMask) ||
                        Safe.Find(point)) continue;
                    position = point;
                    return true;
                }
            }
            return false;
        }

        private static void Add(List<Room> rooms, TileBasedRoomInstance room)
        {
            var metadata = room?.Metadata;
            if (metadata == null || !metadata.spawnMonster) return;
            rooms.Add(new Room(room.bottomLeft + (metadata.isCustomSpawnAreaEnabled ? metadata.customSpawnArea_LB : Vector2.zero),
                metadata.isCustomSpawnAreaEnabled ? room.bottomLeft + metadata.customSpawnArea_RT : room.topRight));
        }

        private static bool Walkable(TileFloorGenerator floor, Vector2 point)
        {
            // Check the footprint, not just its center, including native pits/water/cliffs.
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            {
                Vector2 sample = point + new Vector2(x * 0.6f, y * 0.6f);
                TileBase ground = floor.ground.GetTile(floor.ground.WorldToCell(sample));
                if (!ground || Hazard(ground) || Occupied(floor.wall, sample) || Occupied(floor.water, sample) ||
                    Occupied(floor.cliffCollider, sample)) return false;
                if (floor.upperGround && Hazard(floor.upperGround.GetTile(floor.upperGround.WorldToCell(sample)))) return false;
            }
            return true;
        }

        private static bool Hazard(TileBase tile)
        {
            if (!tile) return false;
            GroundTileEntity ground = TileDatabase.FindGroundTile(tile);
            return ground && (ground.type == GroundTileEntity.Type.Pit || ground.type == GroundTileEntity.Type.Water);
        }

        private static bool Occupied(Tilemap map, Vector2 point) => map && map.HasTile(map.WorldToCell(point));
    }
}
