using SephiriaOne;

internal static class PlayTagChaserRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        bool Chaser(PlayerAvatar player) => CombatMatchRuntime.IsActiveChaser(player);
        var a = start().PlayerAvatar; var b = add(2).PlayerAvatar; var c = add(3).PlayerAvatar;
        UnityEngine.Time.unscaledTime = 0;
        check(!Chaser(a) && !Chaser(b), "Idle players have no chaser modifiers");
        check(CombatMatchRuntime.StartPlayTag(out _), "Start chaser lifecycle fixture");
        check(!Chaser(a) && !Chaser(b), "Countdown grants no chaser advantage");
        UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
        var players = new[] { a, b, c };
        var carrier = players.Single(Chaser); var recipient = players.First(p => p != carrier);
        check(players.Count(Chaser) == 1, "Exactly the randomly assigned carrier has bonuses");
        recipient.IsDead = true;
        var receipt = FriendlyFireParticipants.Capture(recipient);
        CombatMatchRuntime.ConfirmedKill(FriendlyFireParticipants.Capture(carrier), receipt, CombatMatchRuntime.MatchToken);
        check(!Chaser(carrier) && !Chaser(recipient), "Transfer immediately removes old bonuses; dead holder remains inactive");
        recipient.IsDead = false;
        check(!Chaser(recipient), "Unarmed holder does not gain bonuses before completed revival");
        CombatMatchRuntime.Revived(receipt, CombatMatchRuntime.MatchToken);
        check(Chaser(recipient) && !Chaser(carrier), "Actual revival arms only the new carrier's advantage");
        recipient.IsDead = true;
        check(!Chaser(recipient), "Dead carrier's queued effects gain no advantage");
        recipient.IsDead = false;
        check(Chaser(recipient), "Living armed holder regains role without extending fuse");
        for (int i = 0; i < 10000; i++) Chaser(recipient);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        bool valid = true;
        for (int i = 0; i < 10000; i++) valid &= Chaser(recipient);
        long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine($"Active chaser query: {delta} bytes/10000 queries");
        check(valid && delta == 0,
            "Real active chaser query including scope/receipt/connection checks allocates no per-hit state");
        UnityEngine.Time.unscaledTime = 27;
        check(!Chaser(recipient), "Transferred fuse expiry removes bonuses before the next controller tick");
        CombatMatchRuntime.Stop(true, false);
        check(players.All(p => !Chaser(p)), "Stopping clears all chaser modifiers immediately");
        check(CombatMatchRuntime.Start(out _), "Deathmatch can start after Tag");
        UnityEngine.Time.unscaledTime = 30; CombatMatchRuntime.Tick();
        check(players.All(p => !Chaser(p)), "Other combat modes never inherit a carrier advantage");
        CombatMatchRuntime.Stop(true, false);

        PlayerAvatar Begin()
        {
            a = start().PlayerAvatar; b = add(2).PlayerAvatar; c = add(3).PlayerAvatar;
            players = new[] { a, b, c };
            UnityEngine.Time.unscaledTime = 0;
            check(CombatMatchRuntime.StartPlayTag(out _), "Start fresh chaser boundary fixture");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            return players.Single(Chaser);
        }
        foreach (string transition in new[] { "restart", "save", "dungeon", "authority", "settlement" })
        {
            carrier = Begin();
            switch (transition)
            {
                case "restart": HorayModAPI.StartSession(); break;
                case "save": SaveManager.CurrentRun = new(); break;
                case "dungeon": DungeonManager.Instance = new(); break;
                case "authority": Mirror.NetworkServer.active = false; break;
                case "settlement": SaveManager.CurrentRun.enableSave = false; break;
            }
            check(!Chaser(carrier), "Chaser bonus cannot cross " + transition + " before reconciliation");
            CombatMatchRuntime.Stop(false, false);
        }
        carrier = Begin();
        var stale = FriendlyFireParticipants.Capture(carrier);
        FriendlyFireParticipants.Retire(carrier.spawner);
        PlayerSpawner.MultiplayerList.Remove(carrier.spawner);
        var replacement = add(carrier.netId).PlayerAvatar;
        check(!Chaser(carrier) && !Chaser(replacement), "Reused network ID never inherits a retired chaser bonus");
        CombatMatchRuntime.Revived(stale, CombatMatchRuntime.MatchToken);
        check(!Chaser(carrier) && !Chaser(replacement), "Stale revival cannot reactivate an old carrier role");
        CombatMatchRuntime.Tick();
        check(!CombatMatchRuntime.IsRunning, "Lost carrier stops the match through existing roster validation");
        start();
    }
}
