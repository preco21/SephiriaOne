using Mirror;
using SephiriaOne;

internal static class PvpMaxHpControlsTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start(); var guest = add(2);
        check(SettingsActions.Execute("/one friendlyfire balance on").Success && SettingsActions.Execute("/one friendlyfire on").Success,
            "Start casual balanced PvP for Max HP controls");
        guest.connectionToClient.isReady = false;
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "Max HP controls fixture");
        check(SettingsActions.Execute("/one friendlyfire max-hp limits 120.25 60.5 90.75").Success,
            "Policy-only Max HP edits remain available with loading guests and unrelated stat faults");
        check(SettingsActions.Execute("/one friendlyfire max-hp on").Success && SessionSettings.FriendlyFireForDisplay.MaxHp.Enabled,
            "Max HP enables through the shared host command");
        check(SettingsActions.Execute("/one friendlyfire max-hp off").Success && !SessionSettings.FriendlyFireForDisplay.MaxHp.Enabled &&
            SessionSettings.FriendlyFireForDisplay.MaxHp.Baseline == 120.25m, "Off works while a guest loads and retains tuning");
        check(SettingsActions.Execute("/one friendlyfire max-hp reset").Success && !SessionSettings.FriendlyFireForDisplay.MaxHp.HasChanges,
            "Reset works while a guest loads and restores only Max HP defaults");
        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            UnityEngine.Time.unscaledTime = 0;
            host = start(); guest = add(2); add(3); add(4); SessionSettings.EnsureResourceScope();
            check(SettingsActions.Execute("/one friendlyfire balance on").Success &&
                SettingsActions.Execute("/one " + mode + " start").Success && CombatMatchRuntime.IsRunning,
                "Start shared mode for Max HP controls: " + mode);
            var score = FriendlyFireKda.For(host.PlayerAvatar); score.Kills = 7;
            long epoch = FriendlyFireKda.Epoch, balance = SessionSettings.CombatBalanceRevision,
                frequency = SessionSettings.CombatFrequencyRevision, recovery = SessionSettings.CombatRecoveryRevision;
            foreach (string operation in new[] { "limits 125 60 120", "on", "off", "reset" })
            {
                check(SettingsActions.Execute("/one friendlyfire max-hp " + operation).Success && CombatMatchRuntime.IsRunning,
                    "Max HP edit preserves running mode: " + mode + " / " + operation);
                check(FriendlyFireKda.Epoch == epoch && ReferenceEquals(score, FriendlyFireKda.For(host.PlayerAvatar)) && score.Kills == 7 &&
                    SessionSettings.CombatBalanceRevision == balance && SessionSettings.CombatFrequencyRevision == frequency &&
                    SessionSettings.CombatRecoveryRevision == recovery, "Max HP edit preserves K/D/A and balance budget revisions");
            }
            NetworkServer.active = false;
            check(!SettingsActions.Execute("/one friendlyfire max-hp on").Success, "Guests cannot change host Max HP policy");
            NetworkServer.active = true;
            check(SettingsActions.Execute("/one " + mode + " stop").Success, "Stop shared mode after Max HP controls");
        }
        start();
        var before = SessionSettings.FriendlyFireForDisplay;
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, maxHp: new PvpMaxHpChange(baseline: 0, soft: 50, hard: 100)), out _) &&
            SessionSettings.FriendlyFireForDisplay.Equals(before), "Invalid programmatic Max HP edit is rejected without mutation");
        PvpMaxHpFeature.Available = false;
        try
        {
            var snapshot = SessionSettings.ReadSnapshot();
            var compact = SessionSettings.ReadSnapshot(false);
            check(!snapshot.MaxHpAvailable && snapshot.FriendlyFireAvailable && !compact.MaxHpAvailable && compact.FriendlyFireAvailable,
                "Full and compact snapshots isolate Max HP compatibility from friendly fire");
            check(!SettingsActions.Execute("/one friendlyfire max-hp on").Success && !SessionSettings.FriendlyFireForDisplay.MaxHp.Enabled,
                "Failed native Max HP contracts prevent enabling only this feature");
            check(SettingsActions.Execute("/one friendlyfire balance on").Success, "Max HP compatibility cannot block unrelated balancing controls");
            check(SettingsActions.Execute("/one friendlyfire max-hp limits 125 60 120").Success,
                "Unavailable Max HP feature still permits saved policy tuning");
            FriendlyFireFeature.Available = false;
            check(SettingsActions.Execute("/one friendlyfire max-hp off").Success && SettingsActions.Execute("/one friendlyfire max-hp reset").Success,
                "Off/reset remain usable after Max HP and friendly-fire compatibility failure");
        }
        finally { PvpMaxHpFeature.Available = true; FriendlyFireFeature.Available = true; }
        start();
    }
}
