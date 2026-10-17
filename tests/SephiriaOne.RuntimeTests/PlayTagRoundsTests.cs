using HarmonyLib;
using SephiriaOne;

internal static class PlayTagRoundsTests
{
    // Called while PlayTagRuntimeTests' production death/revive hooks are installed.
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        PlayTagBomb Bomb() => (PlayTagBomb)AccessTools.Field(typeof(CombatMatchRuntime), "tagBomb").GetValue(null);
        PlayerAvatar[] New(int rounds = 2)
        {
            var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar };
            for (int i = 0; i < players.Length; i++) players[i].playerNameSource = "R" + (i + 1);
            PlayTagExplosion.Shown.Clear(); At(0);
            check(SettingsActions.Execute("/one tag rounds " + rounds).Success && SettingsActions.Execute("/one tag duration 3").Success &&
                CombatMatchRuntime.StartPlayTag(out _), "Start configured multi-round Tag");
            At(3); return players;
        }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        {
            var damage = new DamageInstance(); FriendlyFireRuntime.Attribute(killer, victim, damage); victim.Die(0, damage);
        }
        var all = New(); var bomb = Bomb(); var carrier = all[bomb.Carrier];
        var others = all.Where(p => p != carrier).ToArray();
        Kill(others[0], others[1]); At(4);
        var score = FriendlyFireKda.For(others[0]);
        check(score.Kills == 1, "Prepare actual KDA credit before next round");
        long firstToken = CombatMatchRuntime.MatchToken;
        At(6);
        check(CombatMatchRuntime.IsPlayTag && Bomb() == null && carrier.IsDead && !SessionSettings.FriendlyFireForHit.Enabled,
            "First explosion completes a round, retains match, and blocks combat during recovery");
        check(PlayTagExplosion.Shown.Count == 1 && PlayTagExplosion.Owned &&
            !DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("Top 5")), "Intermediate round preserves FX and does not publish final standings");
        check(CombatMatchRuntime.MatchToken != firstToken && !CombatMatchRuntime.IsActiveChaser(carrier),
            "Round boundary invalidates old combat callbacks and chaser bonuses");
        At(7);
        check(all.All(p => !p.IsDead) && Bomb() == null && CombatMatchRuntime.DescribePlayTag().Contains("2/2"),
            "All pending participants recover before round two countdown");
        check(ReferenceEquals(score, FriendlyFireKda.For(others[0])) && score.Kills == 1,
            "Round recovery preserves accumulated score identity and totals");
        At(9); check(Bomb() == null && !SessionSettings.FriendlyFireForHit.Enabled, "Round two keeps its full countdown");
        At(10);
        check(Bomb() != null && !ReferenceEquals(Bomb(), bomb) && Bomb().Fuse == 3 && Bomb().PreviousCarrier == -1 &&
            SessionSettings.FriendlyFireForHit.Enabled && FriendlyFireKda.For(others[0]).Kills == 1,
            "New random carrier gets fresh bomb state while KDA survives countdown activation");
        var newCarrier = all[Bomb().Carrier]; var target = all.First(p => p != newCarrier);
        target.IsDead = true;
        CombatMatchRuntime.ConfirmedKill(FriendlyFireParticipants.Capture(newCarrier), FriendlyFireParticipants.Capture(target), firstToken);
        check(!Bomb().AwaitingRespawn && all[Bomb().Carrier] == newCarrier, "Previous round token cannot pass the new bomb"); target.IsDead = false;
        At(13);
        check(!CombatMatchRuntime.IsRunning && PlayTagExplosion.Shown.Count == 2 && all.All(p => !p.IsDead) &&
            DungeonManager.Instance.ChatMessages.Count(x => x.Message.Contains("Top 5")) == 1,
            "Configured final explosion ends match, recovers players and publishes one scoreboard");
        check(DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains(others[0].playerNameSource + "(1/0/0)")),
            "Final leaderboard includes earlier-round kills");

        all = New(); carrier = all[Bomb().Carrier];
        check(SettingsActions.Execute("/one tag rounds 1").Success && SettingsActions.Execute("/one tag duration 30").Success,
            "Live settings edits are accepted for the next match");
        At(6); At(7); At(10);
        check(CombatMatchRuntime.IsPlayTag && Bomb().Fuse == 3 && CombatMatchRuntime.DescribePlayTag().Contains("2/2"),
            "Current match keeps both captured fuse and total rounds");
        At(13); check(!CombatMatchRuntime.IsRunning, "Captured round limit ends unchanged");
        At(20); check(CombatMatchRuntime.StartPlayTag(out _), "Restart with updated settings"); At(23);
        check(Bomb().Fuse == 30 && CombatMatchRuntime.DescribePlayTag().Contains("1/1"), "New match captures new settings and resets progress");
        At(53); check(!CombatMatchRuntime.IsRunning, "Single-round behavior remains immediate final recovery");

        all = New(); carrier = all[Bomb().Carrier]; At(6);
        carrier.spawner.connectionToClient.isReady = false; At(30);
        check(CombatMatchRuntime.IsPlayTag && carrier.IsDead && Bomb() == null, "Loading entrant delays recovery and the next countdown");
        carrier.spawner.connectionToClient.isReady = true; At(31);
        all[0].spawner.connectionToClient.isReady = false; At(32);
        all[0].spawner.connectionToClient.isReady = true; At(40); At(42);
        check(Bomb() == null, "Loading during round warmup restarts a full countdown without skipping a round");
        At(43); check(Bomb() != null && CombatMatchRuntime.DescribePlayTag().Contains("2/2"), "Warmup resumes the same reserved round");
        CombatMatchRuntime.Stop(true, false);

        foreach (string action in new[] { "stop", "toggle", "selection", "reset", "save", "authority", "revive-error", "manual-revive-error" })
        {
            all = New(); carrier = all[Bomb().Carrier]; At(6);
            switch (action)
            {
                case "stop": SettingsActions.Execute("/one tag stop"); break;
                case "toggle": SettingsActions.Execute("/one friendlyfire off"); break;
                case "selection": SettingsActions.Execute("/one friendlyfire players all"); break;
                case "reset": check(SettingsActions.Execute("/one friendlyfire reset").Success, "Reset friendly fire between rounds"); break;
                case "save": SaveManager.CurrentRun = new(); break;
                case "authority": Mirror.NetworkServer.active = false; break;
                case "revive-error": carrier.ReviveCallback = () => throw new InvalidOperationException("round recovery failed"); break;
                case "manual-revive-error":
                    carrier.ReviveCallback = () => throw new InvalidOperationException("manual round recovery failed");
                    try { carrier.Revive(100); } catch (InvalidOperationException) { } break;
            }
            At(7); At(50);
            check(!CombatMatchRuntime.IsRunning && Bomb() == null && PlayTagExplosion.Shown.Count == 1,
                "No later round survives interruption or recovery failure: " + action);
        }

        all = New(); At(6);
        var retired = all[1].spawner; FriendlyFireParticipants.Retire(retired); PlayerSpawner.MultiplayerList.Remove(retired);
        var replacement = add(all[1].netId).PlayerAvatar;
        At(7); At(10);
        check(!CombatMatchRuntime.IsRunning || !CombatMatchRuntime.AllowsPair(replacement, all[0]) &&
            (Bomb() == null || !ReferenceEquals(all[Bomb().Carrier], replacement)), "Rejoining connection cannot inherit frozen Play Tag participation");
        CombatMatchRuntime.Stop(true, false);
    }
}
