using SephiriaOne;
using UnityEngine;

internal static class CrossFloorTeleportTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        PlayerSpawner host = null, stuck = null, same = null;
        DungeonManager dungeon = null;
        void Fresh()
        {
            host = start(); dungeon = DungeonManager.Instance;
            Mirror.NetworkServer.localConnection = host.connectionToClient;
            host.PlayerAvatar.currentFloorGuid = "next-stage";
            host.PlayerAvatar.currentSpawnPoint = "BOSS_EXIT";
            host.PlayerAvatar.transform.position = new(123, 456, 0);
            var data = new FloorData { name = "next", stageName = "chapter2" };
            dungeon.generatedFloors["next-stage"] = data;
            FloorGenerator.Floors.Clear();
            FloorGenerator.Floors["next-stage"] = new() { netId = 99, DataOnServer = data };
            stuck = add(2, 1, 1, 0); stuck.PlayerAvatar.currentFloorGuid = "boss-room";
            same = add(3, 1, 1, 0); same.PlayerAvatar.currentFloorGuid = "next-stage";
        }
        Fresh();
        check(SettingsActions.Execute("/one teleportall").Success && dungeon.TravelRequests.SequenceEqual(new[] { stuck.PlayerAvatar }),
            "Stranded player receives native travel to host stage");
        check(stuck.PlayerAvatar.currentFloorGuid == "boss-room" && stuck.PlayerAvatar.Teleports.Count == 0 && stuck.PlayerAvatar.loadingScreenType == 0 &&
            same.PlayerAvatar.Teleports.Single() == host.PlayerAvatar.transform.position,
            "Cross-floor travel waits natively; same-floor teleport remains immediate");
        check(SettingsActions.Execute("/one teleportall").Success && dungeon.TravelRequests.Count == 1,
            "Repeated click does not duplicate queued travel");
        stuck.PlayerAvatar.loadingScreenType = -1;
        check(SettingsActions.Execute("/one teleportall").Success && dungeon.TravelRequests.Count == 1,
            "Native queue prevents overlap even if another path cleared the loading indicator");
        var destination = host.PlayerAvatar.transform.position;
        host.PlayerAvatar.transform.position = new(900, 900, 0);
        dungeon.CompleteTravel();
        check(stuck.PlayerAvatar.currentFloorGuid == "next-stage" && stuck.PlayerAvatar.currentSpawnPoint == "BOSS_EXIT" &&
            stuck.PlayerAvatar.Teleports.Single() == destination && stuck.PlayerAvatar.loadingScreenType == -1,
            "Native completion synchronizes the destination floor and captured position exactly once");
        check(SettingsActions.Execute("/one teleportall").Success && dungeon.TravelRequests.Count == 1 && stuck.PlayerAvatar.Teleports.Count == 2,
            "After arrival further requests use the original same-floor path");
        foreach (string failure in new[] { "metadata", "generator", "generating", "stale-data", "dead", "unready", "disconnected", "avatar-replaced" })
        {
            Fresh();
            switch (failure)
            {
                case "metadata": dungeon.generatedFloors.Remove("next-stage"); break;
                case "generator": FloorGenerator.Floors.Clear(); break;
                case "generating": FloorGenerator.Floors["next-stage"].GenerateSuccess = false; break;
                case "stale-data": FloorGenerator.Floors["next-stage"].DataOnServer = new(); break;
                case "dead": stuck.PlayerAvatar.IsDead = true; break;
                case "unready": stuck.connectionToClient.isReady = false; break;
                case "disconnected": Mirror.NetworkServer.connections.Remove(stuck.connectionToClient.connectionId); break;
                case "avatar-replaced": stuck.PlayerAvatar.spawner = same; break;
            }
            SettingsActions.Execute("/one teleportall");
            check(dungeon.TravelRequests.Count == 0 && stuck.PlayerAvatar.Teleports.Count == 0 && same.PlayerAvatar.Teleports.Count == 1,
                "Unsafe cross-floor target/destination leaves same-floor requests usable: " + failure);
        }
        foreach (bool hostCountdown in new[] { true, false })
        {
            Fresh();
            (hostCountdown ? host : same).PlayerAvatar.MovingFloorViaWorldmap = true;
            check(!SettingsActions.Execute("/one teleportall").Success && dungeon.TravelRequests.Count == 0 && same.PlayerAvatar.Teleports.Count == 0,
                "Party world-map countdown blocks recovery before native loading/queue begins: host=" + hostCountdown);
            (hostCountdown ? host : same).PlayerAvatar.MovingFloorViaWorldmap = false;
            check(SettingsActions.Execute("/one teleportall").Success && dungeon.TravelRequests.Count == 1,
                "Cancelling countdown immediately restores recovery eligibility");
        }
        foreach (string interruption in new[] { "run", "host-floor", "host-loading", "worldmap", "disconnect-next", "throw", "reentrant" })
        {
            Fresh(); same.PlayerAvatar.currentFloorGuid = "boss-room";
            bool rejected = false;
            dungeon.OnTravel = _ =>
            {
                switch (interruption)
                {
                    case "run": SaveManager.CurrentRun = new(); break;
                    case "host-floor": host.PlayerAvatar.currentFloorGuid = "different"; break;
                    case "host-loading": host.PlayerAvatar.loadingScreenType = 0; break;
                    case "worldmap": host.PlayerAvatar.MovingFloorViaWorldmap = true; break;
                    case "disconnect-next": same.connectionToClient = new(); break;
                    case "throw": throw new Exception("Injected native travel failure");
                    case "reentrant": rejected = !SettingsActions.Execute("/one teleportall").Success; break;
                }
            };
            SettingsActions.Execute("/one teleportall");
            check(dungeon.TravelRequests.Count == (interruption is "throw" or "reentrant" ? 2 : 1),
                "Native travel callback retains captured session/target guards: " + interruption);
            if (interruption == "reentrant") check(rejected, "Recursive cross-floor requests are rejected");
            check(stuck.PlayerAvatar.Teleports.Count == 0 && same.PlayerAvatar.Teleports.Count == 0,
                "No premature position write after cross-floor callback: " + interruption);
        }
        FloorGenerator.Floors.Clear(); start();
    }
}
