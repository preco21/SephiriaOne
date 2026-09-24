using Mirror;
using SephiriaOne;

internal static class MultiplierRuntimeTests
{
    public static void Run(Action<bool, string> check, Func<PlayerSpawner> start,
        Func<uint, int, int, int, PlayerSpawner> add)
    {
        bool Do(string command) => SettingsActions.Execute(command).Success;
        int Luck(PlayerSpawner p) => p.PlayerAvatar.GetCustomStatUnsafe("LUCK");
        int Points(PlayerSpawner p) => p.PlayerAvatar.Inventory.dimensionPocket;
        int Limit() => DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey];
        var host = start();
        var guest = add(2, 10, 8, 0);
        check(Do("/stats luck x3") && Do("/fountain x3") && Luck(host) == 15 && Luck(guest) == 30 &&
            Points(host) == 12 && Points(guest) == 24, "Shared commands multiply each current player independently");
        check(Do("/stats luck x3") && Do("/fountain set x3") && Luck(host) == 15 && Points(guest) == 24,
            "Panel-compatible Set syntax and repeated commands do not compound");
        host.PlayerAvatar.customStats["LUCK"] += 2;
        host.PlayerAvatar.Inventory.dimensionPocket += 2;
        SessionSettings.BeforeNativeRead("Multiplier boundary test");
        check(Luck(host) == 21 && Points(host) == 18, "Native baseline changes are multiplied before a native read");
        check(Do("/fountain +2") && Points(host) == 8 && Points(guest) == 10,
            "Fountain delta after multiplier starts a native-relative offset for everyone");
        check(Do("/fountain +3") && Points(host) == 11 && Points(guest) == 13,
            "Subsequent Fountain deltas accumulate normally");
        check(Do("/fountain x3") && Points(host) == 18 && Points(guest) == 24,
            "Fountain multiplier replaces accumulated offsets");
        host.PlayerAvatar.customStatsAmp["LUCK"] = 100;
        SessionSettings.Synchronize();
        check(Luck(host) == 42, "Equipment amplifier changes retain multiplier of displayed native stat");
        var arrival = add(3, 9, 10, 0);
        var snapshot = SessionSettings.ReadSnapshot();
        check(Luck(arrival) == 9 && Points(arrival) == 10 && snapshot.ActiveSettings.Any(s => s.Contains("multiplier 3")),
            "Status exposes multiplier intent without mutating pending join");
        SessionSettings.Synchronize();
        check(Luck(arrival) == 27 && Points(arrival) == 30 && Limit() == 30,
            "Late join inherits factor against its native baseline and raises carryover cap");
        for (int i = 0; i < 2; i++)
        {
            DungeonManager.Instance.constValueDictionary.Clear();
            DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] = 12;
            HorayModAPI.StartSession();
            arrival.PlayerAvatar.Inventory.dimensionPocket += 1;
            SessionSettings.BeforeNativeRead("Restart multiplier boundary");
            check(Points(arrival) == 33 + i * 3 && Limit() == Points(arrival) && Luck(arrival) == 27,
                "Reused avatars retain multiplier after restart without replay or stale carryover");
        }
        check(!Do("/stats luck x10000") && !Do("/fountain x10000.01") && !Do("/choices all x3"),
            "Invalid factors and unsupported family are rejected");
        SessionSettings.Synchronize();
        check(Luck(arrival) == 27 && Points(arrival) == 36, "Rejected commands leave prior factor and values intact");
        NetworkServer.active = false;
        check(!Do("/stats luck x2") && !Do("/fountain x2"), "Guests cannot execute multiplier commands");
        NetworkServer.active = true;
        check(Do("/stats luck x1") && Do("/fountain x1") && Luck(host) == 14 && Luck(guest) == 10 &&
            Points(host) == 6 && Points(arrival) == 12 && Limit() == 12, "Identity restores distinct current native baselines and owned limit");
        check(!SessionSettings.ReadSnapshot().ActiveSettings.Any(), "Identity clears all multiplier intent");

        host = start();
        check(Do("/fountain x1.5"), "Prepare fractional Fountain policy");
        host.PlayerAvatar.Inventory.dimensionPocket += 1;
        SessionSettings.Synchronize();
        check(Points(host) == 5 && !host.PlayerAvatar.customStats.ContainsKey(FountainPoints.ContributionKey),
            "Unrepresentable changed Fountain baseline suspends and restores native capacity");
        int warnings = UnityEngine.Debug.Warnings.Count;
        SessionSettings.Synchronize();
        check(Points(host) == 5 && UnityEngine.Debug.Warnings.Count == warnings &&
            SessionSettings.ReadSnapshot().Lines.Any(s => s.Contains("fountain-multiplier: Suspended")),
            "Suspended Fountain factor is visible without retry or warning spam");
        host.PlayerAvatar.Inventory.dimensionPocket += 1;
        SessionSettings.Synchronize();
        check(Points(host) == 9, "Compatible native change automatically resumes Fountain multiplier");
        check(Do("/stats luck x3") && Do("/one save"), "Explicit preset saves retained multipliers");
        SessionSettings.Stop();
        PlayerSpawner.MultiplayerList.Clear();
        DungeonManager.Instance = new DungeonManager();
        SessionSettings.Start();
        host = add(4, 11, 10, 0);
        SessionSettings.Synchronize();
        check(Luck(host) == 33 && Points(host) == 15, "New hosted session loads saved factors against different native values");
        SessionSettings.Stop();
        SessionSettings.Start();
        SessionSettings.Synchronize();
        check(Luck(host) == 33 && Points(host) == 15, "Addon reload with retained markers does not stack saved factors");

        host = start();
        guest = add(2, 7, 3, 0);
        check(!Do("/fountain x1.5") && Points(host) == 4 && Points(guest) == 3 && !SessionSettings.ReadSnapshot().ActiveSettings.Any(),
            "One fractional player result rejects entire Fountain batch and policy");
        check(Do("/stats luck x3") && Do("/stats luck +2") && Luck(host) == 7 && Luck(guest) == 9,
            "Delta after multiplier starts fresh offset in shared stat service");
        check(Do("/stats luck +3") && Luck(host) == 10 && Luck(guest) == 12, "Follow-up stat delta accumulates");
        check(Do("/stats luck set 100") && Do("/stats luck x2") && Luck(host) == 10 && Luck(guest) == 14,
            "Stat factor after Set uses native baseline");
        check(Do("/fountain set 100") && Do("/fountain x2") && Points(host) == 8 && Points(guest) == 6,
            "Fountain factor after Set uses native baseline");

        host = start();
        check(Do("/fountain x3"), "Prepare multiplier write fault");
        host.PlayerAvatar.Inventory.dimensionPocket += 2;
        host.PlayerAvatar.customStats.BeforeWrite = (key, value) =>
        { if (key == FountainPoints.ContributionKey) throw new InvalidOperationException("multiplier marker fault"); };
        SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().FaultedFeature == "fountain" && !Do("/stats luck x3"),
            "Partial maintenance write journals fault and blocks other commands");
        host.PlayerAvatar.customStats.BeforeWrite = null;
        check(Do("/fountain reset") && Points(host) == 6 && Do("/stats luck x3"),
            "Reset recovers multiplier maintenance fault without treating partial write as native baseline");

        host = start();
        check(Do("/stats luck x0") && Do("/fountain x0") && Luck(host) == 0 && Points(host) == 0,
            "Zero factor remains an explicit native-relative setting");
        host.PlayerAvatar.customStats["LUCK"] += 2;
        host.PlayerAvatar.Inventory.dimensionPocket += 2;
        SessionSettings.Synchronize();
        check(Luck(host) == 0 && Points(host) == 0, "Zero factor follows native gains without losing their baseline");
        check(Do("/stats luck reset") && Do("/fountain reset") && Luck(host) == 7 && Points(host) == 6,
            "Reset after zero multiplier restores native gains");
        host = start();
        host.PlayerAvatar.customStats["LUCK"] = 0;
        host.PlayerAvatar.Inventory.dimensionPocket = 0;
        check(Do("/stats luck x3") && Do("/fountain x3"), "Zero native baseline still retains multiplier intent");
        host.PlayerAvatar.customStats["LUCK"] = 2;
        host.PlayerAvatar.Inventory.dimensionPocket = 2;
        SessionSettings.Synchronize();
        check(Luck(host) == 6 && Points(host) == 6, "Later native gains from zero are multiplied");

        host = start();
        host.PlayerAvatar.customStats.BeforeWrite = (key, value) =>
        { if (key == FountainPoints.ContributionKey) throw new InvalidOperationException("identity recovery fault"); };
        check(!Do("/fountain x3"), "Prepare identity recovery after rejected partial multiplier command");
        host.PlayerAvatar.customStats.BeforeWrite = null;
        check(Do("/fountain x1") && Points(host) == 4 && !SessionSettings.ReadSnapshot().ActiveSettings.Any(),
            "Identity Fountain factor has the same explicit fault recovery as reset");
    }
}
