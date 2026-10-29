using SephiriaOne;
using UnityEngine;
using UnityEngine.Tilemaps;

internal static class RoomAdapterTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(PvpCoverRooms.Validate(), "Native room field fixture contracts");
        var entrance = new EnhancedProceduralFloorGenerator(); entrance.TestRoom.Metadata.spawnMonster = false;
        var entranceRooms = new PvpCoverRooms(entrance, 7);
        check(entranceRooms.Plan.RoomCount == 1 && entranceRooms.Safe(20.5f, 20.5f), "PvP entrance cover does not depend on native monster spawning");
        entrance.ground.Contains = p => p.x >= 19 && p.x <= 21 && p.y >= 19 && p.y <= 21;
        check(entranceRooms.Safe(20.5f, 20.5f), "A clear 3x3 ground square leaves a walkable bypass for compact props");
        entrance.ground.Contains = p => p.x >= 20 && p.x <= 21 && p.y >= 19 && p.y <= 21;
        check(!entranceRooms.Safe(20.5f, 20.5f), "A narrow route without a complete bypass remains unavailable");
        entrance.ground.Contains = p => p.x >= 0 && p.x < 100 && p.y >= 0 && p.y < 100;
        var rest = new PvpCoverRooms(new SingleRoomFloorGenerator(), 7);
        check(rest.Plan.RoomCount == 1 && rest.Safe(20.5f, 20.5f), "Non-boss single-room rest floor supports PvP cover");
        var hurtbox = new Collider2D { Position = new Vector2(22.5f, 20.5f) }; hurtbox.gameObject.layer = 6;
        Physics2D.Scene.Add(hurtbox);
        check(entranceRooms.Safe(20.5f, 20.5f), "Damage hitbox in bypass ring is not a movement wall");
        hurtbox.Position = new Vector2(20.5f, 20.5f);
        check(!entranceRooms.Safe(20.5f, 20.5f), "Do not overlap a damageable entity at the barrel footprint");
        hurtbox.Position = new Vector2(22, 20.5f); hurtbox.gameObject.layer = 12;
        check(!entranceRooms.Safe(20.5f, 20.5f), "Physical wall still requires full bypass clearance");
        hurtbox.gameObject.layer = 3;
        check(entranceRooms.Safe(20.5f, 20.5f), "Grounded actor body on Blockable is not a movement wall in the bypass ring");
        hurtbox.Position = new Vector2(20.5f, 20.5f);
        check(!entranceRooms.Safe(20.5f, 20.5f), "Do not overlap grounded actor bodies or Blockable props at the footprint");
        check(entranceRooms.Diagnostics.Contains("occupied=2") && entranceRooms.Diagnostics.Contains("colliders=1") &&
            entranceRooms.Diagnostics.Contains("firstBlocker="), "Diagnostics distinguish footprint occupancy from physical obstacles");
        Physics2D.Scene.Clear();
        var boss = new SingleRoomFloorGenerator { floorThreatType = EFloorThreatType.Boss };
        check(new PvpCoverRooms(boss, 7).Plan.RoomCount == 1, "Boss threat metadata does not exclude a verified playable arena");
        entrance.transform.position = new Vector3(2500, 500); entrance.ground.transform.position = entrance.transform.position;
        entrance.TestRoom.bottomLeft = new Vector2(2500, 500); entrance.TestRoom.topRight = new Vector2(2600, 600);
        entrance.TestRoom.TeleportPoint = new Vector2(15, 10);
        entranceRooms = new PvpCoverRooms(entrance, 7);
        check(entranceRooms.Safe(2520.5f, 520.5f) && !entranceRooms.Safe(2515, 510), "Procedural bounds and reserved teleports use native world offsets");
        Physics2D.BlockAll = false;
        var map = new FullyDesignedFloorGenerator();
        map.transform.position = new Vector3(4000, 500);
        map.TestGround.transform.position = map.transform.position;
        map.DataOnServer.stageName = "MultiZone";
        var rooms = new PvpCoverRooms(map, 7);
        check(rooms.UnavailableReason == null && rooms.Plan.RoomCount == 1, "Authored lobby supported without race-lobby name match");
        check(rooms.Safe(4010.5f, 510.5f), "World-offset lobby ground accepted");
        check(!rooms.Safe(10.5f, 10.5f), "Empty world outside ground cannot be treated as walkable");

        map.TestGround.transform.position = map.transform.position + new Vector3(.6f, .6f);
        map.TestGround.Contains = p => p.x >= 8 && p.x <= 10 && p.y >= 8 && p.y <= 10;
        check(!rooms.Safe(4010.5f, 510.5f), "Fractional grid offsets cannot hide missing cells under the bypass edge");
        map.TestGround.transform.position = map.transform.position;

        map.TestGround.Contains = p => p.x >= 0 && p.x < 30 && p.y >= 0 && p.y < 30 && !(p.x == 11 && p.y == 10);
        check(!rooms.Safe(4010.5f, 510.5f), "A missing ground cell anywhere in bypass ring rejects cover");
        map.TestGround.Contains = p => p.x >= 0 && p.x < 30 && p.y >= 0 && p.y < 30;
        map.TestGround.Tile.Entity.type = GroundTileEntity.Type.Water;
        check(!rooms.Safe(4010.5f, 510.5f), "Water rejected");
        map.TestGround.Tile.Entity.type = GroundTileEntity.Type.Pit;
        check(!rooms.Safe(4010.5f, 510.5f), "Pits rejected");
        map.TestGround.Tile.Entity.type = GroundTileEntity.Type.Normal;
        var interaction = new Interactable(); interaction.transform.position = new Vector3(4010.5f, 510.5f);
        var prop = new GameObject(); prop.Interactions.Add(interaction); map.props.Add(prop);
        rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(4010.5f, 510.5f), "Authored props list reserves native NPC/menu/portal interactions");
        interaction.transform.position = new Vector3(4020, 520);
        check(rooms.Safe(4010.5f, 510.5f), "Moving native interaction updates protected position");
        var teleport = new TeleportPoint(); teleport.transform.position = new Vector3(4000, 500); teleport.offset = new Vector3(10.5f, 10.5f);
        map.mapTeleportConnections.Add(new() { point = teleport }); rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(4010.5f, 510.5f), "Map destination offset is reserved");
        map.mapTeleportConnections.Clear(); map.props.Clear();
        map.spawnPoints.Add(new() { SpawnPoint = new Vector3(4010.5f, 510.5f) }); rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(4010.5f, 510.5f), "Native spawn point reserved"); map.spawnPoints.Clear();
        rooms = new PvpCoverRooms(map, 7);
        Physics2D.BlockAll = true; check(!rooms.Safe(4010.5f, 510.5f), "Native collision veto remains"); Physics2D.BlockAll = false;
        var wall = new Tilemap { Contains = p => p.x == 10 && p.y == 10 };
        wall.transform.position = map.transform.position; wall.transform.Component = wall; map.transform.Paths["Grid/WallCollider"] = wall.transform;
        rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(4010.5f, 510.5f), "Authored wall tiles remain protected"); map.transform.Paths.Remove("Grid/WallCollider");
        map.TestGround.cellSize = new Vector3(0.5f, 0.5f, 1); rooms = new PvpCoverRooms(map, 7);
        check(rooms.Plan.Exhausted && rooms.UnavailableReason.Contains("grid"), "Unsupported scale reports reason");
        map.TestGround.cellSize = Vector3.one;
        map.floorThreatType = EFloorThreatType.Boss; rooms = new PvpCoverRooms(map, 7);
        check(rooms.UnavailableReason == null && rooms.Safe(4010.5f, 510.5f), "Authored boss arena uses the same geometry checks"); map.floorThreatType = default;
        map.transform.Paths.Clear(); rooms = new PvpCoverRooms(map, 7);
        check(rooms.Plan.Exhausted && rooms.UnavailableReason.Contains("ground"), "Missing ground fails closed with a reason");
        map.transform.Paths["Grid/Pit/Ground"] = map.TestGround.transform; rooms = new PvpCoverRooms(map, 7);
        check(rooms.Safe(4010.5f, 510.5f), "Rabbittown nested ground supported");
        foreach (var native in new TileFloorGenerator[] { new EnhancedProceduralFloorGenerator(), new FixedFloorGenerator(), new LibraryFloorGenerator(), new SingleRoomFloorGenerator() })
        {
            rooms = new PvpCoverRooms(native, 7);
            check(rooms.UnavailableReason == null && rooms.Safe(20.5f, 20.5f), "Retain procedural/fixed/library/single-room support: " + native.GetType().Name);
        }
        FinalBoss(check);
    }

    private static void FinalBoss(Action<bool, string> check)
    {
        var map = new FullyDesignedFloorGenerator { name = "QTemple_Boss3(Clone)", floorThreatType = EFloorThreatType.Boss };
        map.DataOnServer.threatType = EFloorThreatType.Boss;
        map.transform.position = new Vector3(2000, 500);
        map.TestGround.transform.position = map.transform.position;
        map.TestGround.cellSize = new Vector3(2, 2, 0);
        map.TestGround.cellLayout = GridLayout.CellLayout.Isometric;
        var rooms = new PvpCoverRooms(map, 7);
        check(rooms.UnavailableReason == null && rooms.Safe(2000, 540), "Final boss 2x2 isometric grid has playable ground");
        // Opposite world corners alone map to the same isometric X cell.
        // A missing side cell still lies underneath the full world-space bypass.
        map.TestGround.Contains = p => p.x != 19 || p.y != 21;
        check(!rooms.Safe(2000, 540), "All four world corners contribute to isometric clearance");
        map.TestGround.Contains = _ => false;
        var lit = new Tilemap { cellSize = new Vector3(2, 2, 0), cellLayout = GridLayout.CellLayout.Isometric };
        lit.transform.position = map.transform.position; lit.transform.Component = lit;
        map.transform.Paths["Grid/Ground-Lit"] = lit.transform;
        rooms = new PvpCoverRooms(map, 7);
        check(rooms.Safe(2000, 540), "Lit main-arena ground is an audited playable surface");
        lit.gameObject.activeInHierarchy = false;
        check(!rooms.Safe(2000, 540), "Inactive ground cannot authorize placement over empty space");
        lit.gameObject.activeInHierarchy = true;
        lit.Contains = p => p.x < 20;
        map.TestGround.Contains = p => p.x >= 20;
        check(rooms.Safe(2000, 540), "Adjacent ground layers together support the complete bypass");
        var exit = new Interactable(); exit.transform.position = new Vector3(2000, 540);
        map.Hierarchy.Add(exit); rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(2000, 540), "Inactive victory exits outside authored props lists remain clear");
        map.Hierarchy.Clear();
        var teleport = new TeleportTogether { offset = new Vector3(0, 10) }; teleport.transform.position = new Vector3(2000, 530);
        map.Hierarchy.Add(teleport); rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(2000, 530) && !rooms.Safe(2000, 540), "Party teleport source and destination remain clear");
        map.Hierarchy.Clear();
        var spawner = new BossSpawner { spawnOffset = new Vector2(0, 10), bossRewardPosition = new Vector2(0, 15) };
        spawner.transform.position = new Vector3(2000, 530); map.Hierarchy.Add(spawner);
        rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(2000, 540) && !rooms.Safe(2000, 545), "Boss spawn and reward anchors remain clear");
        map.Hierarchy.Clear();
        var transition = new BossEnvironment_QQBoss { qqqBossPoint = new Transform(), qqqBossDramaticDieReturnPoint = new Transform() };
        transition.qqqBossPoint.position = new Vector3(2000, 540); transition.qqqBossDramaticDieReturnPoint.position = new Vector3(2000, 550);
        map.Hierarchy.Add(transition); rooms = new PvpCoverRooms(map, 7);
        check(!rooms.Safe(2000, 540) && !rooms.Safe(2000, 550), "Final-boss scripted entry and return positions remain clear");
        map.Hierarchy.Clear(); map.TestGround.Contains = _ => false; lit.Contains = _ => false;
        var finalGround = new Tilemap { cellSize = new Vector3(2, 2, 0), cellLayout = GridLayout.CellLayout.Isometric };
        finalGround.transform.position = map.transform.position; finalGround.transform.Component = finalGround;
        map.transform.Paths["QQQGrid/QQQGround"] = finalGround.transform;
        rooms = new PvpCoverRooms(map, 7);
        check(rooms.Safe(2000, 556), "The final-phase arena has its own supported top surface");
        finalGround.Tile.Entity.type = GroundTileEntity.Type.Pit;
        check(!rooms.Safe(2000, 556), "Supplemental ground never overrides hazard rejection");
        finalGround.Tile.Entity.type = GroundTileEntity.Type.Normal;
        finalGround.transform.position = map.transform.position + new Vector3(.25f, 0);
        check(new PvpCoverRooms(map, 7).UnavailableReason?.Contains("grid") == true, "Misaligned ground lattices are not combined");
        finalGround.transform.position = map.transform.position;
        map.TestGround.layoutGrid.cellGap = new Vector3(.1f, 0);
        check(new PvpCoverRooms(map, 7).Plan.Exhausted, "Gapped tile grids cannot imply continuous ground");
        map.TestGround.layoutGrid.cellGap = Vector3.zero;
        map.TestGround.cellSwizzle = GridLayout.CellSwizzle.XZY;
        check(new PvpCoverRooms(map, 7).Plan.Exhausted, "A grid swizzle that moves terrain out of the XY plane is unsupported");
        map.TestGround.cellSwizzle = GridLayout.CellSwizzle.XYZ;
        finalGround.tileAnchor = new Vector3(.75f, .75f, 0);
        finalGround.transform.position = map.transform.position + new Vector3(0, -.5f);
        check(new PvpCoverRooms(map, 7).Plan.Exhausted, "Matching centers must not hide shifted ground-cell boundaries through a compensating anchor");
    }
}
