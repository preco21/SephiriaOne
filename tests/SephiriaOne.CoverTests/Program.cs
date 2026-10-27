using SephiriaOne;
using UnityEngine;

int checks = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
var host = new PlayerSpawner(); PlayerSpawner.MultiplayerList.Add(host);
TileFloorGenerator Floor(string guid)
{
    var floor = new EnhancedProceduralFloorGenerator { guid = guid }; FloorGenerator.Floors[guid] = floor;
    DungeonManager.Instance.generatedFloors[guid] = floor.DataOnServer; return floor;
}
var first = Floor("A");
void Frame()
{
    int before = PvpCoverNative.Spawns, probesBefore = Physics2D.Queries;
    Time.frameCount++; PvpCoverRuntime.Tick();
    Check(PvpCoverNative.Spawns - before <= 2 && Physics2D.Queries - probesBefore <= 2, "Bound work to two candidates per frame");
}
void Fill() { for (int i = 0; i < 600; i++) Frame(); }
void Count(int count) { SessionSettings.FriendlyFireForDisplay.Cover = new(true, count); PvpCoverRuntime.Tick(); }
Fill(); Check(PvpCoverRuntime.ActiveCount == 12 && PvpCoverNative.Spawns == 12, "Initial cover quota");
int probes = Physics2D.Queries, updates = PathGrid.Current.Updates;
Fill(); Check(Physics2D.Queries == probes && PvpCoverNative.Spawns == 12, "No steady-state physics/spawn loop");
Check(updates == 1, "Batch native pathfinding update");
long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 1000; i++) { Time.frameCount++; PvpCoverRuntime.Tick(); }
long allocation = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
Check(allocation == 0, "Stable cover lifecycle has no managed allocations");
Console.WriteLine($"Cover lifecycle: {allocation} bytes/1000 stable ticks (native APIs stubbed).");
Count(4); Check(PvpCoverRuntime.ActiveCount == 4 && first.floorRelatedNetworkObjects.Count == 4, "Immediate count reduction");
Count(20); Fill(); Check(PvpCoverRuntime.ActiveCount == 20, "Live count increase");
PathGrid.Current.IsBuilt = false; updates = PathGrid.Current.Updates;
SessionSettings.FriendlyFireForDisplay.Enabled = false; PvpCoverRuntime.Tick();
Check(PvpCoverRuntime.ActiveCount == 0 && first.floorRelatedNetworkObjects.Count == 0 && PvpCoverNative.Owned.Count == 0, "Manual PvP end removes owned roots");
Check(PathGrid.Current.Updates == updates + 1, "Cleanup queues native graph refresh while scan is in progress");
PathGrid.Current.IsBuilt = true;
foreach (long mode in new long[] { 1, 2, 3, 4 })
{
    CombatMatchRuntime.IsRunning = true; CombatMatchRuntime.MatchToken = mode; Fill();
    Check(PvpCoverRuntime.ActiveCount == 20, "Shared mode countdown coverage");
    CombatMatchRuntime.IsRunning = false; CombatMatchRuntime.MatchToken = 0; PvpCoverRuntime.Tick();
    Check(PvpCoverRuntime.ActiveCount == 0, "Stop/cancel cleanup for every shared match");
}
SessionSettings.FriendlyFireForDisplay.Enabled = true; Fill();
var second = Floor("B"); host.PlayerAvatar.currentFloorGuid = "B"; Frame();
Check(first.floorRelatedNetworkObjects.Count == 0 && PvpCoverRuntime.ActiveCount <= 2, "Floor transition removes old cover before new placement");
Fill(); Check(second.floorRelatedNetworkObjects.Count == 20, "New floor gets its own quota");
host.PlayerAvatar.loadingScreenType = 0; Frame(); Check(PvpCoverRuntime.ActiveCount == 0, "Loading transition clears old scope");
host.PlayerAvatar.loadingScreenType = -1; Fill();
SaveManager.CurrentRun = new(); SessionSettings.ResourceGeneration++; Frame();
Check(PvpCoverRuntime.ActiveCount <= 2, "Fresh run resets cover once"); Fill();
var existing = second.floorRelatedNetworkObjects[0]; existing.Destroyed = true; Frame();
Check(!second.floorRelatedNetworkObjects.Contains(existing), "Native destruction removes bookkeeping");
int spawns = PvpCoverNative.Spawns; Fill(); Check(PvpCoverNative.Spawns == spawns, "Do not replenish externally destroyed objects endlessly");
SessionSettings.FriendlyFireForDisplay.Cover = default; Frame(); Check(PvpCoverNative.Owned.Count == 0, "Option off cleanup");
PvpCoverNative.FailPrefab = true; Count(12); Fill();
int calls = PvpCoverNative.PrefabCalls; Fill(); Check(PvpCoverNative.PrefabCalls == calls, "Asset failure latches instead of retrying every frame");
PvpCoverNative.FailPrefab = false; SessionSettings.FriendlyFireForDisplay.Cover = default; Frame(); Count(12); Fill();
Check(PvpCoverRuntime.ActiveCount == 12, "Explicit off/on retries recovered feature");
PvpCoverRuntime.Clear(); PvpCoverNative.FailSpawn = PvpCoverNative.FailRemove = true; Frame();
Check(PvpCoverRuntime.ActiveCount == 1, "Failed partial spawn retains cleanup ownership");
PvpCoverNative.FailRemove = false; Frame();
Check(PvpCoverRuntime.ActiveCount == 0, "Failed-placement latch still retries cleanup");
PvpCoverNative.FailSpawn = false;
PvpCoverRuntime.Clear(); PvpCoverNative.FailSpawn = true; Frame();
Check(PvpCoverRuntime.ActiveCount == 0 && PvpCoverNative.Owned.Count == 0 && second.floorRelatedNetworkObjects.Count == 0, "Partially published spawn is cleaned");
spawns = PvpCoverNative.Spawns; Fill(); Check(PvpCoverNative.Spawns == spawns, "Spawn failure does not loop");
PvpCoverNative.FailSpawn = false; PvpCoverRuntime.Clear(); Fill();
PvpCoverNative.FailRemove = true; SessionSettings.FriendlyFireForDisplay.Cover = default; Frame();
Check(PvpCoverRuntime.ActiveCount == 12, "Failed removal stays owned");
PvpCoverNative.FailRemove = false; Frame(); Check(PvpCoverRuntime.ActiveCount == 0 && PvpCoverNative.Owned.Count == 0, "Cleanup retry releases all roots");
Count(12); Fill(); Mirror.NetworkServer.active = false; Frame(); Check(PvpCoverRuntime.ActiveCount == 0, "Session shutdown cleanup");
Mirror.NetworkServer.active = true; Physics2D.BlockAll = true; Count(32); Fill();
Check(PvpCoverRuntime.ActiveCount == 0, "Unsafe level stays empty"); probes = Physics2D.Queries; Fill();
Check(Physics2D.Queries == probes, "Exhausted placement stops scanning");
Physics2D.BlockAll = false;
var lobby = new FullyDesignedFloorGenerator { guid = "Lobby" };
FloorGenerator.Floors[lobby.guid] = lobby;
DungeonManager.Instance.generatedFloors[lobby.guid] = lobby.DataOnServer;
host.PlayerAvatar.currentFloorGuid = lobby.guid;
Count(12); Fill();
Check(PvpCoverRuntime.ActiveCount == 12, "FullyDesigned multiplayer lobby receives configured cover");
Check(PvpCoverRuntime.Describe().Contains("12/12"), "Actual placement count is visible");
int reports = Debug.Logs.Count; Fill();
Check(Debug.Logs.Count == reports, "Completed placement logs once, not every frame");
SessionSettings.FriendlyFireForDisplay.Enabled = false; Frame();
Check(lobby.floorRelatedNetworkObjects.Count == 0 && PvpCoverRuntime.Describe().Contains("waiting for PvP"), "Authored lobby cleanup and waiting status");
SessionSettings.FriendlyFireForDisplay.Enabled = true;
lobby.transform.Paths.Clear(); Frame();
Check(PvpCoverRuntime.Describe().Contains("ground") && Debug.Logs.Last().Contains("reason="), "Unsupported authored map reports why no cover spawned");
RoomAdapterTests.Run(Check);
PvpCoverFeature.Available = false; Frame();
Check(PvpCoverRuntime.Describe().Contains("unavailable"), "Compatibility failure is not reported as a loading delay");
PvpCoverFeature.Available = true;
Console.WriteLine($"Passed {checks} cover lifecycle checks.");
