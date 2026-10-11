using Mirror;
using SephiriaOne;

internal static class FriendlyFireScoreResetTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        const string command = "/one friendlyfire resetkda";
        var host = start(); var guest = add(2); var helper = add(3);
        SettingsActions.Execute("/one friendlyfire on");
        SettingsActions.Execute("/one friendlyfire damage 0.1");
        SettingsActions.Execute("/one friendlyfire players all");
        void Kill()
        {
            var receipt = FriendlyFireKda.BeforeDeath(guest.PlayerAvatar, host.PlayerAvatar, FriendlyFireKda.Epoch);
            guest.PlayerAvatar.IsDead = true;
            check(FriendlyFireKda.CompleteDeath(guest.PlayerAvatar, receipt), "Score casual friendly-fire fixture death");
            guest.PlayerAvatar.IsDead = false;
        }
        FriendlyFireKda.Damage(helper.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch); Kill();
        var retained = FriendlyFireKda.For(host.PlayerAvatar);
        long revision = SessionSettings.ReadSnapshot().Revision;
        check(SessionSettings.CanResetFriendlyFireScores, "Combat and Deathmatch button gate permits casual score reset");
        check(SettingsActions.Execute(command).Success, "Reset K/D/A is available in casual friendly fire without a deathmatch");
        check(FriendlyFireKda.For(host.PlayerAvatar).Kills == 0 && FriendlyFireKda.For(guest.PlayerAvatar).Deaths == 0 &&
            FriendlyFireKda.For(helper.PlayerAvatar).Assists == 0 && ReferenceEquals(retained, FriendlyFireKda.For(host.PlayerAvatar)),
            "Casual reset clears all scores without replacing scoreboard identities");
        check(!CombatMatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.Enabled &&
            SessionSettings.FriendlyFireForHit.DamagePercent == 0.1m && FriendlyFireParticipants.SelectedCount == 3 &&
            SessionSettings.ReadSnapshot().Revision == revision && guest.PlayerAvatar.ReviveCalls == 0,
            "Score reset preserves master, damage, selection, session intent and native lives");
        FriendlyFireKda.Damage(helper.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch);
        var stale = FriendlyFireKda.BeforeDeath(guest.PlayerAvatar, host.PlayerAvatar, FriendlyFireKda.Epoch);
        check(SettingsActions.Execute(command).Success, "Repeated casual reset succeeds");
        guest.PlayerAvatar.IsDead = true;
        check(!FriendlyFireKda.CompleteDeath(guest.PlayerAvatar, stale), "An in-flight death cannot restore pre-reset totals");
        guest.PlayerAvatar.IsDead = false; Kill();
        check(FriendlyFireKda.For(helper.PlayerAvatar).Assists == 0 && FriendlyFireKda.For(host.PlayerAvatar).Kills == 1,
            "New kills count from zero and old damage cannot grant an assist");
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "fixture");
        guest.connectionToClient.isReady = false;
        check(SessionSettings.CanResetFriendlyFireScores && SettingsActions.Execute(command).Success,
            "Button and score-only reset ignore unrelated stat fault and guest loading");
        foreach (string invalid in new[] { command + " all", command + " 1" })
        {
            retained.Kills = 7;
            check(!SettingsActions.Execute(invalid).Success && retained.Kills == 7, "Malformed casual reset cannot change totals");
        }
        NetworkServer.active = false;
        check(!SessionSettings.CanResetFriendlyFireScores && !SettingsActions.Execute(command).Success && retained.Kills == 7,
            "Guest button and command cannot reset host scores");
        NetworkServer.active = true; SaveManager.CurrentRun.enableSave = false;
        check(!SettingsActions.Execute(command).Success && retained.Kills == 7, "Closed run cannot reset scores");
        start();
    }
}
