using Mirror;
using SephiriaOne;

internal static class FriendlyFireRuntimeTests
{
    public static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start().PlayerAvatar;
        check(SettingsActions.Execute("/one friendlyfire on").Success, "Host can enable friendly fire");
        var guest = add(14).PlayerAvatar;
        var helper = add(16).PlayerAvatar;
        void Damage() => FriendlyFireKda.Damage(helper, guest, FriendlyFireKda.Epoch);
        void Kill()
        {
            var death = FriendlyFireKda.BeforeDeath(guest, host, FriendlyFireKda.Epoch);
            guest.IsDead = true; FriendlyFireKda.CompleteDeath(guest, death); guest.IsDead = false;
        }
        string Score(PlayerAvatar player) => FriendlyFireKda.Label(player, "P");
        Damage(); Kill();
        check(Score(host) == "P(1/0/0)" && Score(helper) == "P(0/0/1)", "Enabled command starts host KDA accounting");
        SettingsActions.Execute("/one friendlyfire on");
        check(Score(host) == "P(1/0/0)", "No-op on command preserves scores");
        check(SettingsActions.Execute("/one friendlyfire damage 25").Success, "Host can reduce allied damage");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire damage 25"), "Snapshot includes allied damage scale");
        check(SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.DamagePercent == 25, "Hit observes commands without waiting for another frame");
        HorayModAPI.StartSession();
        check(SessionSettings.FriendlyFireForHit.Enabled, "Run restart retains current combat policy");
        check(Score(host) == "P(1/0/0)", "Run restart preserves totals");
        Damage(); HorayModAPI.StartSession(); Kill();
        check(Score(host) == "P(2/0/0)" && Score(helper) == "P(0/0/1)", "Run restart clears previous pending assists");
        SettingsActions.Execute("/one friendlyfire off");
        check(Score(host) == "P(0/0/0)" && Score(helper) == "P(0/0/0)", "Off command resets totals immediately without a hit");
        SettingsActions.Execute("/one friendlyfire on"); Damage();
        SettingsActions.Execute("/one friendlyfire damage 50"); Kill();
        check(Score(helper) == "P(0/0/1)", "Damage command does not reset assists");
        SettingsActions.Execute("/one friendlyfire damage 25");
        Damage(); PlayerSpawner.MultiplayerList.Remove(guest.spawner); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Add(guest.spawner); SessionSettings.Synchronize(); Kill();
        check(Score(helper) == "P(0/0/1)", "Departure forgets old victim contributions even when object is reused");
        var connection = add(15); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Remove(connection); SessionSettings.Synchronize();
        add(15);
        check(SessionSettings.FriendlyFireForHit.DamagePercent == 25, "Replacement connection immediately uses complete current combat policy");
        foreach (string bad in new[] { "-1", "301", "NaN", "1.5", "x2" })
            check(!SettingsActions.Execute("/one friendlyfire damage " + bad).Success, "Reject invalid damage percentage " + bad);
        check(SettingsActions.Execute("/one save").Success, "Friendly fire preset can be saved");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire enabled 1"), "Saved friendly fire restores after controller restart");
        check(Score(host) == "P(0/0/0)", "Saved settings never persist KDA");
        Damage(); Kill();
        check(Score(host) == "P(1/0/0)", "Restored enabled preset activates fresh accounting");
        check(SettingsActions.Execute("/one friendlyfire reset").Success, "Friendly fire reset succeeds");
        check(!SessionSettings.ReadSnapshot().ActiveSettings.Any(x => x.StartsWith("friendlyfire ")), "Reset clears toggle and scale");
        check(Score(host) == "P(0/0/0)", "Reset command clears KDA");
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
