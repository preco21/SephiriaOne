using Mirror;
using SephiriaOne;

internal static class PvpEvasionControlsTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start(); var guest = add(2);
        check(SettingsActions.Execute("/one friendlyfire balance on").Success && SettingsActions.Execute("/one friendlyfire on").Success,
            "Start casual balanced PvP for Evasion controls");
        guest.connectionToClient.isReady = false;
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "Evasion controls fixture");
        check(SettingsActions.Execute("/one friendlyfire evasion limits 25.5 60.75").Success,
            "Policy-only Evasion edits remain available with loading guests and unrelated stat faults");
        check(SettingsActions.Execute("/one friendlyfire evasion on").Success && SessionSettings.FriendlyFireForDisplay.Evasion.Enabled,
            "Evasion enables through the shared host command");
        check(SettingsActions.Execute("/one friendlyfire evasion off").Success && !SessionSettings.FriendlyFireForDisplay.Evasion.Enabled &&
            SessionSettings.FriendlyFireForDisplay.Evasion.SoftPercent == 25.5m, "Off works while a guest loads and retains tuning");
        check(SettingsActions.Execute("/one friendlyfire evasion reset").Success && !SessionSettings.FriendlyFireForDisplay.Evasion.HasChanges,
            "Reset works while a guest loads and restores only Evasion defaults");
        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            UnityEngine.Time.unscaledTime = 0;
            host = start(); guest = add(2); add(3); add(4); SessionSettings.EnsureResourceScope();
            check(SettingsActions.Execute("/one friendlyfire balance on").Success &&
                SettingsActions.Execute("/one " + mode + " start").Success && CombatMatchRuntime.IsRunning,
                "Start shared mode for Evasion controls: " + mode);
            var score = FriendlyFireKda.For(host.PlayerAvatar); score.Kills = 7;
            long epoch = FriendlyFireKda.Epoch, balance = SessionSettings.CombatBalanceRevision,
                frequency = SessionSettings.CombatFrequencyRevision, recovery = SessionSettings.CombatRecoveryRevision;
            foreach (string operation in new[] { "limits 20 40", "on", "off", "reset" })
            {
                check(SettingsActions.Execute("/one friendlyfire evasion " + operation).Success && CombatMatchRuntime.IsRunning,
                    "Evasion edit preserves running mode: " + mode + " / " + operation);
                check(FriendlyFireKda.Epoch == epoch && ReferenceEquals(score, FriendlyFireKda.For(host.PlayerAvatar)) && score.Kills == 7 &&
                    SessionSettings.CombatBalanceRevision == balance && SessionSettings.CombatFrequencyRevision == frequency &&
                    SessionSettings.CombatRecoveryRevision == recovery, "Evasion edit preserves K/D/A and balance budget revisions");
            }
            NetworkServer.active = false;
            check(!SettingsActions.Execute("/one friendlyfire evasion on").Success, "Guests cannot change host Evasion policy");
            NetworkServer.active = true;
            check(SettingsActions.Execute("/one " + mode + " stop").Success, "Stop shared mode after Evasion controls");
        }
        start();
        var before = SessionSettings.FriendlyFireForDisplay;
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, evasion: new PvpEvasionChange(softPercent: 50, hardPercent: 30)), out _) &&
            SessionSettings.FriendlyFireForDisplay.Equals(before), "Invalid programmatic Evasion edit is rejected without mutation");
        start();
    }
}
