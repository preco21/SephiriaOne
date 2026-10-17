using HarmonyLib;
using SephiriaOne;

internal static class PlayTagRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        // Keep the first regression executable before the new runtime API exists.
        var begin = AccessTools.Method(typeof(CombatMatchRuntime), "StartPlayTag");
        check(begin != null, "Play Tag exposes its own shared-runtime start action");
        ReviveAllHooks.Install();
        var harmony = new Harmony("PlayTag.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        float time = 0;
        void At(float value) { time = value; UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        PlayerAvatar[] New(int count)
        {
            var all = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) all.Add(add(i).PlayerAvatar);
            for (int i = 0; i < all.Count; i++) all[i].playerNameSource = "P" + (i + 1);
            PlayTagExplosion.Shown.Clear(); NativePlayerChat.Messages.Clear();
            At(0); return all.ToArray();
        }
        bool Begin() => (bool)begin.Invoke(null, new object[] { null });
        PlayTagBomb Bomb() => (PlayTagBomb)AccessTools.Field(typeof(CombatMatchRuntime), "tagBomb").GetValue(null);
        PlayerAvatar Carrier(PlayerAvatar[] all) => all[Bomb().Carrier];
        void BeginAtThree(int seconds = 30)
        {
            check(SessionSettings.TrySetPlayTagDuration(seconds, out _) && Begin(), "Start Tag with captured initial fuse");
            At(time + 3);
        }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        {
            var damage = new DamageInstance();
            FriendlyFireRuntime.Attribute(killer, victim, damage);
            victim.Die(0, damage);
        }
        var players = New(1);
        check(!Begin(), "Play Tag requires at least two captured participants");
        players = New(3);
        check(Begin(), "Play Tag starts using the shared runtime");
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Tag countdown blocks friendly fire");
        At(3);
        check(players.All(a => players.All(b => a == b || CombatMatchRuntime.AllowsPair(a, b))),
            "Every enrolled pair can fight in active Tag");
        var guardBomb = Bomb(); long guardToken = CombatMatchRuntime.MatchToken, guardEpoch = FriendlyFireKda.Epoch;
        check(SettingsActions.Execute("/one friendlyfire hit-frequency on").Success && SessionSettings.FriendlyFireForHit.HitFrequency &&
            CombatMatchRuntime.IsRunning && ReferenceEquals(guardBomb, Bomb()) && CombatMatchRuntime.MatchToken == guardToken && FriendlyFireKda.Epoch == guardEpoch,
            "Live hit-frequency toggle preserves Tag carrier, fuse and scores");
        check(SettingsActions.Execute("/one friendlyfire guard-mp 15").Success && SessionSettings.FriendlyFireForHit.GuardMpPercent == 15 &&
            CombatMatchRuntime.IsRunning && ReferenceEquals(guardBomb, Bomb()) && CombatMatchRuntime.MatchToken == guardToken &&
            FriendlyFireKda.Epoch == guardEpoch, "Live guard edit preserves Tag bomb, scores, respawns and match identity");
        var carrier = DungeonManager.Instance.ChatMessages.Last(x => x.Player != null && x.Message.Contains("Pass the bomb")).Player;
        check(SettingsActions.Execute("/one friendlyfire dot plasma-burn 1.75").Success &&
            SessionSettings.FriendlyFireForHit.DotIntervals.Seconds(PvpDotEffect.PlasmaBurn) == 1.75m &&
            CombatMatchRuntime.IsRunning && ReferenceEquals(guardBomb, Bomb()) && CombatMatchRuntime.MatchToken == guardToken &&
            FriendlyFireKda.Epoch == guardEpoch, "DoT edit preserves Tag bomb/fuse, score epoch and match identity");
        var recipient = players.First(p => p != carrier);
        long epoch = FriendlyFireKda.Epoch;
        Kill(carrier, recipient);
        check(FriendlyFireKda.Epoch == epoch && recipient.IsDead, "A pass preserves combat receipts and the ordinary death");
        At(3.5f); check(recipient.IsDead, "Tag waits the full one-second respawn delay");
        At(4); check(!recipient.IsDead, "Tag revives after one second");
        check(DungeonManager.Instance.ChatMessages.Last(x => x.Player == recipient).Message.Contains("24s"),
            "Recipient receives the reduced fuse after revival");
        At(27.9f); check(CombatMatchRuntime.IsRunning, "Recipient fuse begins after revival");
        At(28); check(!CombatMatchRuntime.IsRunning && players.All(p => !p.IsDead),
            "One explosion ends Tag and recovers every enrolled participant");
        check(!SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.GuardMpPercent == 15,
            "Ended Tag retains only guard preference, not active PvP scaling");
        check(PlayTagExplosion.Shown.SequenceEqual(new[] { recipient }), "Explosion presentation is emitted exactly once");
        At(29); check(PlayTagExplosion.Shown.Count == 1, "Later ticks cannot replay the consumed explosion");
        check(PlayTagExplosion.Owned, "Natural match end preserves the animation until its own completion");
        check(CombatMatchRuntime.Start(out _) && !PlayTagExplosion.Owned,
            "Starting any new match clears a prior Tag presentation");
        CombatMatchRuntime.Stop(true, false);

        players = New(3); BeginAtThree(3); carrier = Carrier(players);
        int visuals = FriendlyFireDeathVisuals.Shown, audio = FriendlyFireDeathAudio.Shown;
        At(6);
        check(carrier.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }) && PlayTagExplosion.Shown.Single() == carrier,
            "Living timeout uses one native death followed by shared recovery");
        check(FriendlyFireDeathVisuals.Shown == visuals && FriendlyFireDeathAudio.Shown == audio,
            "Special bomb death suppresses default red death presentation and observer audio");
        check(DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("before any pass")) &&
            !DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("Friendly fire:")),
            "Initial expiry announces no fabricated passer or ordinary kill");
        check(DungeonManager.Instance.ChatMessages.Last(x => x.Player == carrier).Message == "Respawned!",
            "Final recovery replaces stale carrier instructions");

        players = New(3); BeginAtThree(3); carrier = Carrier(players);
        var other = players.First(p => p != carrier);
        At(5); Kill(other, carrier);
        check(Bomb().Carrier == Array.IndexOf(players, carrier) && Bomb().Remaining(5) == 1,
            "Noncarrier kill does not transfer or restart the armed fuse");
        At(6);
        check(!CombatMatchRuntime.IsRunning && carrier.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }),
            "Dead carrier expiry wins over same-frame respawn without a second death");
        check(PlayTagExplosion.Shown.Count == 1, "Already-dead carrier still consumes and presents one outcome");

        players = New(2); BeginAtThree(3); carrier = Carrier(players); other = players.First(p => p != carrier);
        At(5);
        carrier.DieCallback = _ =>
        {
            other.Die(0, new DamageInstance());
            check(CombatMatchRuntime.ProtectDeath(carrier) && CombatMatchRuntime.ProtectDeath(other),
                "All-dead nested native callbacks retain shared settlement protection");
        };
        carrier.Die(0, new DamageInstance());
        At(6);
        check(!CombatMatchRuntime.IsRunning && players.All(p => !p.IsDead && p.ReviveCalls == 1) &&
            players.All(p => p.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" })),
            "All-dead explosion cleanup restores each entrant once after nested death completion");

        players = New(2); BeginAtThree(3); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        for (int holding = 0; holding < 4; holding++)
        {
            var from = Carrier(players); var to = players.First(p => p != from);
            Kill(from, to);
            if (holding == 3)
            {
                check(!CombatMatchRuntime.IsRunning && to.ReviveCalls == 2 && PlayTagExplosion.Shown.Single() == to,
                    "Third minimum receipt explodes before revival and ends the match exactly once");
                check(to.Lifecycle.Where(x => x.StartsWith("Rpc")).SequenceEqual(new[] { "RpcDie", "RpcRevive", "RpcDie", "RpcRevive" }),
                    "Immediate outcome recovers after RpcDie without revive-then-kill");
            }
            else
            {
                check(Bomb().AwaitingRespawn, "Minimum receipt waits for actual native revival");
                At(time + 1);
                check(CombatMatchRuntime.ResetScores(out _) && Bomb().Fuse == 3,
                    "KDA reset preserves finite minimum-fuse allowance and timer");
            }
        }

        players = New(3); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient); recipient.spawner.connectionToClient.isReady = false;
        At(20);
        check(recipient.IsDead && Bomb().AwaitingRespawn && Bomb().Remaining(20) == 24,
            "Loading recipient preserves unarmed fuse without extending an armed holding");
        recipient.spawner.connectionToClient.isReady = true; At(21);
        check(!recipient.IsDead && !Bomb().AwaitingRespawn && Bomb().Remaining(21) == 24,
            "Successful delayed native revival arms a full reduced fuse");
        CombatMatchRuntime.Stop(true, false);

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient); UnityEngine.Time.unscaledTime = time = 3.5f;
        recipient.Revive(100); At(4);
        check(!Bomb().AwaitingRespawn && Bomb().Remaining(27.5) == 0 && recipient.ReviveCalls == 1,
            "Manual native revival arms at its completed time and invalidates automatic respawn");
        CombatMatchRuntime.Stop(true, false);

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient); recipient.ReviveCallback = () => throw new InvalidOperationException("manual revive failed");
        try { recipient.Revive(100); } catch (InvalidOperationException) { }
        check(!CombatMatchRuntime.IsRunning && recipient.ReviveRpcCalls == 0 && PlayTagExplosion.Shown.Count == 0,
            "Failed manual native revival safely stops instead of leaving an immortal unarmed bomb");

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient);
        recipient.ReviveCallback = () =>
        {
            recipient.Revive(100); // IsDead is already false, so this native call is a no-op.
            throw new InvalidOperationException("outer native revival failed after nested no-op");
        };
        try { recipient.Revive(100); } catch (InvalidOperationException) { }
        check(!CombatMatchRuntime.IsRunning && recipient.ReviveRpcCalls == 0 && PlayTagExplosion.Shown.Count == 0,
            "Nested no-op revival cannot arm a bomb before an exceptional outer native completion");

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient);
        bool awaitingInside = false;
        recipient.ReviveCallback = () =>
        {
            UnityEngine.Time.unscaledTime = time = 4;
            recipient.Revive(100);
            awaitingInside = Bomb().AwaitingRespawn;
            UnityEngine.Time.unscaledTime = time = 7;
        };
        recipient.Revive(100);
        check(awaitingInside && recipient.ReviveRpcCalls == 1 && !Bomb().AwaitingRespawn && Bomb().Remaining(30) == 1,
            "Nested no-op revival leaves the fuse unarmed until the admitted outer RPC completes");
        CombatMatchRuntime.Stop(true, false);

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient);
        var oldRevive = new FriendlyFireRuntime.ReviveNotice
        {
            PlayerReceipt = FriendlyFireParticipants.Capture(recipient), MatchToken = CombatMatchRuntime.MatchToken
        };
        recipient.ReviveCallback = () =>
        {
            recipient.ReviveCallback = null;
            CombatMatchRuntime.Stop(true, false); BeginAtThree();
            // Whichever entrant starts the new match, pass its bomb to the old
            // outer revival's recipient so a stale finalizer would stop it.
            var currentCarrier = Carrier(players);
            if (currentCarrier == recipient)
            {
                var next = players.First(p => p != recipient);
                Kill(currentCarrier, next); next.Revive(100); currentCarrier = next;
            }
            Kill(currentCarrier, recipient);
            throw new InvalidOperationException("old revival returns after a new match begins");
        };
        try { recipient.Revive(100); } catch (InvalidOperationException) { }
        check(CombatMatchRuntime.IsPlayTag && Bomb().AwaitingRespawn && Carrier(players) == recipient,
            "Old native revival exception cannot stop the new match's waiting carrier");
        recipient.IsDead = false;
        FriendlyFireRuntime.AfterRevive(recipient, oldRevive);
        check(Bomb().AwaitingRespawn, "Old successful revival completion cannot arm a new match's bomb");
        recipient.IsDead = true; CombatMatchRuntime.Stop(true, false);

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient);
        var staleRevive = new FriendlyFireRuntime.ReviveNotice
        {
            PlayerReceipt = FriendlyFireParticipants.Capture(recipient), MatchToken = CombatMatchRuntime.MatchToken
        };
        FriendlyFireParticipants.Retire(recipient.spawner);
        recipient.IsDead = false;
        FriendlyFireRuntime.AfterRevive(recipient, staleRevive);
        FriendlyFireRuntime.FinishRevive(new InvalidOperationException("stale connection"), recipient, staleRevive);
        check(CombatMatchRuntime.IsPlayTag && Bomb().AwaitingRespawn,
            "Retired connection's revival completion cannot arm or stop current match ownership");
        At(time + 1); check(!CombatMatchRuntime.IsRunning, "Current roster validation separately stops the lost carrier");

        players = New(2); BeginAtThree(3); carrier = Carrier(players);
        carrier.DieCallback = _ => carrier.Revive(100);
        At(6);
        check(!CombatMatchRuntime.IsRunning && PlayTagExplosion.Shown.Count == 1 && carrier.ReviveCalls == 1,
            "Native resurrection during bomb death cannot undo consumed outcome or cause another revival");

        players = New(2); BeginAtThree(3); carrier = Carrier(players);
        carrier.DieCallback = _ => CombatMatchRuntime.Stop(true, false);
        At(6);
        check(!CombatMatchRuntime.IsRunning && !PlayTagExplosion.Owned &&
            carrier.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }),
            "Explicit interruption during bomb death clears owned FX while deferring recovery until RpcDie");

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        var environmental = new DamageInstance(); recipient.Die(0, environmental);
        check(Bomb().Carrier == Array.IndexOf(players, carrier), "Environmental deaths never pass the bomb");
        At(4);
        recipient.DieCallback = _ => throw new InvalidOperationException("native death callback failed");
        try { Kill(carrier, recipient); } catch (InvalidOperationException) { }
        check(Bomb().Carrier == Array.IndexOf(players, carrier) && FriendlyFireKda.For(carrier).Kills == 1,
            "Exceptional confirmed deaths retain existing KDA but cannot transfer");
        recipient.DieCallback = null; CombatMatchRuntime.Stop(true, false);

        players = New(2); BeginAtThree(3); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        recipient.DieCallback = _ => UnityEngine.Time.unscaledTime = time = 6;
        Kill(carrier, recipient);
        check(!CombatMatchRuntime.IsRunning && PlayTagExplosion.Shown.Single() == carrier,
            "A kill completing at fuse expiry cannot transfer the expired bomb");

        players = New(3); BeginAtThree(); carrier = Carrier(players);
        var late = add(90).PlayerAvatar;
        At(4);
        check(players.All(p => !CombatMatchRuntime.AllowsPair(late, p)) && !CombatMatchRuntime.AllowsParticipant(late),
            "Late joins cannot enter frozen Tag enrollment");
        recipient = players.First(p => p != carrier);
        FriendlyFireParticipants.Retire(recipient.spawner); PlayerSpawner.MultiplayerList.Remove(recipient.spawner);
        var replacement = add(recipient.netId).PlayerAvatar; At(5);
        check(CombatMatchRuntime.IsPlayTag && players.All(p => !CombatMatchRuntime.AllowsPair(replacement, p)),
            "Reconnection cannot inherit the old entrant while two current entrants remain");
        FriendlyFireParticipants.Retire(carrier.spawner); At(6);
        check(!CombatMatchRuntime.IsRunning && PlayTagExplosion.Shown.Count == 0,
            "Carrier disconnect safely stops without transferring its fuse to a new connection");

        players = New(3); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        string passerName = carrier.playerNameSource;
        Kill(carrier, recipient);
        FriendlyFireParticipants.Retire(carrier.spawner); PlayerSpawner.MultiplayerList.Remove(carrier.spawner);
        carrier.playerNameSource = ""; At(4); At(28);
        check(DungeonManager.Instance.ChatMessages.Any(x => x.Message ==
            $"'{recipient.playerNameSource}' was blown up by '{passerName}'s bomb."),
            "Explosion retains its sanitized last-passer name after that connection departs");

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        Kill(carrier, recipient);
        FriendlyFireParticipants.Retire(carrier.spawner); At(4);
        check(!CombatMatchRuntime.IsRunning && !recipient.IsDead && PlayTagExplosion.Shown.Count == 0,
            "Fewer than two current entrants stops and recovers the connected carrier");

        foreach (string action in new[] { "players reset", "players all", "player 1 off", "on", "off", "reset" })
        {
            players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
            recipient.DieCallback = _ => SettingsActions.Execute("/one friendlyfire " + action);
            Kill(carrier, recipient);
            check(!CombatMatchRuntime.IsRunning && recipient.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }) &&
                PlayTagExplosion.Shown.Count == 0, "Selection/master edit inside native death recovers after RpcDie: " + action);
        }

        foreach (string transition in new[] { "restart", "save", "dungeon", "authority", "settlement" })
        {
            players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
            Kill(carrier, recipient);
            switch (transition)
            {
                case "restart": HorayModAPI.StartSession(); break;
                case "save": SaveManager.CurrentRun = new(); break;
                case "dungeon": DungeonManager.Instance = new(); break;
                case "authority": Mirror.NetworkServer.active = false; break;
                case "settlement": SaveManager.CurrentRun.enableSave = false; break;
            }
            At(40);
            check(!CombatMatchRuntime.IsRunning && recipient.ReviveCalls == 0 && PlayTagExplosion.Shown.Count == 0,
                "Tag timers and old-run recovery never cross " + transition);
        }

        players = New(2); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        var oldKiller = FriendlyFireParticipants.Capture(carrier); var oldVictim = FriendlyFireParticipants.Capture(recipient);
        long oldMatch = CombatMatchRuntime.MatchToken;
        CombatMatchRuntime.Stop(true, false); BeginAtThree();
        int before = Bomb().Carrier; recipient.IsDead = true;
        CombatMatchRuntime.ConfirmedKill(oldKiller, oldVictim, oldMatch);
        check(Bomb().Carrier == before && !Bomb().AwaitingRespawn, "Old match token cannot transfer a fresh match's bomb");
        CombatMatchRuntime.Stop(true, false);

        players = New(3); BeginAtThree(); carrier = Carrier(players); recipient = players.First(p => p != carrier);
        oldKiller = FriendlyFireParticipants.Capture(carrier); oldVictim = FriendlyFireParticipants.Capture(recipient);
        oldMatch = CombatMatchRuntime.MatchToken; before = Bomb().Carrier;
        recipient.IsDead = true; FriendlyFireParticipants.Retire(recipient.spawner);
        PlayerSpawner.MultiplayerList.Remove(recipient.spawner); add(recipient.netId);
        CombatMatchRuntime.ConfirmedKill(oldKiller, oldVictim, oldMatch);
        check(Bomb().Carrier == before && !Bomb().AwaitingRespawn, "An old connection receipt cannot transfer even within the current match");
        CombatMatchRuntime.Stop(true, false);

        players = New(5); BeginAtThree(3600);
        for (int i = 0; i < 20; i++) CombatMatchRuntime.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) CombatMatchRuntime.Tick();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated,
            "Unchanged active Tag ticks allocate no per-frame state or repeated bubbles");
        CombatMatchRuntime.Stop(true, false);
        PlayTagRoundsTests.Run(start, add, check);
        harmony.UnpatchAll("PlayTag.integration.tests"); ReviveAllHooks.Uninstall(); start();
    }
}
