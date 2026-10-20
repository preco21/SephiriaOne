using SephiriaOne;

internal static class TournamentMessageTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        PlayerAvatar[] New(int count = 2)
        {
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) players.Add(add(i).PlayerAvatar);
            for (int i = 0; i < players.Count; i++) players[i].playerNameSource = "Fighter" + i;
            At(0);
            SettingsActions.Execute("/one tournament duration 10");
            check(SettingsActions.Execute("/one tournament start").Success, "Prepare tournament bubble lifecycle");
            At(3);
            return players.ToArray();
        }
        string Last(PlayerAvatar player) => DungeonManager.Instance.ChatMessages
            .Last(x => x.Player == player && !string.IsNullOrWhiteSpace(x.Message)).Message;
        const string End = "[Tournament] Match ended.";

        var players = New(4);
        var pair = players.Where(a => players.Any(b => CombatMatchRuntime.AllowsPair(a, b))).ToArray();
        var chat = DungeonManager.Instance.ChatMessages;
        int before = chat.Count;
        At(3.1f); At(3.5f); At(3.99f);
        check(chat.Count == before, "Initial start bubble is not duplicated within its first second");
        At(4);
        foreach (var player in pair)
            check(Last(player) == $"[Tournament] Opponent: \"{pair.Single(p => p != player).playerNameSource}\" | 9s left",
                "Active fighters see their own opponent and live remaining time");
        check(chat.Count == before + 2 && players.Except(pair).All(p => !chat.Any(x => x.Player == p)),
            "Only two fighters receive active status, never spectators");
        At(4.1f); At(4.9f);
        check(chat.Count == before + 2, "Multiple updates within the same second do not resend status");
        At(7.2f);
        check(chat.Count == before + 4 && pair.All(p => Last(p).EndsWith("6s left")),
            "Delayed frames send only the current time, without replaying missed seconds");
        pair[0].IsDead = true; CombatMatchRuntime.Died(pair[0]);
        check(Last(pair[0]) == "Respawn in: 3s" && Last(pair[1]) == End,
            "Death immediately replaces both live statuses before sending the respawn bubble");
        int ended = chat.Count(x => x.Message == End);
        At(7.3f);
        check(Last(pair[0]) == "Respawn in: 3s" && chat.Count(x => x.Message == End) == ended,
            "Round settlement does not erase the dead fighter's replacement respawn message");
        CombatMatchRuntime.Stop(true, false);

        players = New();
        At(4); At(13);
        check(players.All(p => DungeonManager.Instance.ChatMessages.Any(x => x.Player == p && x.Message == End) &&
            !Last(p).Contains("s left")), "Timeout replaces both timers before a possible next-pair countdown");
        At(13.2f); At(16.2f); At(17.2f);
        check(players.All(p => Last(p).EndsWith("9s left")), "A rematch starts with a fresh status timer");
        SettingsActions.Execute("/one tournament stop");
        check(players.All(p => Last(p) == End), "Stop replaces both active bubbles through nonempty native messages");
        int stopped = DungeonManager.Instance.ChatMessages.Count;
        At(25);
        check(DungeonManager.Instance.ChatMessages.Count == stopped, "Stopped tournaments cannot refresh abandoned messages");

        players = New(3);
        pair = players.Where(a => players.Any(b => CombatMatchRuntime.AllowsPair(a, b))).ToArray();
        At(4);
        var departed = pair[0];
        FriendlyFireParticipants.Retire(departed.spawner);
        PlayerSpawner.MultiplayerList.Remove(departed.spawner);
        var replacement = add(departed.netId).PlayerAvatar;
        before = DungeonManager.Instance.ChatMessages.Count;
        At(4.2f);
        var notices = DungeonManager.Instance.ChatMessages.Skip(before).ToArray();
        check(notices.All(x => x.Player != departed && x.Player != replacement) &&
            notices.Any(x => x.Player == pair[1] && x.Message == End),
            "Withdrawal clears the connected opponent without messaging an old or reused connection");
        At(7.2f); At(8.2f);
        pair = players.Where(p => p != departed).ToArray();
        foreach (var player in pair)
            check(Last(player) == $"[Tournament] Opponent: \"{pair.Single(p => p != player).playerNameSource}\" | 9s left",
                "New pairing replaces the previous opponent identity");
        CombatMatchRuntime.Stop(true, false);

        players = New();
        DungeonManager.Instance.ChatCallback = () =>
        {
            DungeonManager.Instance.ChatCallback = null;
            CombatMatchRuntime.Stop(true, false);
        };
        At(4);
        check(!CombatMatchRuntime.IsRunning && players.All(p => Last(p) == End),
            "Reentrant stop while sending first status cannot send stale status to the second fighter");

        players = New();
        DungeonManager.Instance.ChatCallback = () =>
        {
            DungeonManager.Instance.ChatCallback = null;
            CombatMatchRuntime.Stop(true, false);
        };
        At(13);
        check(!CombatMatchRuntime.IsRunning && players.All(p => Last(p) == End),
            "Stop during first terminal notice still clears the second fighter through nested cleanup");

        players = New();
        DungeonManager.Instance.ChatCallback = () =>
        {
            DungeonManager.Instance.ChatCallback = null;
            players[0].IsDead = true;
            CombatMatchRuntime.Died(players[0]);
        };
        At(4);
        check(Last(players[0]) == "Respawn in: 3s" && Last(players[1]) == End,
            "Death during status delivery preserves the new respawn bubble and invalidates remaining status sends");
        CombatMatchRuntime.Stop(true, false);

        players = New();
        DungeonManager.Instance.ChatCallback = () =>
        {
            DungeonManager.Instance.ChatCallback = null;
            CombatMatchRuntime.Stop(true, false);
        };
        players[0].IsDead = true; CombatMatchRuntime.Died(players[0]);
        check(!CombatMatchRuntime.IsRunning && !players[0].IsDead &&
            Last(players[0]) == "Respawned!" && Last(players[1]) == End,
            "A stop during death-message retirement cannot schedule a stale respawn after cleanup");

        players = New();
        DungeonManager.Instance.ChatCallback = () =>
        {
            DungeonManager.Instance.ChatCallback = null;
            players[0].Revive(100);
        };
        players[0].IsDead = true; CombatMatchRuntime.Died(players[0]);
        check(!players[0].IsDead && Last(players[0]) == End &&
            !DungeonManager.Instance.ChatMessages.Any(x => x.Message.StartsWith("Respawn in:")),
            "Manual revival during terminal notice does not announce a respawn for an already living fighter");
        CombatMatchRuntime.Stop(true, false);

        // The stop can be nested inside native Die before recovery is allowed.
        players = New();
        bool entered = CombatMatchRuntime.EnterDeath(players[0]);
        players[0].IsDead = true;
        CombatMatchRuntime.Stop(true, false);
        check(CombatMatchRuntime.RecoveryPending && players.All(p => Last(p) == End),
            "Stopping clears statuses immediately even when native-death recovery is deferred");
        CombatMatchRuntime.LeaveDeath(players[0], entered);

        start(); var guest = add(2).PlayerAvatar; At(0);
        SettingsActions.Execute("/one tournament start");
        guest.spawner.connectionToClient.isReady = false;
        At(1);
        check(DungeonManager.Instance.ChatMessages.Any(x => x.Message == End),
            "Interrupted preparation replaces the previous countdown instead of leaving stale fight instructions");
        CombatMatchRuntime.Stop(true, false);
        start();
    }
}
