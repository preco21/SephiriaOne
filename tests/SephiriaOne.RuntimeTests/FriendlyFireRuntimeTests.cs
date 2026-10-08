using Mirror;
using SephiriaOne;

internal static class FriendlyFireRuntimeTests
{
    public static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one friendlyfire on").Success, "Host can enable friendly fire");
        check(SettingsActions.Execute("/one friendlyfire damage 25").Success, "Host can reduce allied damage");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire damage 25"), "Snapshot includes allied damage scale");
        check(SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.DamagePercent == 25, "Hit observes commands without waiting for another frame");
        HorayModAPI.StartSession();
        check(SessionSettings.FriendlyFireForHit.Enabled, "Run restart retains current combat policy");
        var connection = add(15); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Remove(connection); SessionSettings.Synchronize();
        add(15);
        check(SessionSettings.FriendlyFireForHit.DamagePercent == 25, "Replacement connection immediately uses complete current combat policy");
        foreach (string bad in new[] { "-1", "301", "NaN", "1.5", "x2" })
            check(!SettingsActions.Execute("/one friendlyfire damage " + bad).Success, "Reject invalid damage percentage " + bad);
        check(SettingsActions.Execute("/one save").Success, "Friendly fire preset can be saved");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire enabled 1"), "Saved friendly fire restores after controller restart");
        check(SettingsActions.Execute("/one friendlyfire reset").Success, "Friendly fire reset succeeds");
        check(!SessionSettings.ReadSnapshot().ActiveSettings.Any(x => x.StartsWith("friendlyfire ")), "Reset clears toggle and scale");
        FriendlyFireFeature.Available = false;
        check(!SettingsActions.Execute("/one friendlyfire on").Success && SettingsActions.Execute("/one friendlyfire reset").Success,
            "Unavailable hooks reject enable but preserve reset");
        FriendlyFireFeature.Available = true;
        check(SettingsActions.Execute("/one forget").Success, "Forget saved combat policy");
        DungeonManager.Instance = new DungeonManager();
        check(!SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.DamagePercent == 100,
            "Replacement session resolves native defaults immediately");
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one friendlyfire on").Success, "Guests cannot enable friendly fire");
    }
}
