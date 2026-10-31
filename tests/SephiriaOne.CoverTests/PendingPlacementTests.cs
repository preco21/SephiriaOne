using SephiriaOne;
using UnityEngine;

internal static class PendingPlacementTests
{
    internal static void Run(Action<bool, string> check, PlayerSpawner host)
    {
        PvpCoverRuntime.Clear(); PvpCoverMoveRequests.Shutdown();
        var floor = new FullyDesignedFloorGenerator { guid = "Pending" };
        FloorGenerator.Floors[floor.guid] = floor; DungeonManager.Instance.generatedFloors[floor.guid] = floor.DataOnServer;
        host.PlayerAvatar.currentFloorGuid = floor.guid; host.PlayerAvatar.Deferred = true;
        SessionSettings.FriendlyFireForDisplay.Enabled = true;
        SessionSettings.FriendlyFireForDisplay.Cover = new(true, 1);
        int probes = 0, requests = host.PlayerAvatar.Requests;
        Physics2D.BlockPoint = point =>
        {
            if (++probes < PvpCoverPlacement.MaximumAttempts) return true;
            if (probes == PvpCoverPlacement.MaximumAttempts) host.PlayerAvatar.transform.position = point;
            return false;
        };
        for (int i = 0; i < 64; i++) { Time.frameCount++; PvpCoverRuntime.Tick(); }
        check(host.PlayerAvatar.Requests == requests + 1 && PvpCoverRuntime.ActiveCount == 0,
            "The final search attempt can enter asynchronous displacement");
        Physics2D.BlockPoint = null;
        host.PlayerAvatar.transform.position = host.PlayerAvatar.Requested.Value;
        host.PlayerAvatar.AcknowledgeRequested();
        Time.frameCount++; PvpCoverRuntime.Tick();
        check(PvpCoverRuntime.ActiveCount == 1 && PvpCoverRuntime.Describe().StartsWith("Cover placed"),
            "Exhausted search still completes a confirmed pending placement");
        PvpCoverRuntime.Clear();
        bool first = true;
        Physics2D.BlockPoint = point => { if (first) { first = false; host.PlayerAvatar.transform.position = point; } return false; };
        Time.frameCount++; PvpCoverRuntime.Tick(); Physics2D.BlockPoint = null;
        check(PvpCoverMoveRequests.Count == 1 && PvpCoverRuntime.ActiveCount == 0, "Second deployment waits for movement");
        SessionSettings.FriendlyFireForDisplay.Cover = default;
        Time.frameCount++; PvpCoverRuntime.Tick();
        check(PvpCoverRuntime.ActiveCount == 0 && PvpCoverMoveRequests.Count == 1,
            "Turning cover off cancels spawn but retains outstanding native landing reservation");
        host.PlayerAvatar.transform.position = host.PlayerAvatar.Requested.Value;
        host.PlayerAvatar.AcknowledgeRequested(); Time.frameCount++; PvpCoverRuntime.Tick();
        check(PvpCoverMoveRequests.Count == 0 && PvpCoverRuntime.ActiveCount == 0, "Late acknowledgement never spawns cancelled cover");
        host.PlayerAvatar.Deferred = false;
        SessionSettings.FriendlyFireForDisplay.Cover = new(true, 1); first = true;
        Physics2D.BlockPoint = point => { if (first) { first = false; host.PlayerAvatar.transform.position = point; } return false; };
        host.PlayerAvatar.OnRequest = () => { SessionSettings.FriendlyFireForDisplay.Cover = default; PvpCoverRuntime.Tick(); };
        Time.frameCount++; PvpCoverRuntime.Tick();
        check(PvpCoverRuntime.ActiveCount == 0, "Native movement callback invalidates scope before cover spawn");
        host.PlayerAvatar.OnRequest = null; Physics2D.BlockPoint = null;
        PvpCoverRuntime.Clear(); PvpCoverMoveRequests.Shutdown();
    }
}
