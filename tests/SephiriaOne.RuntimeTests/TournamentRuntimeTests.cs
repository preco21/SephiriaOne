using HarmonyLib;
using SephiriaOne;

internal static class TournamentRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        ReviveAllHooks.Install();
        var harmony = new Harmony("Tournament.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"));
        float time = 0;
        void At(float value) { time = value; UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        void Advance(float seconds) => At(time + seconds);
        PlayerAvatar[] New(int count)
        {
            var all = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) all.Add(add(i).PlayerAvatar);
            for (int i = 0; i < all.Count; i++) all[i].playerNameSource = "P" + (i + 1);
            At(0); return all.ToArray();
        }
        PlayerAvatar[] Pair(PlayerAvatar[] all)
        {
            var pairs = all.SelectMany(a => all.Where(b => CombatMatchRuntime.AllowsPair(a, b)).Select(b => new[] { a, b })).ToArray();
            check(pairs.Length == 2, "Exactly one symmetric player pair may fight");
            return pairs[0];
        }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        {
            var damage = new DamageInstance();
            FriendlyFireRuntime.Attribute(killer, victim, damage);
            victim.Die(0, damage);
        }

        var players = New(1);
        check(!SettingsActions.Execute("/one tournament start").Success, "Tournament requires two players");
        players = New(4);
        check(SettingsActions.Execute("/one tournament start").Success, "Four-player tournament starts");
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Tournament warmup keeps friendly fire disabled");
        At(1); At(2); At(3);
        var chat = DungeonManager.Instance.ChatMessages;
        var pair = Pair(players);
        check(SettingsActions.Execute("/one deathmatch status").Messages.Any(x => x.Contains("Deathmatch: off.")),
            "Deathmatch status cannot claim the shared tournament phase");
        foreach (var player in pair)
        {
            string opponent = pair.Single(p => p != player).playerNameSource;
            check(chat.Where(x => x.Player == player).Select(x => x.Message).SequenceEqual(new[] {
                $"[Tournament] \"{opponent}\" is your opponent! Fight in: 3...",
                $"[Tournament] \"{opponent}\" is your opponent! Fight in: 2...",
                $"[Tournament] \"{opponent}\" is your opponent! Fight in: 1...",
                $"[Tournament] Match started: Kill \"{opponent}\"! (30 secs)" }),
                "Each fighter receives the opponent and countdown via the native avatar bubble");
        }
        check(!chat.Any(x => x.Player != null && !pair.Contains(x.Player)), "Waiting entrants receive no opponent bubble");
        var firstWinner = pair[0]; var firstLoser = pair[1];
        int shownBeforeKill = FriendlyFireDeathVisuals.Shown;
        Kill(firstWinner, firstLoser);
        check(NativePlayerChat.Messages.Last() == (firstLoser,
            $"Killed by ‘{firstWinner.playerNameSource}’ (Matchup: Me 0–1 Opponent)"),
            "Tournament uses the shared private victim notice");
        check(FriendlyFireDeathVisuals.Shown == shownBeforeKill + 1,
            "Tournament down uses the shared one-shot death visual before advancing the round");
        check(FriendlyFireKda.For(firstWinner).Kills == 1 && FriendlyFireKda.For(firstLoser).Deaths == 1,
            "Native death finalizer credits shared PvP KDA before resolving the round");
        Advance(0.2f); Advance(0.2f);
        check(!SessionSettings.FriendlyFireForHit.Enabled && FriendlyFireKda.For(firstWinner).Kills == 1,
            "Next countdown suspends damage without resetting tournament scores");
        check(FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(firstWinner), FriendlyFireKda.For(firstLoser)) == 1,
            "Tournament round transition preserves matchup history while invalidating pending hits");
        check(SettingsActions.Execute("/one friendlyfire damage 0.1").Success && FriendlyFireKda.For(firstWinner).Kills == 1,
            "Damage scale edits between rounds preserve score objects");
        Advance(3);
        pair = Pair(players);
        check(!pair.Contains(firstWinner) && !pair.Contains(firstLoser), "Unplayed entrants fight before the previous winner");
        check(!firstLoser.IsDead && firstLoser.ReviveRpcCalls == 1, "Eliminated player uses shared three-second native recovery");
        var secondWinner = pair[0]; Kill(secondWinner, pair[1]);
        Advance(0.2f); Advance(0.2f); Advance(3);
        pair = Pair(players);
        check(pair.Contains(firstWinner) && pair.Contains(secondWinner), "Previous winners enter the final stage");
        check(FriendlyFireKda.For(firstWinner).Kills == 1 && FriendlyFireKda.For(secondWinner).Kills == 1,
            "Earlier wins persist through both round transitions");
        Kill(firstWinner, secondWinner); Advance(0.2f); Advance(0.2f);
        check(!CombatMatchRuntime.IsRunning && !SessionSettings.FriendlyFireForHit.Enabled && players.All(p => !p.IsDead),
            "Final ends tournament, disables friendly fire and immediately recovers pending players");
        check(chat.Any(x => x.Message == "1. " + firstWinner.playerNameSource + "(2/0/0)"),
            "Tournament top-five uses accumulated KDA before shutdown resets scores");
        check(FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(firstWinner), FriendlyFireKda.For(firstLoser)) == 0,
            "Tournament completion clears matchup records");

        players = New(2); SettingsActions.Execute("/one tournament duration 10");
        SettingsActions.Execute("/one tournament start"); At(3);
        SettingsActions.Execute("/one tournament duration 60");
        check(CombatMatchRuntime.DescribeTournament().Contains("Round duration: 10s."),
            "Active status displays the captured round duration, not edits for the next tournament");
        At(13); Advance(0.2f); Advance(3);
        check(CombatMatchRuntime.IsTournament, "First draw schedules one rematch without eliminating either player");
        Advance(10); Advance(0.2f);
        check(!CombatMatchRuntime.IsRunning, "Second draw exhausts the pair; edited duration did not affect current tournament");
        check(DungeonManager.Instance.ChatMessages.Count(x => x.Message.Contains("Match started:")) == 4,
            "An exhausted two-player draw never schedules a third match");

        foreach (bool beforeDeadline in new[] { false, true })
        {
            players = New(2); SettingsActions.Execute("/one tournament duration 10");
            SettingsActions.Execute("/one tournament start"); At(3);
            UnityEngine.Time.unscaledTime = beforeDeadline ? 12 : 14;
            Kill(players[0], players[1]); At(14);
            check(DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("wins the round")) == beforeDeadline,
                "Death admission time, not delayed host tick, determines whether the round was won");
            CombatMatchRuntime.Stop(true, false);
        }

        players = New(2); SettingsActions.Execute("/one tournament start"); At(3);
        Kill(players[0], players[1]);
        check(SettingsActions.Execute("/one reviveall").Success && !players[1].IsDead, "Manual recovery may precede round settlement");
        Advance(0.2f); Advance(0.2f);
        check(!CombatMatchRuntime.IsRunning && DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("wins the round")),
            "Manual revival cannot erase a recorded loss");

        players = New(2); SettingsActions.Execute("/one tournament start"); At(3);
        Kill(players[0], players[1]); Kill(players[1], players[0]); Advance(0.2f);
        check(CombatMatchRuntime.IsTournament && DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("Round unresolved")),
            "Both deaths in one update resolve as an unresolved round");
        Advance(3); Advance(3);
        check(players.All(p => !p.IsDead) && Pair(players).Length == 2, "Double-death recovery starts a fresh rematch countdown");
        check(FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(players[0]), FriendlyFireKda.For(players[1])) == 1 &&
            FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(players[1]), FriendlyFireKda.For(players[0])) == 1,
            "Unresolved double-kill rematch preserves both directed records");
        check(SettingsActions.Execute("/one tournament resetkda").Success &&
            FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(players[0]), FriendlyFireKda.For(players[1])) == 0,
            "Tournament score reset clears matchups during a rematch");
        CombatMatchRuntime.Stop(true, false);

        players = New(2); SettingsActions.Execute("/one tournament start"); At(1);
        players[1].spawner.connectionToClient.isReady = false; At(4);
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Floor readiness loss prevents a countdown from arming damage");
        players[1].spawner.connectionToClient.isReady = true; At(5); At(7);
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Re-ready fighters receive the full fresh three-second countdown");
        At(8); check(Pair(players).Length == 2, "Countdown preserves the same reserved pair after floor loading");
        CombatMatchRuntime.Stop(true, false);

        players = New(2); players[1].IsDead = true;
        SettingsActions.Execute("/one friendlyfire players all"); SettingsActions.Execute("/one tournament start");
        players[1].ReviveCallback = () => FriendlyFireParticipants.Retire(players[0].spawner);
        At(3);
        check(!CombatMatchRuntime.IsRunning && players[1].ReviveRpcCalls == 1 && !players[1].IsDead,
            "Selected disconnect inside recovery finishes the admitted revival then discards cleared bracket state");

        players = New(2); SettingsActions.Execute("/one tournament start"); At(1);
        Kill(players[0], players[1]); At(4); At(7);
        check(CombatMatchRuntime.IsTournament && !players[1].IsDead && Pair(players).Length == 2,
            "Countdown deaths recover without elimination or using a draw attempt");
        CombatMatchRuntime.Stop(true, false);

        players = New(3); SettingsActions.Execute("/one tournament start"); At(3);
        var late = add(90).PlayerAvatar; Advance(0.2f);
        check(players.All(p => !CombatMatchRuntime.AllowsPair(late, p)), "Late joins cannot enter a captured bracket");
        pair = Pair(players); var departed = pair[0];
        FriendlyFireParticipants.Retire(departed.spawner);
        PlayerSpawner.MultiplayerList.Remove(departed.spawner);
        var replacement = add(departed.netId).PlayerAvatar;
        Advance(0.2f); Advance(0.2f); Advance(3);
        check(CombatMatchRuntime.IsTournament && players.All(p => !CombatMatchRuntime.AllowsPair(replacement, p)),
            "Default-selection disconnect withdraws exact lifetime; rejoin does not inherit bracket entry");
        pair = Pair(players.Where(p => p != departed).ToArray());
        check(!pair.Contains(departed), "Remaining live entrants can finish after a withdrawal");
        CombatMatchRuntime.Stop(true, false);

        foreach (string change in new[] { "players reset", "players all", "player 1 off" })
        {
            players = New(2); SettingsActions.Execute("/one tournament start"); At(3);
            Kill(players[0], players[1]);
            check(SettingsActions.Execute("/one friendlyfire " + change).Success && !CombatMatchRuntime.IsRunning && !players[1].IsDead,
                "Selection action stops tournament and recovers pending fighter: " + change);
            SettingsActions.Execute("/one friendlyfire players reset");
            check(SettingsActions.Execute("/one tournament start").Success, "A stopped bracket restarts from initial state");
            CombatMatchRuntime.Stop(true, false);
        }
        players = New(3); SettingsActions.Execute("/one friendlyfire players all");
        SettingsActions.Execute("/one tournament start"); At(3);
        FriendlyFireParticipants.Retire(players[2].spawner);
        check(!CombatMatchRuntime.IsRunning, "Pruning a selected disconnect immediately resets tournament state");

        foreach (string toggle in new[] { "on", "off", "reset" })
        {
            players = New(2); SettingsActions.Execute("/one tournament start"); At(3);
            players[1].DieCallback = _ => SettingsActions.Execute("/one friendlyfire " + toggle);
            Kill(players[0], players[1]);
            check(!CombatMatchRuntime.IsRunning && players[1].Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }),
                "Stop inside lethal callback defers recovery until RpcDie: " + toggle);
            check(SessionSettings.FriendlyFireForHit.Enabled == (toggle == "on"), "Manual toggle wins after tournament cleanup");
        }

        players = New(3); SettingsActions.Execute("/one friendlyfire player 1 on"); SettingsActions.Execute("/one friendlyfire player 2 on");
        players[2].IsDead = true;
        SettingsActions.Execute("/one tournament start"); At(3);
        check(!CombatMatchRuntime.ProtectDeath(players[2]), "Excluded dead bystander gets no tournament settlement protection");
        Kill(players[0], players[1]); Advance(0.2f); Advance(0.2f);
        check(players[2].IsDead && players[2].ReviveCalls == 0, "Tournament cleanup leaves excluded bystander untouched");

        foreach (string transition in new[] { "restart", "save", "dungeon", "authority", "settlement" })
        {
            players = New(2); SettingsActions.Execute("/one tournament start"); At(3);
            Kill(players[0], players[1]);
            switch (transition)
            {
                case "restart": HorayModAPI.StartSession(); break;
                case "save": SaveManager.CurrentRun = new(); break;
                case "dungeon": DungeonManager.Instance = new(); break;
                case "authority": Mirror.NetworkServer.active = false; break;
                case "settlement": SaveManager.CurrentRun.enableSave = false; break;
            }
            Advance(4);
            check(!CombatMatchRuntime.IsRunning && players[1].ReviveCalls == 0, "Old tournament recovery never crosses " + transition);
        }
        players = New(5); SettingsActions.Execute("/one tournament duration 3600");
        SettingsActions.Execute("/one tournament start"); At(3);
        for (int i = 0; i < 50; i++) Advance(0.11f);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Advance(0.11f);
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Active five-player tournament ticks allocate zero bytes after warmup");
        CombatMatchRuntime.Stop(true, false);
        harmony.UnpatchAll("Tournament.integration.tests"); ReviveAllHooks.Uninstall(); start();
    }
}
