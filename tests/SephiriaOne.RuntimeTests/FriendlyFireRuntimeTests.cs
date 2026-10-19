using Mirror;
using SephiriaOne;

internal static class FriendlyFireRuntimeTests
{
    internal static void CheckMatchIntervals(Action<bool, string> check)
    {
        var prior = SessionSettings.FriendlyFireForHit.DotIntervals;
        long token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, roles = CombatMatchRuntime.RoleRevision;
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
        {
            string command = "/one friendlyfire dot " + PvpDotSettings.Key(effect) + " ";
            check(SettingsActions.Execute(command + "1.75").Success &&
                SessionSettings.FriendlyFireForHit.DotIntervals.Seconds(effect) == 1.75m &&
                CombatMatchRuntime.IsRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                SessionSettings.CombatBalanceRevision == balance && CombatMatchRuntime.RoleRevision == roles,
                "Live interval edit preserves active mode, scores, budgets and roles: " + effect);
            check(SettingsActions.Execute(command + prior.Number(effect)).Success, "Restore prior interval: " + effect);
            if (!PvpDotSettings.SupportsSchedule(effect)) continue;
            foreach (bool enabled in new[] { false, true })
                check(SettingsActions.Execute(command + "preserve " + (enabled ? "on" : "off")).Success &&
                    SessionSettings.FriendlyFireForHit.DotIntervals.PreserveSchedule(effect) == enabled &&
                    CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                    SessionSettings.CombatBalanceRevision == balance && CombatMatchRuntime.RoleRevision == roles,
                    "Live schedule toggle preserves active mode, scores, budgets and roles: " + effect);
            check(SettingsActions.Execute(command + "preserve " + (prior.PreserveSchedule(effect) ? "on" : "off")).Success,
                "Restore prior schedule toggle: " + effect);
        }
    }

