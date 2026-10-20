using SephiriaOne;

internal static class SlateSelectionTests
{
    internal static void Run(Action reset, Func<string, PlayerAvatar> create, Action<bool, string> check)
    {
        void Select(SlateKind kind, bool automatic = true, int count = 2)
        { SessionSettings.Requital = new(automatic, count, kind); RequitalRuntime.Tick(); }
        reset(); var a = create(null); var b = create(null);
        Select(SlateKind.Heaven);
        check(new[] { a, b }.All(p => p.Inventory.Held.Count == 2 &&
            p.Inventory.Held.Values.All(x => x.entityID == 2099)), "Heaven automatic copies reach all ready players");
        var oldIds = a.Inventory.startingItems.Select(x => x.instanceID).ToArray();
        check(RequitalRuntime.GiveAll(out _), "Heaven manual grant succeeds");
        Select(SlateKind.Requital);
        check(a.Inventory.Held.Values.Count(x => x.entityID == 2099) == 2 &&
            a.Inventory.Held.Values.Count(x => x.entityID == 2100) == 2 &&
            oldIds.All(id => !a.Inventory.Held.ContainsKey(id)),
            "Type switch replaces automatic IDs and preserves ordinary manual Heaven gifts");
        Select(SlateKind.Heaven, false);
        check(a.Inventory.Held.Count == 2 && a.Inventory.startingItems.Count == 0, "Disable retracts selected loadout only");

        reset(); a = create(null);
        Select(SlateKind.Requital);
        HorayNetworkAuthenticator.AccessDeny_InDungeon = true;
        Select(SlateKind.Heaven);
        check(a.Inventory.Held.Count == 0 && a.Inventory.startingItems.All(x => x.entityID == 2099),
            "In-run type change retracts old copies and registers replacements for next fresh run");
        DungeonManager.Instance.globalItemStatTable.Clear();
        a.Inventory.RestockStartingItem(0);
        check(a.Inventory.Held.Count == 2 && a.Inventory.Held.Values.All(x => x.entityID == 2099),
            "Weak ownership retains Heaven identity after native metadata clearing");
        var restored = a.Inventory.Held.Values.ToArray();
        PlayerSpawner.MultiplayerList.Remove(a.spawner);
        b = create(a.spawner.playerGuid);
        foreach (var item in restored) b.Inventory.Held[item.instanceID] = item;
        RequitalRuntime.Clear(); RequitalRuntime.Tick();
        check(b.Inventory.Grants == 0 && b.Inventory.startingItems.Count == 2 &&
            b.Inventory.startingItems.All(x => x.entityID == 2099), "Saved Heaven provenance rebuilds the correct type without regrant");
        Select(SlateKind.Requital);
        check(b.Inventory.Held.Count == 0 && b.Inventory.startingItems.All(x => x.entityID == 2100),
            "Restored Heaven IDs retract on selection change");

        reset(); a = create(null); a.Inventory.Full = true;
        Select(SlateKind.Heaven);
        var queued = a.Inventory.Pending.Select(x => x.instanceID).ToArray();
        Select(SlateKind.Requital);
        check(a.Inventory.Pending.Count == 2 && a.Inventory.Pending.All(x => x.entityID == 2100 && !queued.Contains(x.instanceID)),
            "Type switch replaces pending automatic copies with fresh IDs of the selected type");
        Select(SlateKind.Heaven);
        a.Inventory.ClearCarried(); DungeonManager.Instance.globalItemStatTable.Clear();
        a.Inventory.RestockStartingItem(0);
        check(a.Inventory.Pending.Count == 2 && a.Inventory.Pending.All(x => x.entityID == 2099),
            "Heaven restock keeps exactly one pending copy per ID");
        a.Inventory.Full = false; a.Inventory.RetryPending();
        a.Inventory.RestockStartingItem(0);
        check(a.Inventory.Grants == 2 && a.Inventory.Held.Values.All(x => x.entityID == 2099),
            "Heaven retry before restock cannot duplicate deliveries");

        reset(); a = create(null); b = create(null);
        Select(SlateKind.Heaven, count: 1);
        var transferred = a.Inventory.Held.Values.Single();
        b.Inventory.Engraved[transferred.instanceID] = transferred;
        a.Inventory.Held.Remove(transferred.instanceID);
        PlayerSpawner.MultiplayerList.Remove(a.spawner);
        Select(SlateKind.Requital, count: 1);
        check(!b.Inventory.Engraved.ContainsKey(transferred.instanceID),
            "Type switch retracts a transferred Heaven even with its original owner offline");
        var rejoin = create(a.spawner.playerGuid);
        rejoin.Inventory.Held[transferred.instanceID] = transferred;
        RequitalRuntime.Tick();
        check(!rejoin.Inventory.Held.ContainsKey(transferred.instanceID) &&
            rejoin.Inventory.Held.Values.All(x => x.entityID == 2100),
            "Saved retired type cannot reappear on reconnect");

        reset(); a = create(null);
        Select(SlateKind.Heaven, false);
        ItemDatabase.Items[2100].rarity = EItemRarity.Common; RequitalRuntime.Load();
        check(RequitalRuntime.Available && RequitalRuntime.GiveAll(out _) &&
            a.Inventory.Held.Values.All(x => x.entityID == 2099),
            "Invalid Requital catalog entry cannot disable valid Heaven");
        ItemDatabase.Items[2099].activeType = EItemActiveType.WitchHat; RequitalRuntime.Load();
        check(!RequitalRuntime.Available && !RequitalRuntime.GiveAll(out _), "Heaven validates its own Hidden active type");
        ItemDatabase.Items[2100].rarity = EItemRarity.Eternal; RequitalRuntime.Load();
        Select(SlateKind.Requital, false);
        check(RequitalRuntime.Available && RequitalRuntime.GiveAll(out _), "Invalid Heaven cannot disable valid Requital");

        reset(); a = create(null); Select(SlateKind.Requital);
        DungeonManager.Instance.globalItemStatTable.Clear(); // Fresh run has kept weak ownership, but erased markers.
        HorayNetworkAuthenticator.AccessDeny_InDungeon = true;
        Select(SlateKind.Heaven);
        check(a.Inventory.Held.Count == 0 && a.Inventory.startingItems.Count == 2 &&
            a.Inventory.startingItems.All(x => x.entityID == 2099),
            "Type replacement still retracts old native registrations after global provenance is cleared");

        reset(); a = create(null);
        Select(SlateKind.Heaven, false, 3);
        a.Inventory.OnWrite = () => SessionSettings.Requital = new(false, 3, SlateKind.Requital);
        check(!RequitalRuntime.GiveAll(out _) && a.Inventory.Grants == 1 &&
            a.Inventory.Held.Values.Single().entityID == 2099, "Manual batch stops if selection changes during a native callback");
        a.Inventory.OnWrite = null;

        reset(); a = create(null);
        a.Inventory.OnWrite = () => SessionSettings.Requital = new(true, 3, SlateKind.Requital);
        Select(SlateKind.Heaven, count: 3);
        check(a.Inventory.Grants == 1, "Automatic type change during callback cannot create a mixed batch");
        a.Inventory.OnWrite = null; RequitalRuntime.Tick();
        check(a.Inventory.Held.Count == 0 && a.Inventory.Grants == 1,
            "Faulted old type is retractable without silently replaying a new addition");
        Select(SlateKind.Requital, false); Select(SlateKind.Requital);
        check(a.Inventory.Held.Count == 2 && a.Inventory.Held.Values.All(x => x.entityID == 2100),
            "Verified off cleanup admits an explicit fresh selection after a fault");
        RequitalRuntime.Shutdown();

        reset();
        for (int i = 0; i < 9; i++) create(null);
        Select(SlateKind.Heaven);
        for (int i = 0; i < 10000; i++) RequitalRuntime.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) RequitalRuntime.Tick();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated &&
            PlayerSpawner.MultiplayerList.Sum(p => p.PlayerAvatar.Inventory.Grants) == 18,
            "Stable nine-player Heaven loadouts allocate zero and never refill");
        RequitalRuntime.Shutdown();
    }
}
