using SephiriaOne;

internal static class PenaltyMultiplierRuntimeTests
{
    public static void Run(Action<bool, string> check, Func<PlayerSpawner> start,
        Func<uint, int, int, int, PlayerSpawner> add)
    {
        bool Do(string command) => SettingsActions.Execute(command).Success;
        int Raw(PlayerSpawner p) => p.PlayerAvatar.customStats.GetValueOrDefault("COOLDOWNRECOVERYSPEED");
        const string marker = "SEPHIRIAONE_STAT_COOLDOWNRECOVERYSPEED";
        var host = start();
        host.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = 10;
        check(Do("/stats cooldown x3") && Do("/stats luck x3") && Do("/fountain x3") && Do("/choices item 3") &&
            Do("/resources talents +20"), "Prepare mixed settings before penalty costume joins");
        var guest = add(2, 7, 6, 1);
        guest.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = -50;
        check(SessionSettings.EnsureFresh() && Raw(guest) == -50 && Raw(host) == 30 &&
            !guest.PlayerAvatar.customStats.ContainsKey(marker), "Penalty join falls back to native and completes synchronization");
        check(guest.PlayerAvatar.customStats["LUCK"] == 21 && guest.PlayerAvatar.Inventory.dimensionPocket == 18 &&
            guest.PlayerAvatar.customStats["EXTRAITEMCHOICES"] == 4 && guest.PlayerAvatar.maxPassivePoint == 25,
            "Penalty join receives every compatible family in the same enrollment");
        check(SessionSettings.ReadSnapshot().Lines.Any(s => s.Contains("cooldown / relative-stat: NativeFallback")),
            "Status distinguishes verified native fallback from unfinished state");
        int writes = 0, warnings = UnityEngine.Debug.Warnings.Count;
        guest.PlayerAvatar.customStats.BeforeWrite = (_, _) => writes++;
        guest.PlayerAvatar.customStats.BeforeRemove = _ => writes++;
        for (int i = 0; i < 100; i++)
        {
            SessionSettings.BeforeNativeRead("Fountain grant");
            SessionSettings.BeforeNativeRead("Candidate generation");
        }
        check(writes == 0 && UnityEngine.Debug.Warnings.Count == warnings,
            "Stable fallback causes no repeated native writes or false boundary warnings");
        guest.PlayerAvatar.customStats.BeforeWrite = null;
        guest.PlayerAvatar.customStats.BeforeRemove = null;
        guest.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = 10;
        check(SessionSettings.EnsureFresh() && Raw(guest) == 30, "Compatible costume change resumes multiplier");
        guest.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] -= 60;
        check(SessionSettings.EnsureFresh() && Raw(guest) == -50 && !guest.PlayerAvatar.customStats.ContainsKey(marker),
            "Penalty after boosted costume removes exactly the previous contribution");
        check(Do("/stats cooldown x3") && Raw(guest) == -50 && Raw(host) == 30,
            "Repeated command preserves native penalty and another player's multiplier");
        check(Do("/one save"), "Fallback preserves saveable multiplier intent");
        for (int i = 0; i < 2; i++)
        {
            PlayerSpawner.MultiplayerList.Remove(guest);
            SessionSettings.Synchronize();
            guest = add(2, 7, 6, 1);
            guest.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = -50;
            guest.connectionToClient.isReady = false;
            check(!SessionSettings.EnsureFresh() && guest.PlayerAvatar.customStats["LUCK"] == 7,
                "Incomplete rejoin is not mistaken for native fallback");
            guest.connectionToClient.isReady = true;
            check(SessionSettings.EnsureFresh() && Raw(guest) == -50 && guest.PlayerAvatar.customStats["LUCK"] == 21 &&
                guest.PlayerAvatar.Inventory.dimensionPocket == 18, "Replacement connection inherits current settings again");
            DungeonManager.Instance.constValueDictionary.Clear();
            DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] = 12;
            HorayModAPI.StartSession();
            check(SessionSettings.EnsureFresh() && Raw(guest) == -50 &&
                DungeonManager.Instance.constValueDictionary[FountainPoints.LimitKey] >= 18,
                "Restart with fallback still restores Fountain carryover");
        }
        SessionSettings.Stop();
        PlayerSpawner.MultiplayerList.Clear();
        DungeonManager.Instance = new DungeonManager();
        SessionSettings.Start();
        guest = add(3, 7, 6, 1);
        guest.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = -50;
        check(SessionSettings.EnsureFresh() && Raw(guest) == -50 && guest.PlayerAvatar.customStats["LUCK"] == 21,
            "Saved session preset enrolls negative costume without rejecting other settings");
        check(Do("/stats cooldown reset") && Raw(guest) == -50, "Reset preserves negative native cooldown");

        host = start();
        host.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = 10;
        check(Do("/stats cooldown x3"), "Prepare fallback write failure");
        host.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] -= 60;
        host.PlayerAvatar.customStats.BeforeRemove = key => { if (key == marker) throw new InvalidOperationException("marker failure"); };
        check(!SessionSettings.EnsureFresh() && SessionSettings.ReadSnapshot().FaultedFeature == "stats",
            "Partially written fallback remains faulted, never fresh");
        host.PlayerAvatar.customStats.BeforeRemove = null;
        check(Do("/stats reset") && Raw(host) == -50 && SessionSettings.EnsureFresh(), "Journal recovery restores native penalty exactly once");
    }
}
