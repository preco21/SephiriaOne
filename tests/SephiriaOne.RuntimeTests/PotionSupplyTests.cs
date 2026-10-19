using SephiriaOne;

internal static class PotionSupplyTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        PlayerAvatar[] New(int count = 4)
        {
            UnityEngine.Time.unscaledTime = 0;
            var all = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint id = 2; id <= count; id++) all.Add(add(id).PlayerAvatar);
            SessionSettings.EnsureResourceScope(); LargePotionGrant.Load();
            return all.ToArray();
        }
        void Command(string args) => check(SettingsActions.Execute("/one friendlyfire " + args).Success, "Potion command: " + args);
        void Consume(PlayerAvatar player) { player.Inventory.inventoryMatrix.Clear(); player.Inventory.temporaryInventory.Clear(); player.Inventory.subBagMatrix.Clear(); }
        bool Has(PlayerAvatar player) => LargePotionGrant.Owns(player.Inventory);

        var players = New(9);
        check(!SessionSettings.FriendlyFireSupplies.AutoPotion, "Automatic potion default off");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[0]), true);
        players[1].Inventory.inventoryMatrix[new ItemPosition { y = 100 }] = new() { EntityID = 1, Quantity = 3 };
        players[2].Inventory.inventoryMatrix[new ItemPosition { y = 2 }] = new() { EntityID = 1, Quantity = 1 };
        players[3].Inventory.subBagMatrix[0] = new(3, 1, 2);
        players[4].Inventory.temporaryInventory.Add(new(4, 1, 1));
        players[5].Inventory.PotionFull = true;
        players[6].Inventory.inventoryMatrix[new ItemPosition { y = 100 }] = new() { EntityID = 37, Quantity = 3 };
        players[7].Inventory.inventoryMatrix[new ItemPosition { y = 100 }] = new() { EntityID = 1, Quantity = 0 };
        Command("potion");
        check(players.All(Has), "Manual grant covers all nine players independent of selection/master");
        check(players.Skip(1).Take(4).All(p => p.Inventory.PotionWrites == 0), "All native storage locations prevent duplicates");
        check(players[5].Inventory.temporaryInventory.Count == 1 && players[5].Inventory.PotionWrites == 1, "Full inventory gets one native overflow item");
        Command("potion");
        check(players.Sum(p => p.Inventory.PotionWrites) == 5, "Repeating manual action does not add copies");
        Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one friendlyfire potion").Success, "Guests cannot run manual grant");
        Mirror.NetworkServer.active = true;

        players = New();
        Command("on"); PvpPotionSupplies.Tick(); check(players.All(p => !Has(p)), "Off option does not grant on FF entry");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[0]), true);
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[1]), true);
        Command("auto-potion on"); PvpPotionSupplies.Tick();
        check(Has(players[0]) && Has(players[1]) && !Has(players[2]) && !Has(players[3]), "Auto grants only selected players");
        Consume(players[0]);
        players[0].Inventory.canBroadcast = 0; PvpPotionSupplies.Tick();
        players[0].Inventory.canBroadcast = 1; PvpPotionSupplies.Tick();
        check(!Has(players[0]), "Transient inventory readiness cannot refill a consumed supply");
        Command("damage 0.1"); Command("balance on"); Command("resetkda");
        for (int i = 0; i < 100; i++) PvpPotionSupplies.Tick();
        check(!Has(players[0]) && players[0].Inventory.PotionWrites == 1, "Consumption, KDA reset, tuning and idle frames do not refill");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[2]), true); PvpPotionSupplies.Tick();
        check(Has(players[2]) && !Has(players[0]), "New participant gets supply without refilling existing members");
        Command("off"); Command("on"); PvpPotionSupplies.Tick();
        check(Has(players[0]) && players[0].Inventory.PotionWrites == 2, "Off/on within one frame starts new supply eligibility");
        var preset = new SessionPolicy(); preset.Record(new FriendlyFireCommand(false, autoPotion: true));
        string text = preset.ToPresetText();
        check(text.StartsWith("SephiriaOne preset v36") && SessionPolicy.TryReadPreset(text, out var loaded, out _) && loaded.FriendlyFire.AutoPotion,
            "Automatic supply persists in versioned preset");
        check(!SessionPolicy.TryReadPreset(text.Replace("v36", "v35"), out _, out _), "Old preset cannot claim new setting");
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v35\nfriendlyfire enabled 1\n", out loaded, out _) && !loaded.FriendlyFire.AutoPotion,
            "Old presets default automatic supply off");
        check(default(FriendlyFireSettings).Apply(new FriendlyFireCommand(false, autoPotion: true)).WithEnabled(true).AutoPotion,
            "Match overlays preserve supply setting");
        Command("auto-potion reset"); check(!SessionSettings.FriendlyFireSupplies.AutoPotion && SessionSettings.FriendlyFireSupplies.Enabled, "Supply reset leaves master alone");

        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            players = New();
            Command("auto-potion on");
            check(SettingsActions.Execute("/one " + mode + " start").Success, mode + " starts");
            PvpPotionSupplies.Tick(); check(players.All(Has), mode + ": grants during countdown, including waiting Tournament players");
            foreach (var player in players) Consume(player);
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick(); PvpPotionSupplies.Tick();
            check(players.All(p => !Has(p)), mode + ": combat activation does not refill countdown supplies");
            check(SettingsActions.Execute("/one " + mode + " stop").Success, mode + " stops"); PvpPotionSupplies.Tick();
            check(players.All(p => !Has(p)), mode + ": cleanup does not grant");
            check(SettingsActions.Execute("/one " + mode + " start").Success, mode + " restarts"); PvpPotionSupplies.Tick();
            check(players.All(Has), mode + ": next match gets fresh check");
        }

        players = New(); Command("auto-potion on"); Command("on");
        players[1].Inventory.canBroadcast = 0; PvpPotionSupplies.Tick();
        check(!Has(players[1]) && Has(players[0]), "Loading player does not block ready players");
        players[1].Inventory.canBroadcast = 1; PvpPotionSupplies.Tick(); check(Has(players[1]), "Readiness retries grant");
        var old = players[1].spawner; PlayerSpawner.MultiplayerList.Remove(old); Mirror.NetworkServer.connections.Remove(old.connectionToClient.connectionId);
        PvpPotionSupplies.Tick(); var rejoined = add(2).PlayerAvatar; PvpPotionSupplies.Tick();
        check(Has(rejoined), "Replacement connection gets fresh supply check");

        players = New(2); var inv = players[0].Inventory;
        using (new GridInventory.Permission(inv))
        {
            Command("potion"); check(inv.HasPermission, "Borrowed inventory permission is left open");
        }
        check(!inv.HasPermission, "Permission owner closes transaction");
        Consume(players[0]);
        inv.PermissionOpened = () => inv.temporaryInventory.Add(new(91, 1, 1));
        int priorWrites = inv.PotionWrites; Command("potion");
        check(inv.PotionWrites == priorWrites && inv.temporaryInventory.Count == 1, "Native permission callbacks are followed by a duplicate recheck");
        inv.PermissionOpened = null;
        Consume(players[0]); inv.AfterPotionWrite = () => throw new Exception("post-write native failure");
        check(!SettingsActions.Execute("/one friendlyfire potion").Success && Has(players[0]) && inv.temporaryInventory.Count == 0,
            "Partial write exception never adds overflow");
        inv.AfterPotionWrite = null; int writes = inv.PotionWrites; Command("potion");
        check(inv.PotionWrites == writes, "Manual retry after partial write rechecks ownership");
        Consume(players[0]); inv.AfterPotionWrite = () => { LargePotionGrant.GiveAll(out _); PvpPotionSupplies.Tick(); };
        Command("auto-potion on"); Command("on"); PvpPotionSupplies.Tick();
        check(inv.PotionWrites == writes + 1, "Reentrant native callbacks cannot duplicate supplies");
        inv.AfterPotionWrite = null;
        players = New(2); inv = players[0].Inventory;
        inv.PermissionOpened = () => Mirror.NetworkServer.connections.Remove(players[0].spawner.connectionToClient.connectionId);
        check(!SettingsActions.Execute("/one friendlyfire potion").Success && inv.PotionWrites == 0 && !Has(players[0]) && Has(players[1]),
            "Native permission callback disconnect prevents stale write without blocking other targets");
        players = New(); Command("auto-potion on"); Command("on");
        players[0].Inventory.BeforePotionWrite = () => throw new Exception("native failure");
        PvpPotionSupplies.Tick(); int faults = players[0].Inventory.PotionWrites;
        players[0].Inventory.BeforePotionWrite = null; PvpPotionSupplies.Tick();
        check(!Has(players[0]) && players.Skip(1).All(Has) && players[0].Inventory.PotionWrites == faults, "Fault is isolated and not automatically retried");
        players[0].Inventory.canBroadcast = 0; PvpPotionSupplies.Tick();
        players[0].Inventory.canBroadcast = 1; PvpPotionSupplies.Tick();
        check(!Has(players[0]), "Readiness bounce cannot retry an ambiguous grant failure");

        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            players = New(); Command("auto-potion on"); Command("on"); PvpPotionSupplies.Tick();
            check(SettingsActions.Execute("/one " + mode + " start").Success, mode + ": starts from casual FF");
            PvpPotionSupplies.Tick(); foreach (var player in players) Consume(player);
            check(SettingsActions.Execute("/one " + mode + " stop").Success, mode + ": stops after casual FF"); PvpPotionSupplies.Tick();
            check(players.All(p => !Has(p)) && !SessionSettings.FriendlyFireSupplies.Enabled, mode + ": stop disables master without refilling");
        }

        players = New(9); Command("auto-potion on"); Command("on");
        for (int i = 0; i < 100; i++) PvpPotionSupplies.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) PvpPotionSupplies.Tick();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        check(allocated == 0 && players.All(p => p.Inventory.PotionWrites == 1), "Unchanged nine-player supplies allocate nothing and do not write");
        Console.WriteLine($"PvP supplies: {allocated} bytes/1000 nine-player ticks");
    }
}
