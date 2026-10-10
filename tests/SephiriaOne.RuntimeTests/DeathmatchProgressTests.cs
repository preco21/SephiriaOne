using SephiriaOne;

internal static class DeathmatchProgressTests
{
    // Runs with the same death/revival hooks as DeathmatchTests. Native victoryType
    // is an outcome marker written during chapter progression, before settlement.
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void At(float time) { UnityEngine.Time.unscaledTime = time; DeathmatchRuntime.Tick(); }
        foreach (int progress in new[] { 2, 6 })
        {
            var host = start(); var guest = add(90); At(0);
            DungeonManager.Instance.victoryType = progress;
            check(DeathmatchRuntime.CanStart && SettingsActions.Execute("/one deathmatch duration 10").Success &&
                SettingsActions.Execute("/one deathmatch start").Success,
                "Deathmatch controls can start during native chapter progress: " + progress);
            At(1); At(2); At(3);
            check(DeathmatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.Enabled,
                "Chapter progress does not cancel warmup or block effective friendly fire: " + progress);
            guest.PlayerAvatar.Die(1, new());
            check(DeathmatchRuntime.ProtectDeath(guest.PlayerAvatar),
                "Chapter progress preserves match game-over protection: " + progress);
            check(DeathmatchRuntime.CanResetScores && SettingsActions.Execute("/one deathmatch resetkda").Success,
                "KDA control stays usable in an active late-run match: " + progress);
            At(5); check(guest.PlayerAvatar.IsDead, "Late-run respawn retains full countdown");
            At(6); check(!guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveRpcCalls == 1,
                "Late-run respawn reaches native guest revival RPC: " + progress);
            At(12); guest.PlayerAvatar.Die(1, new()); At(13);
            check(!DeathmatchRuntime.IsRunning && !SessionSettings.FriendlyFireForHit.Enabled &&
                !guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveRpcCalls == 2,
                "Late-run expiry still disables friendly fire and revives pending players: " + progress);
        }

        // An already running match must survive markers changing between ticks,
        // including the chapter-five escape marker retained in chapter six.
        start(); var changingGuest = add(90); At(0);
        check(SettingsActions.Execute("/one deathmatch start").Success, "Start match before chapter progress");
        DungeonManager.Instance.victoryType = 2; At(3);
        changingGuest.PlayerAvatar.Die(1, new());
        DungeonManager.Instance.victoryType = 6; At(4);
        check(DeathmatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.Enabled && changingGuest.PlayerAvatar.IsDead,
            "Chapter progress updates retain the active match and pending life");
        At(6);
        check(!changingGuest.PlayerAvatar.IsDead && changingGuest.PlayerAvatar.ReviveRpcCalls == 1,
            "Chapter progress does not invalidate an already scheduled respawn");
        changingGuest.PlayerAvatar.Die(1, new());
        check(SettingsActions.Execute("/one deathmatch stop").Success && !DeathmatchRuntime.IsRunning &&
            !changingGuest.PlayerAvatar.IsDead && changingGuest.PlayerAvatar.ReviveRpcCalls == 2,
            "Stop control and immediate recovery remain usable after chapter progress");

        foreach (int outcome in new[] { 0, 2, 6 })
        {
            start(); var guest = add(90); At(0);
            DungeonManager.Instance.victoryType = outcome;
            check(SettingsActions.Execute("/one deathmatch start").Success, "Prepare match before real settlement"); At(3);
            guest.PlayerAvatar.Die(1, new()); SaveManager.CurrentRun.enableSave = false;
            check(!ReviveAllAction.CanExecute && !DeathmatchRuntime.CanResetScores &&
                !SessionSettings.FriendlyFireForHit.Enabled && !DeathmatchRuntime.ProtectDeath(guest.PlayerAvatar),
                "Real settlement immediately invalidates combat/recovery regardless of outcome: " + outcome);
            At(6);
            check(!DeathmatchRuntime.IsRunning && guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveCalls == 0 &&
                !DeathmatchRuntime.CanStart && !SettingsActions.Execute("/one deathmatch start").Success,
                "Settled run cannot respawn or restart a match: " + outcome);
        }
        start(); var leaving = add(90); At(0);
        check(SettingsActions.Execute("/one deathmatch start").Success, "Prepare match before leaving"); At(3);
        leaving.PlayerAvatar.Die(1, new()); DungeonManager.Instance.requestLeaveOnHost = true; At(6);
        check(!DeathmatchRuntime.IsRunning && leaving.PlayerAvatar.IsDead && leaving.PlayerAvatar.ReviveCalls == 0 &&
            !DeathmatchRuntime.CanStart, "Leaving the run still cancels recovery without publishing revival");
    }
}
