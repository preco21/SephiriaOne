using HarmonyLib;
using Mirror;
using SephiriaOne;

internal static class FriendlyFireParticipantTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        check(typeof(SessionSettings).Assembly.GetType("SephiriaOne.FriendlyFireParticipants") != null,
            "Transient participant lifetime registry is available");
        const string patchId = "SephiriaOne.Tests.Participants";
        var harmony = new Harmony(patchId);
        FriendlyFireParticipantHooks.Install(harmony);
        try
        {
            var host = start(); var guest = add(2); var other = add(3);
            FriendlyFireParticipants.Clear();
            var h = FriendlyFireParticipants.Capture(host.PlayerAvatar);
            var g = FriendlyFireParticipants.Capture(guest.PlayerAvatar);
            check(h != null && g != null && ReferenceEquals(g, FriendlyFireParticipants.Capture(guest.PlayerAvatar)),
                "Capture returns the cached exact current lifetime");
            // Cached admission must still observe native state changes even if
            // no lifecycle hook or synchronization frame has run in between.
            var guestAvatar = guest.PlayerAvatar;
            void RejectTransient(Action invalidate, Action restore, string label)
            {
                invalidate();
                check(FriendlyFireParticipants.Capture(guestAvatar) == null && !FriendlyFireParticipants.IsCurrent(g), label);
                restore();
                check(ReferenceEquals(g, FriendlyFireParticipants.Capture(guestAvatar)),
                    "Restored native state retains the same live receipt: " + label);
            }
            check(FriendlyFireParticipants.Capture(null) == null, "Null avatar cannot enter cached admission");
            RejectTransient(() => PlayerSpawner.MultiplayerList.Remove(guest), () => PlayerSpawner.MultiplayerList.Add(guest),
                "Cached player must remain in the native roster");
            RejectTransient(() => NetworkServer.spawned.Remove(guest.netId), () => NetworkServer.spawned.Add(guest.netId, guest.netIdentity),
                "Cached player must retain a spawned identity");
            RejectTransient(() => NetworkServer.spawned[guest.netId] = host.netIdentity, () => NetworkServer.spawned[guest.netId] = guest.netIdentity,
                "Cached player rejects replacement spawned identities");
            RejectTransient(() => guest.PlayerAvatar = other.PlayerAvatar, () => guest.PlayerAvatar = guestAvatar,
                "Cached avatar must still belong to its spawner");
            RejectTransient(() => guestAvatar.connectionToClient = host.connectionToClient, () => guestAvatar.connectionToClient = guest.connectionToClient,
                "Cached avatar and spawner must share the exact connection");
            RejectTransient(() => guestAvatar.netId = host.netId, () => guestAvatar.netId = guest.netId,
                "Cached avatar cannot change its network ID");
            RejectTransient(() => guestAvatar.isServer = false, () => guestAvatar.isServer = true,
                "Cached client-only avatar is ineligible");
            RejectTransient(() => guest.isServer = false, () => guest.isServer = true,
                "Cached client-only spawner is ineligible");
            check(FriendlyFireParticipants.Includes(host.PlayerAvatar) && FriendlyFireParticipants.Includes(guest.PlayerAvatar),
                "Empty selection includes all current eligible players");
            check(FriendlyFireParticipants.Set(h, true) && FriendlyFireParticipants.Set(g, true) &&
                !FriendlyFireParticipants.Includes(other.PlayerAvatar) && FriendlyFireParticipants.SelectedCount == 2,
                "Nonempty selection includes only selected current players");
            guest.connectionToClient.isReady = false;
            guest.PlayerAvatar.currentFloorGuid = "";
            guest.PlayerAvatar.Inventory.canBroadcast = 0;
            check(FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.IsSelected(guest.PlayerAvatar) &&
                FriendlyFireParticipants.SelectedCount == 2 && !FriendlyFireParticipants.Includes(guest.PlayerAvatar),
                "Temporary readiness loss denies interaction without erasing selection");
            guest.connectionToClient.isReady = true; guest.PlayerAvatar.currentFloorGuid = "town";
            guest.PlayerAvatar.Inventory.canBroadcast = 1;
            check(FriendlyFireParticipants.Includes(guest.PlayerAvatar), "Selection resumes after floor readiness returns");
            long epochForSelection = FriendlyFireParticipants.Epoch;
            FriendlyFireKda.For(host.PlayerAvatar).Kills = 7;
            check(FriendlyFireParticipants.SetAll(new[] { h, g, FriendlyFireParticipants.Capture(other.PlayerAvatar) }) &&
                FriendlyFireParticipants.SelectedCount == 3 && FriendlyFireParticipants.Epoch == epochForSelection + 1 &&
                FriendlyFireKda.For(host.PlayerAvatar).Kills == 0, "Select All selects the exact roster atomically and resets scores once");
            FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(other.PlayerAvatar), false);

            var connection = guest.connectionToClient;
            long epoch = FriendlyFireParticipants.Epoch;
            NetworkServer.RemoveConnection(connection.connectionId);
            NetworkServer.AddConnection(connection);
            check(!FriendlyFireParticipants.IsCurrent(g) && !FriendlyFireParticipants.Set(g, true) &&
                FriendlyFireParticipants.Capture(guest.PlayerAvatar) != null && !FriendlyFireParticipants.IsSelected(guest.PlayerAvatar) &&
                FriendlyFireParticipants.Epoch != epoch, "Same-frame connection removal/readdition invalidates old receipt and selection");
            epochForSelection = FriendlyFireParticipants.Epoch;
            FriendlyFireKda.For(host.PlayerAvatar).Kills = 4;
            check(!FriendlyFireParticipants.SetAll(new[] { h, g }) && FriendlyFireParticipants.SelectedCount == 1 &&
                FriendlyFireParticipants.IsSelected(host.PlayerAvatar) && FriendlyFireParticipants.Epoch == epochForSelection &&
                FriendlyFireKda.For(host.PlayerAvatar).Kills == 4, "Stale bulk selection leaves selection, score and epoch unchanged");
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar);
            FriendlyFireParticipants.Set(g, true);
            check(!NetworkServer.AddConnection(connection) && FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.IsSelected(guest.PlayerAvatar),
                "Rejected duplicate connection insertion does not erase the live selection");

            FriendlyFireParticipants.ResetSelection(); FriendlyFireParticipants.Set(g, true);
            var manager = new HorayNetworkManager();
            manager.Disconnecting = () => check(!FriendlyFireParticipants.Includes(guest.PlayerAvatar) &&
                FriendlyFireParticipants.SelectedCount == 0 && FriendlyFireParticipants.Includes(host.PlayerAvatar),
                "Last selection retirement activates remaining players without readmitting the still-mapped departing connection");
            manager.OnServerDisconnect(connection);
            check(!FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.Capture(guest.PlayerAvatar) == null,
                "Native disconnect retires immediately before dictionary removal or LateUpdate");
            NetworkServer.RemoveConnection(connection.connectionId); NetworkServer.AddConnection(connection);
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            guest.Initializing = () => check(!FriendlyFireParticipants.Includes(guest.PlayerAvatar) &&
                FriendlyFireParticipants.Capture(guest.PlayerAvatar) == null, "Native initialization callbacks cannot use the old lifetime");
            guest.RestoreInventory(); guest.Initializing = null;
            check(!FriendlyFireParticipants.IsCurrent(g) && !FriendlyFireParticipants.IsSelected(guest.PlayerAvatar) &&
                FriendlyFireParticipants.Includes(guest.PlayerAvatar), "Initialization completes with a fresh unselected eligible lifetime");
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            guest.Initializing = () => throw new InvalidOperationException("fixture initialization failure");
            try { guest.RestoreInventory(); } catch (InvalidOperationException) { }
            check(FriendlyFireParticipants.Capture(guest.PlayerAvatar) == null, "Failed initialization remains unavailable");
            guest.Initializing = null; guest.RestoreInventory();
            check(FriendlyFireParticipants.Capture(guest.PlayerAvatar) != null, "Successful later initialization starts a new lifetime");

            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            guest.Destroying = () => check(!FriendlyFireParticipants.Includes(guest.PlayerAvatar) &&
                FriendlyFireParticipants.Includes(host.PlayerAvatar), "Destroy callback retirement precedes native roster removal");
            guest.DestroyForTest(); PlayerSpawner.MultiplayerList.Add(guest); guest.OnStartServer();
            check(!FriendlyFireParticipants.IsCurrent(g) && !FriendlyFireParticipants.IsSelected(guest.PlayerAvatar) &&
                FriendlyFireParticipants.Capture(guest.PlayerAvatar) != null, "Same-frame same-object respawn cannot retain selection");

            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            SaveManager.CurrentRun = new SaveData();
            check(!FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.SelectedCount == 0,
                "New run save invalidates selection without synchronization");
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            DungeonManager.Instance = new DungeonManager();
            check(!FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.SelectedCount == 0,
                "New dungeon instance invalidates selection without synchronization");
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            manager.Stopping = () => check(!FriendlyFireParticipants.Includes(guest.PlayerAvatar) &&
                FriendlyFireParticipants.Capture(host.PlayerAvatar) == null, "Server stop closes permission scope while native server is still active");
            manager.OnStopServer();
            FriendlyFireParticipants.Clear();
            check(FriendlyFireParticipants.Capture(host.PlayerAvatar) == null, "Ordinary scope clear cannot reopen a retiring server");
            manager.OnStartServer();
            check(FriendlyFireParticipants.Capture(host.PlayerAvatar) != null && FriendlyFireParticipants.SelectedCount == 0,
                "New server start resumes with no selections");
            manager.OnStopClient(); manager.OnStartServer();

            SessionSettings.Synchronize();
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            FriendlyFireKda.For(host.PlayerAvatar).Kills = 6;
            HorayModAPI.StartSession();
            check(!FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.SelectedCount == 0 &&
                FriendlyFireKda.For(host.PlayerAvatar).Kills == 6, "Native run-start clears selection immediately while preserving score totals");
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "Fixture write fault");
            NetworkServer.RemoveConnection(connection.connectionId); NetworkServer.AddConnection(connection);
            check(!FriendlyFireParticipants.IsCurrent(g) && FriendlyFireParticipants.SelectedCount == 0,
                "Faulted stat synchronization cannot preserve a departed selection");

            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar); FriendlyFireParticipants.Set(g, true);
            NetworkServer.connections[connection.connectionId] = new NetworkConnectionToClient { connectionId = connection.connectionId,
                identity = connection.identity };
            check(!FriendlyFireParticipants.IsCurrent(g) && !FriendlyFireParticipants.Includes(guest.PlayerAvatar) &&
                FriendlyFireParticipants.SelectedCount == 0, "Reused connection ID cannot admit the departed exact connection");
            NetworkServer.connections[connection.connectionId] = connection;
            guest.connectionToClient.identity = host.netIdentity;
            check(FriendlyFireParticipants.Capture(guest.PlayerAvatar) == null, "Connection player identity must belong to this avatar");
            connection.identity = guest.netIdentity;
            g = FriendlyFireParticipants.Capture(guest.PlayerAvatar);
            FriendlyFireParticipants.Set(g, true);
            for (int i = 0; i < 100; i++) FriendlyFireParticipants.Capture(guest.PlayerAvatar);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                FriendlyFireParticipants.Capture(guest.PlayerAvatar);
                FriendlyFireParticipants.Includes(guest.PlayerAvatar);
                FriendlyFireParticipants.IsCurrent(g);
            }
            check(GC.GetAllocatedBytesForCurrentThread() == before, "Stable participant reads allocate no memory after first capture");
        }
        finally { harmony.UnpatchAll(patchId); FriendlyFireParticipants.Clear(); }
    }
}
