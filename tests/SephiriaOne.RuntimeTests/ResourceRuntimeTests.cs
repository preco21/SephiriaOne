using SephiriaOne;

internal static class ResourceRuntimeTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        bool Command(string text) => SettingsActions.Execute("/resources " + text).Success;
        int Owned(PlayerAvatar player, ResourceKind kind) => player.customStats.GetValueOrDefault(ResourceCatalog.Get(kind).Marker);
        var host = start();
        var guest = add(2, 7, 2, 0);
        guest.PlayerAvatar.maxPassivePoint = 8;
        check(Command("talents +5") && host.PlayerAvatar.maxPassivePoint == 10 && guest.PlayerAvatar.maxPassivePoint == 13,
            "Resource offset uses each native talent budget");
        check(Command("talents +2") && host.PlayerAvatar.maxPassivePoint == 12 && guest.PlayerAvatar.maxPassivePoint == 15,
            "Resource offsets accumulate across commands");
        check(Command("talents x3") && host.PlayerAvatar.maxPassivePoint == 15 && guest.PlayerAvatar.maxPassivePoint == 24,
            "Resource multiplier replaces offset and excludes its own contribution");
        check(Command("talents x3") && guest.PlayerAvatar.maxPassivePoint == 24, "Repeated resource factor does not compound");
        host.PlayerAvatar.maxPassivePoint += 2;
        SessionSettings.Synchronize();
        check(host.PlayerAvatar.maxPassivePoint == 21 && Owned(host.PlayerAvatar, ResourceKind.Talents) == 14,
            "Native hard-mode/reward talent gains update active resource multipliers");
        check(Command("talents set 30"), "Absolute talent command");
        host.PlayerAvatar.maxPassivePoint += 4;
        SessionSettings.Synchronize();
        check(host.PlayerAvatar.maxPassivePoint == 34, "Absolute talent setting preserves subsequent native gains");
        ResourceRuntime.ApplyEarly(host.PlayerAvatar, ResourceKind.Talents, 34);
        check(host.PlayerAvatar.maxPassivePoint == 34, "Early talent consumer preserves native gains after Set");
        var late = add(3, 5, 4, 0);
        late.PlayerAvatar.maxPassivePoint = 9;
        SessionSettings.Synchronize();
        check(late.PlayerAvatar.maxPassivePoint == 30, "Late join gets active absolute talent target");
        check(Command("talents +1") && host.PlayerAvatar.maxPassivePoint == 12 && guest.PlayerAvatar.maxPassivePoint == 9,
            "Delta after Set starts a new native-relative offset, preserving native gains");
        guest.PlayerAvatar.passiveStats[1] = 9;
        check(!Command("talents reset") && host.PlayerAvatar.maxPassivePoint == 12 && guest.PlayerAvatar.maxPassivePoint == 9,
            "One allocated guest rejects all-player budget reset before writes");
        guest.PlayerAvatar.passiveStats.Clear();
        check(Command("talents x1") && host.PlayerAvatar.maxPassivePoint == 11 && guest.PlayerAvatar.maxPassivePoint == 8,
            "Identity factor restores native talent budgets including later gains");

        host = start(); guest = add(2, 7, 2, 0);
        host.PlayerAvatar.customStats["FRUITCOUNT"] = 2;
        guest.PlayerAvatar.customStatsAmp["FRUITCOUNT"] = 100;
        check(Command("fruit x2") && host.PlayerAvatar.GetCustomStatUnsafe("FRUITCOUNT") + 6 == 16 &&
            guest.PlayerAvatar.GetCustomStatUnsafe("FRUITCOUNT") + 6 == 12, "Fruit factors include each native default and stat multiplier");
        host.PlayerAvatar.customStatsAmp["FRUITCOUNT"] = 100;
        SessionSettings.Synchronize();
        check(host.PlayerAvatar.GetCustomStatUnsafe("FRUITCOUNT") + 6 == 20, "Fruit multiplier follows native equipment/buff amplifiers");
        guest.PlayerAvatar.localDataStorage.preparingUIThings = true;
        check(!Command("fruit reset") && host.PlayerAvatar.GetCustomStatUnsafe("FRUITCOUNT") + 6 == 20,
            "Remote unfinished fruit menu blocks reductions without partially changing host");
        guest.PlayerAvatar.localDataStorage.preparingUIThings = false;
        host.PlayerAvatar.localDataStorage.preparingUIThings = true;
        check(Command("fruit reset"), "Host addon panel does not block its own reset using generic native UI flag");
        check(Command("fruit set 0"), "Zero fruit budget allowed with no committed selections");
        guest.PlayerAvatar.localDataStorage.fruitSkewerBonus.Add(new GridInventory.ItemDropBonusData { categoryName = "attack", weight = 1 });
        bool stopped = false;
        try { ResourceRuntime.ApplyEarly(guest.PlayerAvatar, ResourceKind.Fruit, 1); } catch (InvalidOperationException) { stopped = true; }
        check(stopped && guest.PlayerAvatar.localDataStorage.fruitSkewerBonus.Count == 1,
            "Early zero-budget consumer refuses to consume or rewrite native selections");

        host = start(); check(Command("fruit +4"), "Zero-multiplier fruit reset prepared");
        host.PlayerAvatar.customStatsAmp["FRUITCOUNT"] = -100;
        check(Command("fruit reset") && Owned(host.PlayerAvatar, ResourceKind.Fruit) == 0,
            "Native zero multiplier does not trap the resource reset");

        host = start(); guest = add(2, 7, 2, 0);
        check(Command("slots +6") && host.PlayerAvatar.Inventory.CurrentInventoryStorage == 30, "Slot command applies native capacity");
        guest.PlayerAvatar.Inventory.CurrentInventoryStorage += 6;
        check(Command("slots x2") && host.PlayerAvatar.Inventory.CurrentInventoryStorage == 48 &&
            guest.PlayerAvatar.Inventory.CurrentInventoryStorage == 60, "Slot multiplier preserves character-specific native capacity");
        SessionSettings.Synchronize();
        check(host.PlayerAvatar.Inventory.CurrentInventoryStorage == 48, "Slot maintenance does not stack");
        check(Command("slots reset") && host.PlayerAvatar.Inventory.CurrentInventoryStorage == 24 &&
            guest.PlayerAvatar.Inventory.CurrentInventoryStorage == 30, "Slot reset removes only owned capacity");

        host = start(); guest = add(2, 7, 2, 0);
        host.PlayerAvatar.currentMoney = 123; host.PlayerAvatar.rerollDice = 1;
        guest.PlayerAvatar.StartingLeaves = 200; guest.PlayerAvatar.maxRerollDice = 5;
        check(Command("leaves x3") && Command("dice +10"), "Future grant policies accepted");
        check(host.PlayerAvatar.currentMoney == 123 && host.PlayerAvatar.rerollDice == 1 && host.PlayerAvatar.maxRerollDice == 3,
            "Starting commands never refill live balances or change live dice maximum");
        var snapshot = SessionSettings.ReadSnapshot();
        check(snapshot.Players[0].Resources["leaves"].Contains("next fresh start 300") && snapshot.Players[1].Resources["leaves"].Contains("next fresh start 600"),
            "Read-only status shows separate character-specific next grants");
        check(Command("dice reset") && host.PlayerAvatar.rerollDice == 1, "Future dice reset does not alter current spendable dice");
        check(SettingsActions.Execute("/one save").Success, "Resource intent explicitly saves");
        check(SessionSettings.ReadSnapshot(true).SavedSettings.Any(x => x == "resources leaves multiplier 3"), "Saved resource policy visible in shared snapshot");
        var before = SessionSettings.ReadSnapshot().ActiveSettings.ToArray();
        guest.PlayerAvatar.StartingLeaves = 200001;
        check(!Command("leaves x10000") && SessionSettings.ReadSnapshot().ActiveSettings.SequenceEqual(before),
            "Out-of-range newcomer-specific target rejects without replacing active policy");
        Mirror.NetworkServer.active = false;
        check(!Command("talents +2"), "Guest cannot change resource policies");

        host = start(); guest = add(2, 7, 2, 0);
        string marker = ResourceCatalog.Get(ResourceKind.Talents).Marker;
        guest.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == marker) throw new Exception("injected marker failure"); };
        check(!Command("talents +5") && SessionSettings.ReadSnapshot().FaultedFeature == "resources", "Partial budget failure enters shared fault journal");
        check(!SettingsActions.Execute("/stats luck +2").Success, "Other feature commands cannot cross unresolved resource fault");
        check(!Command("talents reset"), "Selective resource reset cannot recover an uncommitted family batch");
        guest.PlayerAvatar.customStats.BeforeWrite = null;
        check(Command("reset") && host.PlayerAvatar.maxPassivePoint == 5 && guest.PlayerAvatar.maxPassivePoint == 5,
            "Full resource reset recovers incomplete journal then removes only verified ownership");

        host = start();
        check(Command("talents x2"), "Early inheritance test policy accepted");
        late = add(2, 7, 2, 0); late.PlayerAvatar.maxPassivePoint = 7;
        late.connectionToClient.isReady = false; late.PlayerAvatar.Race = null; late.PlayerAvatar.Inventory.canBroadcast = 0;
        ResourceRuntime.ApplyEarly(late.PlayerAvatar, ResourceKind.Talents, 12);
        check(late.PlayerAvatar.maxPassivePoint == 14 && !HostStateAdapter.IsReady(late),
            "Validated early budget does not require or establish normal ready-player enrollment");
        late.connectionToClient.isReady = true; late.PlayerAvatar.Race = new UnityEngine.Object(); late.PlayerAvatar.Inventory.canBroadcast = 1;
        SessionSettings.Synchronize();
        check(late.PlayerAvatar.maxPassivePoint == 14, "Ordinary inheritance after early application does not duplicate resource contribution");

        host = start();
        check(SettingsActions.Execute("/stats luck x2").Success && Command("talents +5"), "Combined inheritance policy prepared");
        late = add(2, 6000, 4, 0);
        SessionSettings.Synchronize(); SessionSettings.Synchronize();
        check(late.PlayerAvatar.maxPassivePoint == 5 && Owned(late.PlayerAvatar, ResourceKind.Talents) == 0,
            "Rejected stat inheritance cannot partially enroll resource maintenance");
        check(Command("talents +1") && late.PlayerAvatar.maxPassivePoint == 11,
            "Explicit resource command can enroll its own kind after rejected unrelated inheritance");
        late.PlayerAvatar.maxPassivePoint += 2;
        SessionSettings.Synchronize();
        check(late.PlayerAvatar.maxPassivePoint == 13, "Explicit enrollment remains maintained independently of rejected unrelated policy");

        host = start();
        check(SettingsActions.Execute("/stats luck x2").Success && Command("talents +5"), "Faulted inheritance policy prepared");
        late = add(2, 7, 4, 0);
        // Use the catalog's actual marker so the failure occurs after the raw stat write.
        string luckMarker = StatCatalog.Find("luck").Marker;
        late.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == luckMarker) throw new Exception("injected inheritance marker fault"); };
        SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().FaultedFeature == "inheritance" && late.PlayerAvatar.maxPassivePoint == 5,
            "Faulted inherited stat batch blocks later resource maintenance in the same coordinator pass");
        late.PlayerAvatar.customStats.BeforeWrite = null;
        check(Command("reset") && late.PlayerAvatar.maxPassivePoint == 5, "Inherited journal recovers resources before explicit reset");

        foreach (string replacement in new[] { "netId", "storage", "run" })
        {
            host = start(); check(Command("talents +5"), "Early journal policy: " + replacement);
            late = add(2, 7, 4, 0); late.connectionToClient.isReady = false;
            late.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == marker) throw new Exception("early marker fault"); };
            try { ResourceRuntime.ApplyEarly(late.PlayerAvatar, ResourceKind.Talents); } catch (InvalidOperationException) { }
            check(SessionSettings.ReadSnapshot().FaultedFeature == "resources", "Early partial write retained: " + replacement);
            late.PlayerAvatar.customStats.BeforeWrite = null; late.connectionToClient.isReady = true;
            if (replacement == "netId") late.PlayerAvatar.netId = 99;
            else if (replacement == "storage") late.PlayerAvatar.localDataStorage = late.LocalDataStorage = new PlayerLocalDataStorage();
            else SaveManager.CurrentRun = new object();
            check(!Command("reset") && Owned(late.PlayerAvatar, ResourceKind.Talents) == 0,
                "Early recovery cannot replay into changed " + replacement);
        }

        host = start(); guest = add(2, 7, 4, 0);
        guest.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == marker) throw new Exception("command marker fault"); };
        check(!Command("talents +5"), "Command journal established before restart");
        guest.PlayerAvatar.customStats.BeforeWrite = null;
        HorayModAPI.StartSession();
        check(!Command("reset"), "Shared command journal cannot cross run generation");

        host = start(); guest = add(2, 7, 4, 0);
        check(Command("slots +6"), "Occupied slot reset scenario prepared");
        guest.PlayerAvatar.Inventory.inventoryMatrix[new ItemPosition { x = 5, y = 4 }] = new NewItemOwnInstance();
        check(!Command("slots reset") && host.PlayerAvatar.Inventory.CurrentInventoryStorage == 30 &&
            guest.PlayerAvatar.Inventory.inventoryMatrix.Count == 1, "Occupied trailing slot blocks whole-party reduction before native resize");
    }
}
