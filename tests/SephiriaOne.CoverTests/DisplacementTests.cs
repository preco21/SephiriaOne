using SephiriaOne;
using UnityEngine;
using Mirror;

internal static class DisplacementTests
{
    internal static void Run(Action<bool, string> check)
    {
        var original = PlayerSpawner.MultiplayerList.ToArray();
        PlayerSpawner.MultiplayerList.Clear(); Physics2D.Scene.Clear();
        var floor = new FullyDesignedFloorGenerator { guid = "Displace" };
        var rooms = new PvpCoverRooms(floor, 123);
        var pending = new PvpCoverDisplacement();
        PlayerSpawner Player(int id, float x = 20, float y = 20)
        {
            var connection = new NetworkConnectionToClient { connectionId = id };
            NetworkServer.connections[id] = connection;
            var spawner = new PlayerSpawner { connectionToClient = connection };
            spawner.PlayerAvatar.connectionToClient = connection;
            spawner.PlayerAvatar.currentFloorGuid = floor.guid;
            spawner.PlayerAvatar.transform.position = new Vector3(x, y);
            spawner.PlayerAvatar.TopdownRigidbody.MovementCollider.gameObject.layer = 3;
            PlayerSpawner.MultiplayerList.Add(spawner);
            Physics2D.Scene.Add(spawner.PlayerAvatar.TopdownRigidbody.MovementCollider);
            return spawner;
        }
        var remote = Player(10); remote.PlayerAvatar.Deferred = true;
        check(rooms.Safe(20, 20), "Movable player collider does not reject a cover candidate");
        check(pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Plan safe nearby displacement for guest");
        check(remote.PlayerAvatar.Requests == 1 && pending.Poll(rooms) == PvpCoverDisplacement.Result.Waiting,
            "Native request alone cannot authorize spawn while guest remains inside footprint");
        remote.PlayerAvatar.transform.position = new Vector3(25, 25);
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Waiting, "Walking away cannot acknowledge an in-flight teleport");
        remote.PlayerAvatar.AcknowledgeRequested(); PvpCoverMoveRequests.Reconcile();
        check(PvpCoverMoveRequests.Count == 1, "Acknowledgement without observed arrival retains the destination");
        remote.PlayerAvatar.transform.position = remote.PlayerAvatar.Requested.Value;
        PvpCoverMoveRequests.Reconcile();
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Ready, "Observed guest clearance authorizes spawn");
        var newcomer = Player(11);
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Waiting, "A newcomer entering before spawn blocks final admission");
        Time.realtimeSinceStartup += 2.1f;
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Cancelled && newcomer.PlayerAvatar.Requests == 0,
            "Timeout cancels instead of moving new occupants or resending requests");
        pending.Cancel();
        remote.PlayerAvatar.transform.position = new Vector3(20, 20); remote.PlayerAvatar.Deferred = false;
        for (int i = 12; i < 19; i++) Player(i);
        pending.BeginFrame();
        check(pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Plan all nine stacked players");
        PvpCoverMoveRequests.Reconcile();
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Ready, "Nine stacked players receive nonoverlapping nearby destinations");
        foreach (var a in PlayerSpawner.MultiplayerList) foreach (var b in PlayerSpawner.MultiplayerList)
            if (a != b) check(!PvpCoverRooms.Overlaps(a.PlayerAvatar.transform.position, new Vector2(.6f,.6f),
                b.PlayerAvatar.transform.position, new Vector2(.6f,.6f)), "Destinations reserve space for all displaced players");
        foreach (var p in PlayerSpawner.MultiplayerList) p.PlayerAvatar.transform.position = new Vector3(20, 20);
        int requests = PlayerSpawner.MultiplayerList.Sum(p => p.PlayerAvatar.Requests);
        Physics2D.BlockAll = true; pending.BeginFrame();
        check(!pending.Begin(rooms, floor.guid, new Vector2(20, 20)) &&
            PlayerSpawner.MultiplayerList.Sum(p => p.PlayerAvatar.Requests) == requests,
            "If no complete safe plan exists, nobody is moved");
        Physics2D.BlockAll = false;
        PlayerSpawner.MultiplayerList.RemoveRange(1, 8); Physics2D.Scene.RemoveRange(1, 8);
        remote.PlayerAvatar.Deferred = true; pending.BeginFrame();
        check(pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Prepare another asynchronous move");
        NetworkServer.connections[10] = new NetworkConnectionToClient { connectionId = 10 };
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Cancelled, "Connection replacement invalidates pending placement");
        PvpCoverMoveRequests.Reconcile();
        NetworkServer.connections[10] = remote.connectionToClient; pending.BeginFrame();
        pending.Begin(rooms, floor.guid, new Vector2(20, 20)); remote.PlayerAvatar.currentFloorGuid = "Other";
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Cancelled, "Floor transition invalidates pending displacement");
        PvpCoverMoveRequests.Reconcile();
        check(PvpCoverMoveRequests.Count == 1, "Floor change cannot recall a queued native move or release its destination");
        remote.PlayerAvatar.currentFloorGuid = floor.guid;
        remote.PlayerAvatar.loadingScreenType = 0; remote.connectionToClient.isReady = false;
        PvpCoverMoveRequests.Reconcile();
        check(PvpCoverMoveRequests.Count == 1, "Transient loading/readiness changes preserve outstanding reservations");
        remote.connectionToClient.isReady = true; pending.BeginFrame();
        check(!pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Loading occupants are not forcibly relocated");
        pending.Cancel(); PvpCoverMoveRequests.Reconcile();
        check(!pending.Active && pending.Poll(rooms) == PvpCoverDisplacement.Result.Cancelled, "Cancel leaves no pending action");
        remote.PlayerAvatar.loadingScreenType = -1;
        remote.PlayerAvatar.transform.position = remote.PlayerAvatar.Requested.Value;
        remote.PlayerAvatar.AcknowledgeRequested(); PvpCoverMoveRequests.Reconcile();
        check(PvpCoverMoveRequests.Count == 0, "Returning ready still requires actual acknowledged arrival");
        remote.PlayerAvatar.transform.position = new Vector3(20, 20); pending.BeginFrame();
        check(pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Pending request before timeout");
        var landing = (Vector2)remote.PlayerAvatar.Requested.Value;
        Time.realtimeSinceStartup += 2.1f;
        check(pending.Poll(rooms) == PvpCoverDisplacement.Result.Cancelled, "Unacknowledged request times out placement");
        pending.Cancel();
        check(PvpCoverMoveRequests.Blocks(landing, PvpCoverRooms.CoverSize) && !rooms.Safe(landing.x, landing.y),
            "Cancelled placement retains landing reservation against delayed RPC");
        var reused = new PvpCoverRooms(new FullyDesignedFloorGenerator { guid = "DifferentRun" }, 12);
        check(!reused.Safe(landing.x, landing.y), "Outstanding world-space landing remains protected if floor coordinates are reused");
        pending.BeginFrame(); check(!pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Do not resend while prior teleport is outstanding");
        remote.PlayerAvatar.transform.position = remote.PlayerAvatar.Requested.Value;
        remote.PlayerAvatar.AcknowledgeRequested(); PvpCoverMoveRequests.Reconcile();
        check(PvpCoverMoveRequests.Count == 0, "Delayed acknowledged arrival retires reservation");
        remote.PlayerAvatar.transform.position = new Vector3(22, 20);
        remote.PlayerAvatar.TopdownRigidbody.MovementCollider.Size = new Vector2(5, 1);
        check(!rooms.PlayersClear(new Vector2(20, 20), PvpCoverRooms.CoverSize), "Oversized player bounds are preserved for exclusion");
        check(!pending.Begin(rooms, floor.guid, new Vector2(20, 20)), "Unsupported large occupant cannot authorize spawn");
        remote.PlayerAvatar.TopdownRigidbody.MovementCollider.enabled = false;
        check(!rooms.PlayersClear(new Vector2(20, 20), PvpCoverRooms.CoverSize), "Unreadable player body fails closed");
        PvpCoverMoveRequests.Shutdown();
        PlayerSpawner.MultiplayerList.Clear(); PlayerSpawner.MultiplayerList.AddRange(original); Physics2D.Scene.Clear();
        for (int i = 10; i < 19; i++) NetworkServer.connections.Remove(i);
    }
}
