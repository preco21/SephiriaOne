using Mirror;
using SephiriaOne;

internal static class FriendlyFireRuntimeTests
{
    internal static void CheckMatchIntervals(Action<bool, string> check)
    {
        CheckMatchAmplification(check);
        CheckRapidEdits(check, true);
        CheckPerfectGuardEdits(check, true);
        CheckDotDurationEdits(check, true);
        CheckArtifactRateEdits(check, true);
        CheckThornsEdits(check, true);
        CheckLowerBoundEdits(check, true);
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

    private static void CheckMatchAmplification(Action<bool, string> check)
    {
        var prior = SessionSettings.FriendlyFireForHit.Amplification;
        long token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision,
            roles = CombatMatchRuntime.RoleRevision;
        foreach (string operation in new[] { "limits 200 300", "on", "off", "reset" })
            check(SettingsActions.Execute("/one friendlyfire amplification " + operation).Success &&
                CombatMatchRuntime.IsRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                SessionSettings.CombatBalanceRevision == balance && SessionSettings.CombatFrequencyRevision == frequency &&
                CombatMatchRuntime.RoleRevision == roles, "Amplification edits preserve live match, scores, budgets, frequency and roles: " + operation);
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, amplification:
            new PvpAmplificationChange(prior.Enabled, prior.SoftPercent, prior.HardPercent)), out _), "Restore match amplification settings");
    }

    private static void CheckRapidEdits(Action<bool, string> check, bool matchRunning)
    {
        var prior = SessionSettings.FriendlyFireForHit;
        // First participant inspection establishes the run scope and retires old lives.
        // Establish it before measuring whether numeric edits preserve that scope.
        _ = FriendlyFireParticipants.Epoch;
        long token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision,
            roles = CombatMatchRuntime.RoleRevision;
        foreach (string edit in new[] { "window 2.25", "rate 8.75", "exponent 1.25" })
        {
            check(SettingsActions.Execute("/one friendlyfire rapid " + edit).Success &&
                SessionSettings.CombatFrequencyRevision == ++frequency, "Effective rapid history edit advances only frequency revision: " + edit);
            check(SettingsActions.Execute("/one friendlyfire rapid " + edit).Success &&
                SessionSettings.CombatFrequencyRevision == frequency, "Unchanged rapid edit retains frequency history: " + edit);
        }
        check(SettingsActions.Execute("/one friendlyfire rapid burst 35.25").Success &&
            SettingsActions.Execute("/one friendlyfire refill 90").Success && SessionSettings.CombatFrequencyRevision == frequency,
            "Capacity and continuous refill edits preserve frequency history");
        check(SessionSettings.CombatBalanceRevision == balance && FriendlyFireKda.Epoch == scores && CombatMatchRuntime.MatchToken == token &&
            CombatMatchRuntime.RoleRevision == roles && CombatMatchRuntime.IsRunning == matchRunning &&
            SessionSettings.FriendlyFireForHit.Enabled == prior.Enabled && SessionSettings.FriendlyFireForHit.Balance == prior.Balance &&
            SessionSettings.FriendlyFireForHit.HitFrequency == prior.HitFrequency, "All rapid edits preserve active mode, switches, scores, roles and budget revision");
        var configured = SessionSettings.FriendlyFireForHit.Rapid;
        long recoveryRevision = SessionSettings.CombatRecoveryRevision;
        foreach (string edit in new[] { "recovery off", "interval 2.25", "recovery on" })
        {
            check(SettingsActions.Execute("/one friendlyfire rapid " + edit).Success && SessionSettings.CombatRecoveryRevision == ++recoveryRevision &&
                SessionSettings.CombatFrequencyRevision == frequency && SessionSettings.CombatBalanceRevision == balance &&
                CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores && CombatMatchRuntime.IsRunning == matchRunning,
                "Recovery edit rearms only its deadline across casual and match modes: " + edit);
            check(SettingsActions.Execute("/one friendlyfire rapid " + edit).Success && SessionSettings.CombatRecoveryRevision == recoveryRevision,
                "Repeated recovery command preserves deadline");
        }
        check(SettingsActions.Execute("/one friendlyfire rapid interval reset").Success, "Restore recovery interval after mode check");
        check(SessionSettings.ReadSnapshot().FriendlyFire.Rapid.Equals(configured) &&
            SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Rapid history: 2.25s;") &&
                m.Contains("8.75 hits/s") && m.Contains("exponent: 1.25") && m.Contains("35.25% max HP") &&
                m.Contains("base empty-to-full recovery: 0.392s")), "Snapshot and status expose exact rapid settings and base recovery time");
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, rapid: new PvpRapidChange(windowSeconds: 0)), out _) &&
            SessionSettings.FriendlyFireForHit.Rapid.Equals(configured) && SessionSettings.CombatFrequencyRevision == frequency,
            "Invalid typed rapid changes fail atomically before reconciliation");
        var rapidReset = SettingsActions.Execute("/one friendlyfire rapid reset");
        frequency++;
        check(rapidReset.Success && !SessionSettings.FriendlyFireForHit.Rapid.HasChanges &&
            SessionSettings.CombatFrequencyRevision == frequency && SessionSettings.FriendlyFireForHit.RefillPercent == 90 &&
            SessionSettings.CombatBalanceRevision == balance && FriendlyFireKda.Epoch == scores && CombatMatchRuntime.MatchToken == token,
            "Rapid reset restores only its four numbers and advances history revision once");
        check(SettingsActions.Execute("/one friendlyfire rapid reset").Success && SessionSettings.CombatFrequencyRevision == frequency,
            "Repeated numeric reset never clears history again");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, rapid: new PvpRapidChange(prior.Rapid.WindowSeconds,
            prior.Rapid.HitsPerSecond, prior.Rapid.Exponent, prior.Rapid.BurstPercent,
            periodicReset: prior.Rapid.PeriodicReset, resetSeconds: prior.Rapid.ResetSeconds), refillPercent: prior.RefillPercent), out _),
            "Restore prior rapid and refill controls");
    }

    private static void CheckPerfectGuardEdits(Action<bool, string> check, bool matchRunning)
    {
        var prior = SessionSettings.FriendlyFireForHit;
        long participants = FriendlyFireParticipants.Epoch, token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision,
            roles = CombatMatchRuntime.RoleRevision;
        foreach (string operation in new[] { "multiplier x3.25", "on", "off", "on", "multiplier reset", "multiplier 100", "reset" })
            check(SettingsActions.Execute("/one friendlyfire perfect-guard " + operation).Success &&
                CombatMatchRuntime.IsRunning == matchRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                SessionSettings.CombatBalanceRevision == balance && SessionSettings.CombatFrequencyRevision == frequency &&
                CombatMatchRuntime.RoleRevision == roles && FriendlyFireParticipants.Epoch == participants &&
                SessionSettings.FriendlyFireForHit.Enabled == prior.Enabled && SessionSettings.FriendlyFireForHit.Balance == prior.Balance &&
                SessionSettings.FriendlyFireForHit.HitFrequency == prior.HitFrequency,
                "Perfect Guard edits preserve mode, participants, switches, scores and all revisions: " + operation);
        check(SettingsActions.Execute("/one friendlyfire perfect-guard multiplier x3.25").Success &&
            SettingsActions.Execute("/one friendlyfire perfect-guard on").Success, "Configure Perfect Guard for status and snapshot");
        var configured = SessionSettings.FriendlyFireForHit.PerfectGuard;
        check(configured.Enabled && configured.Multiplier == 3.25m && SessionSettings.ReadSnapshot().FriendlyFire.PerfectGuard.Equals(configured) &&
            SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Perfect Guard MP multiplier: on; x3.25 native MP cost")),
            "Perfect Guard status and snapshot expose current multiplier and cost basis");
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, perfectGuard: new PvpPerfectGuardChange(multiplier: 0)), out _) &&
            !SettingsActions.Execute("/one friendlyfire perfect-guard multiplier 1.001").Success &&
            SessionSettings.FriendlyFireForHit.PerfectGuard.Equals(configured) && SessionSettings.CombatBalanceRevision == balance &&
            SessionSettings.CombatFrequencyRevision == frequency && FriendlyFireKda.Epoch == scores && CombatMatchRuntime.MatchToken == token,
            "Invalid Perfect Guard commands fail atomically before any state changes");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, perfectGuard:
            new PvpPerfectGuardChange(prior.PerfectGuard.Enabled, prior.PerfectGuard.Multiplier)), out _), "Restore prior Perfect Guard policy");
    }

    private static void CheckLowerBoundEdits(Action<bool, string> check, bool matchRunning)
    {
        var prior = SessionSettings.FriendlyFireForHit;
        long participants = FriendlyFireParticipants.Epoch, token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision, roles = CombatMatchRuntime.RoleRevision;
        foreach (string operation in new[] { "reference 5.25", "refill 10.5", "on", "off", "on", "reference reset", "refill reset", "reset" })
            check(SettingsActions.Execute("/one friendlyfire lower-bound " + operation).Success &&
                CombatMatchRuntime.IsRunning == matchRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                FriendlyFireParticipants.Epoch == participants && SessionSettings.CombatBalanceRevision == balance &&
                SessionSettings.CombatFrequencyRevision == frequency && CombatMatchRuntime.RoleRevision == roles &&
                SessionSettings.FriendlyFireForHit.Enabled == prior.Enabled && SessionSettings.FriendlyFireForHit.Balance == prior.Balance,
                "Lower-bound edits preserve match, participants, switches, scores and all combat revisions: " + operation);
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, lowerBound: new(referencePercent: 0)), out _) &&
            !SettingsActions.Execute("/one friendlyfire lower-bound refill 8.001").Success &&
            !SessionSettings.FriendlyFireForHit.LowerBound.HasChanges && SessionSettings.CombatBalanceRevision == balance,
            "Invalid assistance settings fail atomically");
        check(SettingsActions.Execute("/one friendlyfire lower-bound reference 5.25").Success &&
            SettingsActions.Execute("/one friendlyfire lower-bound refill 10.5").Success &&
            SettingsActions.Execute("/one friendlyfire lower-bound on").Success, "Configure assistance snapshot and status");
        var configured = SessionSettings.ReadSnapshot().FriendlyFire.LowerBound;
        check(configured.Enabled && configured.ReferencePercent == 5.25m && configured.RefillPercent == 10.5m &&
            SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("lower-bound assistance: on; reference 5.25%") &&
                m.Contains("shared bonus refill 10.5%")), "Snapshot and status expose current assistance settings");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, lowerBound:
            new(prior.LowerBound.Enabled, prior.LowerBound.ReferencePercent, prior.LowerBound.RefillPercent)), out _), "Restore assistance settings");
    }

    private static void CheckThornsEdits(Action<bool, string> check, bool matchRunning)
    {
        var prior = SessionSettings.FriendlyFireForHit;
        long participants = FriendlyFireParticipants.Epoch, token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision, roles = CombatMatchRuntime.RoleRevision;
        foreach (string value in new[] { "on", "off", "on", "reset" })
            check(SettingsActions.Execute("/one friendlyfire suppress-thorns " + value).Success &&
                SessionSettings.FriendlyFireForHit.SuppressThorns == (value == "on") &&
                SessionSettings.ReadSnapshot().FriendlyFire.SuppressThorns == (value == "on") &&
                CombatMatchRuntime.IsRunning == matchRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                FriendlyFireParticipants.Epoch == participants && SessionSettings.CombatBalanceRevision == balance &&
                SessionSettings.CombatFrequencyRevision == frequency && CombatMatchRuntime.RoleRevision == roles,
                "Thorns edits preserve match, scores, participants, budgets and roles: " + value);
        check(SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Suppress PvP Thorns: off")), "Status exposes Thorns preference");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, suppressThorns: prior.SuppressThorns), out _), "Restore prior Thorns preference");
    }

    private static void CheckArtifactRateEdits(Action<bool, string> check, bool matchRunning)
    {
        var prior = SessionSettings.FriendlyFireForHit;
        long participants = FriendlyFireParticipants.Epoch, token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision, roles = CombatMatchRuntime.RoleRevision;
        foreach (string value in new[] { "interval 3.25", "on", "off", "interval reset", "reset" })
            check(SettingsActions.Execute("/one friendlyfire artifact-rate " + value).Success &&
                CombatMatchRuntime.IsRunning == matchRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                FriendlyFireParticipants.Epoch == participants && SessionSettings.CombatBalanceRevision == balance &&
                SessionSettings.CombatFrequencyRevision == frequency && CombatMatchRuntime.RoleRevision == roles,
                "Artifact-rate edits preserve active match and all combat lifetime revisions: " + value);
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, artifactRate: new(seconds: 0)), out _) &&
            !SettingsActions.Execute("/one friendlyfire artifact-rate interval 1.001").Success &&
            !SessionSettings.FriendlyFireForHit.ArtifactRate.HasChanges, "Invalid artifact policy rejects atomically");
        check(SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("Autonomous artifact hit limit: off; 2s")) &&
            !SessionSettings.ReadSnapshot().FriendlyFire.ArtifactRate.HasChanges, "Snapshot/status expose new controls");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, artifactRate:
            new(prior.ArtifactRate.Enabled, prior.ArtifactRate.Seconds)), out _), "Restore previous artifact policy");
    }

    private static void CheckDotDurationEdits(Action<bool, string> check, bool matchRunning)
    {
        var prior = SessionSettings.FriendlyFireForHit;
        long participants = FriendlyFireParticipants.Epoch, token = CombatMatchRuntime.MatchToken, scores = FriendlyFireKda.Epoch,
            balance = SessionSettings.CombatBalanceRevision, frequency = SessionSettings.CombatFrequencyRevision, roles = CombatMatchRuntime.RoleRevision;
        foreach (string value in new[] { "0.05", "3.33", "10", "reset" })
            check(SettingsActions.Execute("/one friendlyfire dot-duration " + value).Success &&
                CombatMatchRuntime.IsRunning == matchRunning && CombatMatchRuntime.MatchToken == token && FriendlyFireKda.Epoch == scores &&
                FriendlyFireParticipants.Epoch == participants && SessionSettings.CombatBalanceRevision == balance &&
                SessionSettings.CombatFrequencyRevision == frequency && CombatMatchRuntime.RoleRevision == roles &&
                SessionSettings.FriendlyFireForHit.DotIntervals.Equals(prior.DotIntervals),
                "Duration edits preserve mode, KDA, interval/schedule settings, participants and all combat revisions: " + value);
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, dotMaxDurationSeconds: 0), out _) &&
            !SettingsActions.Execute("/one friendlyfire dot-duration 1.201").Success && SessionSettings.FriendlyFireForHit.DotMaxDurationSeconds == 1.2m,
            "Invalid duration commands reject atomically");
        check(SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("PvP Burn-family duration cap: 1.2s")) &&
            SessionSettings.ReadSnapshot().FriendlyFire.DotMaxDurationSeconds == 1.2m, "Status and UI snapshot expose current duration");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, dotMaxDurationSeconds: prior.DotMaxDurationSeconds), out _),
            "Restore prior duration after mode checks");
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
        CheckRapidEdits(check, false);
        CheckPerfectGuardEdits(check, false);
        CheckDotDurationEdits(check, false);
        CheckArtifactRateEdits(check, false);
        CheckThornsEdits(check, false);
        CheckLowerBoundEdits(check, false);
        check(Score(host) == "P(1/0/0)" && Score(helper) == "P(0/0/1)", "Perfect Guard edits retain exact K/D/A totals");
        check(Score(host) == "P(1/0/0)" && Score(helper) == "P(0/0/1)", "Rapid number edits retain exact K/D/A scores");
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
        frequencyRevision = SessionSettings.CombatFrequencyRevision;
        nativeStats = host.customStats.ToArray(); nativeHp = host.hp;
        check(SettingsActions.Execute("/one friendlyfire amplification limits 70.25 125.5").Success &&
            SettingsActions.Execute("/one friendlyfire amplification on").Success &&
            SessionSettings.FriendlyFireForHit.Amplification.Enabled && SessionSettings.FriendlyFireForHit.Amplification.SoftPercent == 70.25m &&
            SessionSettings.FriendlyFireForHit.Amplification.HardPercent == 125.5m &&
            SessionSettings.CombatFrequencyRevision == frequencyRevision && SessionSettings.CombatBalanceRevision == balanceRevision && Score(host) == "P(1/0/0)",
            "Amplification applies immediately without resetting scores or budget/frequency revisions");
        var amplification = SessionSettings.FriendlyFireForHit.Amplification;
        check(host.hp == nativeHp && host.customStats.OrderBy(p => p.Key).SequenceEqual(nativeStats.OrderBy(p => p.Key)),
            "Amplification settings never write native player stats or health");
        check(SessionSettings.ReadSnapshot().FriendlyFire.Amplification.Equals(amplification) &&
            SettingsActions.Execute("/one friendlyfire status").Messages.Any(m => m.Contains("soft 70.25%, hard 125.5% bonus")),
            "Snapshot and status report the exact paired bonus limits");
        check(!SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(softPercent: 200)), out _) &&
            !SettingsActions.Execute("/one friendlyfire amplification limits 200 100").Success &&
            SessionSettings.FriendlyFireForHit.Amplification.Equals(amplification), "Invalid direct and parsed commands never partially apply");
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
        var rapidSettings = new PvpRapidSettings(1.25m, 8.75m, 0.25m, 35.25m);
        check(SettingsActions.Execute("/one friendlyfire suppress-thorns on").Success, "Configure Thorns suppression for persistence");
        check(SettingsActions.Execute("/one friendlyfire artifact-rate interval 3.25").Success, "Configure inactive artifact interval for persistence");
        var perfectGuardSettings = new PvpPerfectGuardSettings(false, 3.25m);
        check(SettingsActions.Execute("/one friendlyfire dot-duration 3.33").Success, "Configure duration for persistence");
        check(SettingsActions.Execute("/one friendlyfire perfect-guard multiplier 3.25").Success,
            "Configure inactive Perfect Guard multiplier for persistence");
        check(SessionSettings.TryExecuteFriendlyFire(new FriendlyFireCommand(false, rapid: new PvpRapidChange(1.25m, 8.75m, 0.25m, 35.25m)), out _),
            "Host can configure exact rapid values for session persistence");
        HorayModAPI.StartSession();
        check(SessionSettings.FriendlyFireForHit.Enabled, "Run restart retains current combat policy");
        check(SessionSettings.FriendlyFireForHit.RefillPercent == 125.5m, "Run restart retains configured refill");
        check(SessionSettings.FriendlyFireForHit.DotIntervals.Equals(dotSettings), "Run restart retains all nine current effect intervals");
        check(SessionSettings.FriendlyFireForHit.Amplification.Equals(amplification), "Run restart retains exact amplification limits");
        check(SessionSettings.FriendlyFireForHit.Rapid.Equals(rapidSettings), "Run restart retains all configured rapid numbers");
        check(SessionSettings.FriendlyFireForHit.PerfectGuard.Equals(perfectGuardSettings), "Run restart retains inactive Perfect Guard multiplier");
        check(SessionSettings.FriendlyFireForHit.DotMaxDurationSeconds == 3.33m, "Run restart retains current duration");
        check(SessionSettings.FriendlyFireForHit.ArtifactRate.Equals(new PvpArtifactRateSettings(false, 3.25m)), "Current artifact interval follows run, connection and preset lifetime");
        check(SessionSettings.FriendlyFireForHit.SuppressThorns, "Thorns suppression follows run, reconnect and saved preset lifetime");
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
        check(SessionSettings.FriendlyFireForHit.Amplification.Equals(amplification), "Replacement connection reads current amplification limits");
        check(SessionSettings.FriendlyFireForHit.Rapid.Equals(rapidSettings), "Replacement connection uses current rapid numbers");
        check(SessionSettings.FriendlyFireForHit.PerfectGuard.Equals(perfectGuardSettings), "Replacement connection uses current Perfect Guard policy");
        check(SessionSettings.FriendlyFireForHit.DotMaxDurationSeconds == 3.33m, "Replacement connection uses current duration, not stale settings");
        check(SessionSettings.FriendlyFireForHit.ArtifactRate.Equals(new PvpArtifactRateSettings(false, 3.25m)), "Current artifact interval follows run, connection and preset lifetime");
        check(SessionSettings.FriendlyFireForHit.SuppressThorns, "Thorns suppression follows run, reconnect and saved preset lifetime");
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
        check(SessionSettings.FriendlyFireForHit.Amplification.Equals(amplification), "Saved amplification limits restore alongside every other combat setting");
        check(SessionSettings.FriendlyFireForHit.Rapid.Equals(rapidSettings), "Saved rapid numbers restore exactly after controller restart");
        check(SessionSettings.FriendlyFireForHit.PerfectGuard.Equals(perfectGuardSettings), "Saved inactive Perfect Guard multiplier restores after controller restart");
        check(SessionSettings.FriendlyFireForHit.DotMaxDurationSeconds == 3.33m, "Saved duration restores after controller restart");
        check(SessionSettings.FriendlyFireForHit.ArtifactRate.Equals(new PvpArtifactRateSettings(false, 3.25m)), "Current artifact interval follows run, connection and preset lifetime");
        check(SessionSettings.FriendlyFireForHit.SuppressThorns, "Thorns suppression follows run, reconnect and saved preset lifetime");
        check(Score(host) == "P(0/0/0)", "Saved settings never persist KDA");
        Damage(); Kill();
        check(Score(host) == "P(1/0/0)", "Restored enabled preset activates fresh accounting");
        check(SettingsActions.Execute("/one friendlyfire reset").Success, "Friendly fire reset succeeds");
        check(!SessionSettings.ReadSnapshot().ActiveSettings.Any(x => x.StartsWith("friendlyfire ")), "Reset clears toggle and scale");
        check(Score(host) == "P(0/0/0)", "Reset command clears KDA");
        check(SessionSettings.FriendlyFireForHit.RefillPercent == 70, "Full reset restores default refill");
        check(!SessionSettings.FriendlyFireForHit.HitFrequency, "Full reset restores hit-frequency default off");
        check(!SessionSettings.FriendlyFireForHit.DotIntervals.HasChanges, "Full reset restores all nine current per-effect defaults");
        check(!SessionSettings.FriendlyFireForHit.Amplification.HasChanges, "Full reset restores amplification off/50/100");
        check(!SessionSettings.FriendlyFireForHit.Rapid.HasChanges, "Full reset restores all rapid defaults");
        check(!SessionSettings.FriendlyFireForHit.SuppressThorns, "Combat reset restores native Thorns");
        check(!SessionSettings.FriendlyFireForHit.ArtifactRate.HasChanges, "Combat reset restores artifact defaults");
        check(!SessionSettings.FriendlyFireForHit.PerfectGuard.HasChanges, "Full reset restores Perfect Guard off/x2");
        check(SessionSettings.FriendlyFireForHit.DotMaxDurationSeconds == 1.2m, "Full reset restores 1.2-second duration");
        FriendlyFireFeature.Available = false;
        check(!SettingsActions.Execute("/one friendlyfire rapid recovery on").Success &&
            SettingsActions.Execute("/one friendlyfire rapid recovery off").Success &&
            SettingsActions.Execute("/one friendlyfire rapid recovery reset").Success,
            "Explicit periodic recovery reset remains available during compatibility failure");
        check(!SettingsActions.Execute("/one friendlyfire suppress-thorns on").Success &&
            SettingsActions.Execute("/one friendlyfire suppress-thorns off").Success &&
            SettingsActions.Execute("/one friendlyfire suppress-thorns reset").Success, "Thorns off/reset remain available during compatibility failure");
        check(!SettingsActions.Execute("/one friendlyfire artifact-rate on").Success &&
            !SettingsActions.Execute("/one friendlyfire artifact-rate interval 3").Success &&
            SettingsActions.Execute("/one friendlyfire artifact-rate off").Success &&
            SettingsActions.Execute("/one friendlyfire artifact-rate reset").Success, "Compatibility failure preserves off/reset recovery");
        check(!SettingsActions.Execute("/one friendlyfire dot-duration 3").Success &&
            SettingsActions.Execute("/one friendlyfire dot-duration reset").Success, "Unavailable hooks retain duration reset recovery");
        check(!SettingsActions.Execute("/one friendlyfire perfect-guard on").Success &&
            !SettingsActions.Execute("/one friendlyfire perfect-guard multiplier 3").Success &&
            SettingsActions.Execute("/one friendlyfire perfect-guard off").Success &&
            SettingsActions.Execute("/one friendlyfire perfect-guard multiplier reset").Success &&
            SettingsActions.Execute("/one friendlyfire perfect-guard reset").Success,
            "Unavailable hooks reject custom Perfect Guard settings but retain off and both reset paths");
        check(!SettingsActions.Execute("/one friendlyfire rapid window 2").Success &&
            SettingsActions.Execute("/one friendlyfire rapid window reset").Success && SettingsActions.Execute("/one friendlyfire rapid reset").Success,
            "Unavailable combat hooks retain per-field and all-rapid reset recovery");
        check(!SettingsActions.Execute("/one friendlyfire amplification on").Success &&
            !SettingsActions.Execute("/one friendlyfire amplification limits 10 20").Success &&
            SettingsActions.Execute("/one friendlyfire amplification off").Success && SettingsActions.Execute("/one friendlyfire amplification reset").Success,
            "Unavailable hooks reject amplification enable/limits but preserve off/reset recovery");
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
        check(!SettingsActions.Execute("/one friendlyfire suppress-thorns on").Success &&
            !SettingsActions.Execute("/one friendlyfire suppress-thorns off").Success, "Guests cannot mutate host Thorns policy");
        check(!SettingsActions.Execute("/one friendlyfire artifact-rate on").Success, "Guests cannot mutate artifact rate policy");
        check(!SettingsActions.Execute("/one friendlyfire dot-duration 3").Success, "Guests cannot edit authoritative duration");
        check(!SettingsActions.Execute("/one friendlyfire perfect-guard on").Success &&
            !SettingsActions.Execute("/one friendlyfire perfect-guard multiplier 3").Success, "Guests cannot change Perfect Guard host policy");
        check(!SettingsActions.Execute("/one friendlyfire on").Success, "Guests cannot enable friendly fire");
        check(!SettingsActions.Execute("/one friendlyfire balance on").Success, "Guests cannot change host balancing policy");
        check(!SettingsActions.Execute("/one friendlyfire refill 200").Success, "Guests cannot change host refill policy");
        check(!SettingsActions.Execute("/one friendlyfire dot burn 2").Success, "Guests cannot change host interval policy");
        check(!SettingsActions.Execute("/one friendlyfire hit-frequency on").Success, "Guests cannot change host frequency policy");
        check(!SettingsActions.Execute("/one friendlyfire amplification on").Success &&
            !SettingsActions.Execute("/one friendlyfire amplification limits 10 20").Success, "Guests cannot change amplification policy");
    }
}
