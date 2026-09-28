using SephiriaOne;
using UnityEngine;
using UnityEngine.Tilemaps;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var normal = new TileBase(); var pit = new TileBase(); var water = new TileBase();
TileDatabase.Ground[normal] = new GroundTileEntity();
TileDatabase.Ground[pit] = new GroundTileEntity { type = GroundTileEntity.Type.Pit };
TileDatabase.Ground[water] = new GroundTileEntity { type = GroundTileEntity.Type.Water };
Tilemap Tiles(TileBase tile) => new() { Tiles = _ => tile };
TileBasedRoomInstance Room(float x = 0, float y = 0, float width = 10, float height = 10) =>
    new() { bottomLeft = new Vector2(x, y), topRight = new Vector2(x + width, y + height) };
EnhancedProceduralFloorGenerator Floor(params TileBasedRoomInstance[] rooms)
{
    Physics2D.Queries = Safe.Queries = 0; Physics2D.Blocked = _ => false; Safe.Nearby = _ => false;
    var floor = new EnhancedProceduralFloorGenerator { ground = Tiles(normal) };
    foreach (var room in rooms) floor.Add(room);
    return floor;
}
bool Within(Vector2 point, float minX, float minY, float maxX, float maxY) =>
    point.x >= minX && point.y >= minY && point.x <= maxX && point.y <= maxY;

Check(MerchantRooms.ValidateContracts(), "Installed room field shapes are required for reflection access");
var floor = Floor(Room());
Check(MerchantRooms.TryChoose(floor, out var point) && Within(point, 1, 1, 9, 9), "Procedural room uses the interior with an edge margin");
Check(MerchantRooms.TryChoose(floor, out var repeated) && point == repeated, "Same floor seed gives a deterministic location");
Check(floor.ground.WorldSamples.Count == 18, "Each accepted point checks all nine footprint samples");

var excluded = Room(100, 100); excluded.Metadata.spawnMonster = false;
floor = Floor(excluded, new TileBasedRoomInstance { Metadata = null }, Room(20, 30));
Check(MerchantRooms.TryChoose(floor, out point) && Within(point, 21, 31, 29, 39),
    "Mixed room lists select only normal monster rooms and skip missing metadata");
floor = Floor(Room(0, 0, 2, 20), Room(0, 0, 20, 2), excluded);
Check(!MerchantRooms.TryChoose(floor, out _) && floor.ground.Queries == 0,
    "Too-small and noncombat rooms are skipped without probing tiles");

var library = new LibraryFloorGenerator { ground = Tiles(normal) };
library.transform.position = new Vector3(100, 200);
library.Add(new LibraryFloorRoomInstance
{
    pos = new Vector2Int(10, 20), Size = new Vector2Int(50, 50),
    Metadata = new RoomMetadata { isCustomSpawnAreaEnabled = true, customSpawnArea_LB = new Vector2(2, 3), customSpawnArea_RT = new Vector2(8, 9) }
});
Check(MerchantRooms.TryChoose(library, out point) && Within(point, 113, 224, 117, 228),
    "Library custom bounds combine floor transform, room position, and spawn-area offsets");
var fixedFloor = new FixedFloorGenerator { ground = Tiles(normal) };
var fixedRoom = Room(40, 60, 100, 100);
fixedRoom.Metadata = new RoomMetadata { isCustomSpawnAreaEnabled = true, customSpawnArea_LB = new Vector2(2, 3), customSpawnArea_RT = new Vector2(7, 8) };
fixedFloor.Add(new Vector2Int(0, 0), fixedRoom);
Check(MerchantRooms.TryChoose(fixedFloor, out point) && Within(point, 43, 64, 46, 67),
    "Fixed room custom bounds are relative to the room's world origin");

floor = Floor(Room(100, 200)); floor.ground.Origin = new Vector2(100, 200);
floor.ground.Tiles = cell => cell.x >= 0 && cell.x < 10 && cell.y >= 0 && cell.y < 10 ? normal : null;
Check(MerchantRooms.TryChoose(floor, out point) && Within(point, 101, 201, 109, 209),
    "Tile probing respects the tilemap's world-to-cell transform");
foreach (bool dataBoss in new[] { false, true })
{
    floor = Floor(Room());
    if (dataBoss) floor.DataOnServer.threatType = EFloorThreatType.Boss;
    else floor.floorThreatType = EFloorThreatType.Boss;
    Check(!MerchantRooms.TryChoose(floor, out _) && floor.ground.Queries == 0, "Boss classification excludes the entire floor");
}
Check(!MerchantRooms.TryChoose(new SingleRoomFloorGenerator { ground = Tiles(normal) }, out _) &&
    !MerchantRooms.TryChoose(new TileFloorGenerator { ground = Tiles(normal) }, out _) &&
    !MerchantRooms.TryChoose(new FloorGenerator(), out _), "Single-room and unknown floor generators fail closed");
floor = Floor(Room()); floor.ground = null;
Check(!MerchantRooms.TryChoose(floor, out _), "Missing ground map cannot supply a safe spawn");

foreach (string obstacle in new[] { "missing-ground", "pit", "water-ground", "upper-pit", "wall", "water", "cliff", "collider", "safe" })
{
    floor = Floor(Room());
    switch (obstacle)
    {
        case "missing-ground": floor.ground = Tiles(null); break;
        case "pit": floor.ground = Tiles(pit); break;
        case "water-ground": floor.ground = Tiles(water); break;
        case "upper-pit": floor.upperGround = Tiles(pit); break;
        case "wall": floor.wall = Tiles(normal); break;
        case "water": floor.water = Tiles(normal); break;
        case "cliff": floor.cliffCollider = Tiles(normal); break;
        case "collider": Physics2D.Blocked = _ => true; break;
        case "safe": Safe.Nearby = _ => true; break;
    }
    Check(!MerchantRooms.TryChoose(floor, out _), "Unsafe placement is rejected: " + obstacle);
    Check(floor.ground.Queries <= 128 * 9 && Physics2D.Queries <= 128 && Safe.Queries <= 128,
        "No available point uses a bounded search: " + obstacle);
}
floor = Floor(Room(0, 0, 3, 3));
floor.ground.Tiles = cell => cell.x == 1 && cell.y == 1 ? normal : pit;
Check(!MerchantRooms.TryChoose(floor, out _), "A safe center with hazardous footprint edges is rejected");
floor = Floor(Room(), Room(50, 50)); floor.ground.Tiles = cell => cell.x < 50 ? pit : normal;
Check(MerchantRooms.TryChoose(floor, out point) && Within(point, 51, 51, 59, 59),
    "A blocked room does not prevent choosing another eligible room");
var blocked = Floor(Room(), Room(50, 50)); blocked.ground = Tiles(pit);
Check(!MerchantRooms.TryChoose(blocked, out _) && blocked.ground.Queries <= 256,
    "Exhaustion across multiple rooms remains bounded");

Console.WriteLine($"Passed {checks} merchant room placement checks.");