    public static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start().PlayerAvatar;
        check(SettingsActions.Execute("/one friendlyfire on").Success, "Host can enable friendly fire");
        var guest = add(14).PlayerAvatar;
        var helper = add(16).PlayerAvatar;
        long balanceRevision = SessionSettings.CombatBalanceRevision;
        var nativeStats = host.customStats.ToArray(); float nativeHp = host.hp;
        check(SettingsActions.Execute("/one friendlyfire balance on").Success && SessionSettings.FriendlyFireForHit.Balance &&
            SessionSettings.CombatBalanceRevision > balanceRevision, "Balance command updates policy and cloud revision immediately");
        check(host.hp == nativeHp && host.customStats.OrderBy(p => p.Key).SequenceEqual(nativeStats.OrderBy(p => p.Key)),
            "Enabling balancing writes no player stats or health");
        void Damage() => FriendlyFireKda.Damage(helper, guest, FriendlyFireKda.Epoch);
        void Kill()
        {
            var death = FriendlyFireKda.BeforeDeath(guest, host, FriendlyFireKda.Epoch);
            guest.IsDead = true; FriendlyFireKda.CompleteDeath(guest, death); guest.IsDead = false;
        }
        string Score(PlayerAvatar player) => FriendlyFireKda.Label(player, "P");
        Damage(); Kill();
        check(Score(host) == "P(1/0/0)" && Score(helper) == "P(0/0/1)", "Enabled command starts host KDA accounting");
        balanceRevision = SessionSettings.CombatBalanceRevision;
        long frequencyRevision = SessionSettings.CombatFrequencyRevision;
        check(SettingsActions.Execute("/one friendlyfire hit-frequency on").Success && SessionSettings.FriendlyFireForHit.HitFrequency &&
            SessionSettings.CombatFrequencyRevision > frequencyRevision && SessionSettings.CombatBalanceRevision == balanceRevision && Score(host) == "P(1/0/0)",
            "Frequency toggle updates only its own history revision, preserving budgets and scores");
        frequencyRevision = SessionSettings.CombatFrequencyRevision;
        SettingsActions.Execute("/one friendlyfire hit-frequency on");
        check(SessionSettings.CombatFrequencyRevision == frequencyRevision, "Repeated on command cannot reset frequency history");
        check(SettingsActions.Execute("/one friendlyfire hit-frequency off").Success && SettingsActions.Execute("/one friendlyfire hit-frequency on").Success &&
            SessionSettings.CombatFrequencyRevision == frequencyRevision + 2 && SessionSettings.CombatBalanceRevision == balanceRevision,
            "Off/on between hits invalidates frequency only");
        check(SessionSettings.ReadSnapshot().FriendlyFire.HitFrequency && SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Hit-frequency scaling: on")),
            "Frequency checkbox snapshot and status report the same setting");
        check(SettingsActions.Execute("/one friendlyfire refill 125.5").Success &&
            SessionSettings.FriendlyFireForHit.RefillPercent == 125.5m && SessionSettings.CombatBalanceRevision == balanceRevision && Score(host) == "P(1/0/0)",
            "Refill edits reach hits immediately without resetting KDA or budget revision");
        check(SessionSettings.ReadSnapshot().FriendlyFire.RefillPercent == 125.5m &&
            SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("125.5% max HP/s")), "Snapshot and status expose current refill");
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
            check(SettingsActions.Execute("/one friendlyfire dot " + PvpDotSettings.Key(effect) + " " + ((int)effect + 1)).Success,
                "Host can edit each DoT interval live: " + effect);
        foreach (var effect in Enum.GetValues<PvpDotEffect>())
            if (PvpDotSettings.SupportsSchedule(effect))
                check(SettingsActions.Execute("/one friendlyfire dot " + PvpDotSettings.Key(effect) + " preserve off").Success,
                    "Host can edit protection independently: " + effect);
        var dotSettings = SessionSettings.FriendlyFireForHit.DotIntervals;
        check(SessionSettings.CombatBalanceRevision == balanceRevision && Score(host) == "P(1/0/0)" &&
            SessionSettings.ReadSnapshot().FriendlyFire.DotIntervals.Equals(dotSettings), "DoT edits preserve KDA/budgets and update snapshot immediately");
        check(SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Burn 1; Plasma Burn 2; Frostbite 3; Poison 4")), "Status reports each independent interval");
        check(SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Wound 5; Lightning/Shock 6; Freeze damage 7; Chim 8; Dark Cloud 9")),
            "Status reports all newly configurable periodic and pulse intervals");
        check(!SettingsActions.Execute("/one friendlyfire dot poison 0").Success && SessionSettings.FriendlyFireForHit.DotIntervals.Equals(dotSettings), "Rejected interval cannot partially change policy");
        check(SettingsActions.Execute("/one friendlyfire block-lobby-regen on").Success &&
            SettingsActions.Execute("/one friendlyfire block-lobby-regen off").Success &&
            SessionSettings.CombatBalanceRevision == balanceRevision && Score(host) == "P(1/0/0)",
            "Recovery edits preserve KDA and balancing budgets");
        check(SettingsActions.Execute("/one friendlyfire balance off").Success && SettingsActions.Execute("/one friendlyfire balance on").Success &&
            SessionSettings.CombatBalanceRevision == balanceRevision + 2 && Score(host) == "P(1/0/0)",
            "Off/on without damage advances the limiter revision while retaining casual KDA");
        check(SessionSettings.ReadSnapshot().FriendlyFire.Balance && SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("PvP balancing: on.")),
            "Combat snapshot and status expose the same balance preference");
        SettingsActions.Execute("/one friendlyfire on");
        check(Score(host) == "P(1/0/0)", "No-op on command preserves scores");
        check(SettingsActions.Execute("/one friendlyfire damage 25").Success, "Host can reduce allied damage");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire damage 25"), "Snapshot includes allied damage scale");
        check(SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.DamagePercent == 25, "Hit observes commands without waiting for another frame");
        HorayModAPI.StartSession();
        check(SessionSettings.FriendlyFireForHit.Enabled, "Run restart retains current combat policy");
        check(SessionSettings.FriendlyFireForHit.RefillPercent == 125.5m, "Run restart retains configured refill");
        check(SessionSettings.FriendlyFireForHit.DotIntervals.Equals(dotSettings), "Run restart retains all nine current effect intervals");
        check(Score(host) == "P(1/0/0)", "Run restart preserves totals");
        Damage(); HorayModAPI.StartSession(); Kill();
        check(Score(host) == "P(2/0/0)" && Score(helper) == "P(0/0/1)", "Run restart clears previous pending assists");
        SettingsActions.Execute("/one friendlyfire off");
        check(Score(host) == "P(0/0/0)" && Score(helper) == "P(0/0/0)", "Off command resets totals immediately without a hit");
        SettingsActions.Execute("/one friendlyfire on"); Damage();
        SettingsActions.Execute("/one friendlyfire damage 50"); Kill();
        check(Score(helper) == "P(0/0/1)", "Damage command does not reset assists");
        check(SettingsActions.Execute("/one friendlyfire damage 0.1%").Success, "Host can enter a sub-one percentage with optional suffix");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire damage 0.1") &&
            SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("0.1%")), "Snapshot and status retain fractional percent");
        Damage(); PlayerSpawner.MultiplayerList.Remove(guest.spawner); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Add(guest.spawner); SessionSettings.Synchronize(); Kill();
        check(Score(helper) == "P(0/0/1)", "Departure forgets old victim contributions even when object is reused");
        var connection = add(15); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Remove(connection); SessionSettings.Synchronize();
        add(15);
        check(SessionSettings.FriendlyFireForHit.DamagePercent == 0.1m, "Replacement connection immediately uses complete fractional combat policy");
        check(SessionSettings.FriendlyFireForHit.RefillPercent == 125.5m, "Replacement connection uses current refill policy");
        check(SessionSettings.FriendlyFireForHit.Balance, "Rejoined players immediately receive current balancing policy");
        check(SessionSettings.FriendlyFireForHit.HitFrequency, "Rejoined players immediately use current hit-frequency preference");
        check(SessionSettings.FriendlyFireForHit.DotIntervals.Equals(dotSettings), "Replacement connection gets all current DoT intervals, not stale per-player values");
        HorayModAPI.StartSession();
        check(SessionSettings.FriendlyFireForHit.DamagePercent == 0.1m, "Run restart retains fractional combat policy");
        foreach (string bad in new[] { "-1", "301", "NaN", "0.001", "x2" })
            check(!SettingsActions.Execute("/one friendlyfire damage " + bad).Success, "Reject invalid damage percentage " + bad);
        check(SettingsActions.Execute("/one save").Success, "Friendly fire preset can be saved");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("friendlyfire enabled 1"), "Saved friendly fire restores after controller restart");
        check(SessionSettings.FriendlyFireForHit.DamagePercent == 0.1m, "Saved sub-one scale restores exactly after controller restart");
        check(SessionSettings.FriendlyFireForHit.Balance, "Saved balancing restores after full controller restart");
        check(SessionSettings.FriendlyFireForHit.HitFrequency, "Saved frequency preference restores after full controller restart");
        check(SessionSettings.FriendlyFireForHit.RefillPercent == 125.5m, "Saved refill restores after full controller restart");
        check(SessionSettings.FriendlyFireForHit.DotIntervals.Equals(dotSettings), "Saved intervals restore after full controller restart");
        check(Score(host) == "P(0/0/0)", "Saved settings never persist KDA");
        Damage(); Kill();
        check(Score(host) == "P(1/0/0)", "Restored enabled preset activates fresh accounting");
        check(SettingsActions.Execute("/one friendlyfire reset").Success, "Friendly fire reset succeeds");
        check(!SessionSettings.ReadSnapshot().ActiveSettings.Any(x => x.StartsWith("friendlyfire ")), "Reset clears toggle and scale");
        check(Score(host) == "P(0/0/0)", "Reset command clears KDA");
        check(SessionSettings.FriendlyFireForHit.RefillPercent == 70, "Full reset restores default refill");
        check(!SessionSettings.FriendlyFireForHit.HitFrequency, "Full reset restores hit-frequency default off");
        check(!SessionSettings.FriendlyFireForHit.DotIntervals.HasChanges, "Full reset restores all nine current per-effect defaults");
        FriendlyFireFeature.Available = false;
        check(!SettingsActions.Execute("/one friendlyfire hit-frequency on").Success &&
            SettingsActions.Execute("/one friendlyfire hit-frequency off").Success && SettingsActions.Execute("/one friendlyfire hit-frequency reset").Success,
            "Unavailable hooks reject frequency enable but retain off/reset recovery");
        check(!SettingsActions.Execute("/one friendlyfire balance on").Success && SettingsActions.Execute("/one friendlyfire balance off").Success,
            "Unavailable hooks reject balance enable but preserve off recovery");
        check(!SettingsActions.Execute("/one friendlyfire on").Success && SettingsActions.Execute("/one friendlyfire reset").Success,
            "Unavailable hooks reject enable but preserve reset");
        FriendlyFireFeature.Available = true;
        check(SettingsActions.Execute("/one forget").Success, "Forget saved combat policy");
        DungeonManager.Instance = new DungeonManager();
        check(!SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.DamagePercent == 100,
            "Replacement session resolves native defaults immediately");
        check(!SessionSettings.FriendlyFireForHit.Balance, "An unsaved new session starts with balancing off");
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one friendlyfire on").Success, "Guests cannot enable friendly fire");
        check(!SettingsActions.Execute("/one friendlyfire balance on").Success, "Guests cannot change host balancing policy");
        check(!SettingsActions.Execute("/one friendlyfire refill 200").Success, "Guests cannot change host refill policy");
        check(!SettingsActions.Execute("/one friendlyfire dot burn 2").Success, "Guests cannot change host interval policy");
        check(!SettingsActions.Execute("/one friendlyfire hit-frequency on").Success, "Guests cannot change host frequency policy");
    }
}
