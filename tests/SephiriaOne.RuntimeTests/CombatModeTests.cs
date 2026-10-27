using SephiriaOne;

internal static class CombatModeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void Command(string text) => check(SettingsActions.Execute("/one friendlyfire " + text).Success, "Combat Mode command: " + text);
        CombatModeHooks.Install();
        try
        {
            foreach (string value in new[] { "ON", "off", "reset" })
            {
                check(FriendlyFireCommand.Parse("/ONE FRIENDLYFIRE COMBAT-MODE " + value, out var command) == FriendlyFireParseResult.Valid &&
                    !command.TouchesMasterToggle && command.IsReset == (value != "ON"), "Combat Mode isolated parse/reset");
            }
            foreach (string value in new[] { "", "true", "on extra", "1" })
                check(FriendlyFireCommand.Parse("/one friendlyfire combat-mode " + value, out _) == FriendlyFireParseResult.Invalid, "Reject malformed Combat Mode");
            var policy = new SessionPolicy();
            check(!policy.FriendlyFire.CombatMode && !policy.HasChanges, "Combat Mode defaults off");
            policy.Record(new FriendlyFireCommand(false, combatMode: true));
            check(policy.ToPresetText() == "SephiriaOne preset v55\nfriendlyfire combat-mode 1\n", "Sparse v55 Combat Mode");
            check(SessionPolicy.TryReadPreset(policy.ToPresetText(), out var loaded, out _) && loaded.FriendlyFire.CombatMode, "Combat Mode round trip");
            check(policy.FriendlyFire.WithEnabled(true).CombatMode && policy.FriendlyFire.WithEnabled(false).CombatMode, "Match overlay preserves preference");
            for (int version = 1; version < 55; version++)
            {
                check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) && !old.FriendlyFire.CombatMode, "Old preset defaults off");
                check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nfriendlyfire combat-mode 1\n", out _, out _), "Older schema rejects new row");
            }
            foreach (string bad in new[] { "2", "-1", "1.0", "1\nfriendlyfire combat-mode 0" })
                check(!SessionPolicy.TryReadPreset("SephiriaOne preset v55\nfriendlyfire combat-mode " + bad + "\n", out _, out _), "Invalid/duplicate Combat Mode rows");
            var host = start().PlayerAvatar; var guest = add(2).PlayerAvatar;
            Command("combat-mode on");
            check(!host.IsInBattle && !guest.IsInBattle, "Preference alone does not lock menus");
            Command("on");
            check(host.IsInBattle && guest.IsInBattle, "Manual PvP immediately overlays host and guest combat counters");
            int writes = host.BattleWrites + guest.BattleWrites;
            for (int i = 0; i < 100; i++) CombatModeRuntime.Tick();
            check(host.BattleWrites + guest.BattleWrites == writes, "Stable frames do not generate SyncVar writes");
            guest.MoveFloorViaWorldmap("other", true, "START");
            check(guest.MapTrips == 0, "Host rejects participant map travel");
            host.StartBattle(); host.StartBattle(); host.StopBattle();
            check(host.isInBattle == 1 && host.StartedBattles == 1 && host.StoppedBattles == 0, "Native nested combat count/events preserved");
            Command("combat-mode off");
            check(host.isInBattle == 1 && guest.isInBattle == 0, "Disable restores only addon state, retaining native combat");
            host.StopBattle(); check(!host.IsInBattle && host.StoppedBattles == 1, "Native combat ends normally");
            Command("combat-mode on"); host.StartBattle(); host.StopBattle();
            check(host.IsInBattle && host.StartedBattles == 2 && host.StoppedBattles == 2, "Native event boundaries run under overlay");
            guest.spawner.connectionToClient.isReady = false;
            Command("combat-mode off"); check(!guest.IsInBattle, "Disable succeeds while guest loading");
            guest.spawner.connectionToClient.isReady = true;
            Command("combat-mode on");
            FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(host), true);
            CombatModeRuntime.Tick(); check(host.IsInBattle && !guest.IsInBattle, "Only selected player locked");
            guest.MoveFloorViaWorldmap("other", true, "START"); check(guest.MapTrips == 1, "Unselected map request stays native");
            FriendlyFireParticipants.ResetSelection(); CombatModeRuntime.Tick();
            check(guest.IsInBattle, "Empty selection includes all");
            FriendlyFireParticipants.Retire(guest.spawner); CombatModeRuntime.Tick();
            check(!guest.IsInBattle, "Disconnected incarnation released");
            PlayerSpawner.MultiplayerList.Remove(guest.spawner); guest = add(2).PlayerAvatar; CombatModeRuntime.Tick();
            check(guest.IsInBattle, "Rejoin same id receives fresh restriction");
            var initialization = FriendlyFireParticipants.Initializing(guest.spawner);
            check(!guest.IsInBattle, "Native initialization starts without stale overlay");
            guest.isInBattle = 1; // a new incarnation is already in real combat
            CombatModeRuntime.Tick(); check(guest.isInBattle == 1, "Retired receipt cannot reset newly initialized native combat");
            FriendlyFireParticipants.Initialized(initialization, true); CombatModeRuntime.Tick();
            Command("combat-mode off"); check(guest.isInBattle == 1, "Reinitialized native battle survives cleanup");
            guest.isInBattle = 0; Command("combat-mode on");
            // Reentrant real-combat events can toggle settings: never add an
            // artificial count inside the native increment/decrement sequence.
            host.NativeBattleCallback = () => { Command("combat-mode off"); Command("combat-mode on"); };
            host.StartBattle(); Command("combat-mode off");
            check(host.isInBattle == 1, "Reentrant toggles do not leak a native counter");
            host.NativeBattleCallback = null; host.StopBattle();
            foreach (sbyte native in new sbyte[] { -128, -1, 0, 126, 127 })
            {
                host.isInBattle = native; Command("combat-mode on");
                check(host.IsInBattle, "Counter boundary blocks menus: " + native);
                Command("combat-mode off"); check(host.isInBattle == native, "Counter boundary restores exactly: " + native);
            }
            host.isInBattle = 0; Command("combat-mode on");
            host.NativeBattleCallback = () => throw new InvalidOperationException("native test");
            try { host.StartBattle(); check(false, "Native error should propagate"); } catch (InvalidOperationException) { }
            check(host.IsInBattle, "Exceptional native callback restores combat overlay");
            host.NativeBattleCallback = null; Command("off");
            check(!host.IsInBattle && !guest.IsInBattle, "Master off releases all overlays");
            foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
            {
                host = start().PlayerAvatar; guest = add(2).PlayerAvatar; add(3);
                Command("combat-mode on"); UnityEngine.Time.unscaledTime = 0;
                check(SettingsActions.Execute("/one " + mode + " start").Success, "Start with Combat Mode: " + mode);
                CombatModeRuntime.Tick(); check(host.IsInBattle && guest.IsInBattle, "Countdown locked: " + mode);
                var late = add(4).PlayerAvatar; CombatModeRuntime.Tick();
                // Deathmatch permits new eligible participants; roster-frozen
                // modes must retain their existing late-join behavior.
                check(CombatModeRuntime.Restricted(late) == CombatMatchRuntime.IsSupplyParticipant(late), "Shared late-join semantics: " + mode);
                UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick(); CombatModeRuntime.Tick();
                guest.IsDead = true; CombatModeRuntime.Tick(); check(guest.IsInBattle, "Death does not unlock menu: " + mode);
                Command("combat-mode off"); check(CombatMatchRuntime.IsRunning && !guest.IsInBattle, "Option change does not cancel match: " + mode);
                Command("combat-mode on"); CombatMatchRuntime.Stop(false, false); CombatModeRuntime.Tick();
                check(!host.IsInBattle && !guest.IsInBattle, "Stop releases all overlays: " + mode);
            }
            host = start().PlayerAvatar;
            for (uint id = 2; id <= 9; id++) add(id);
            Command("combat-mode on"); Command("on");
            for (int i = 0; i < 50; i++) CombatModeRuntime.Tick();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) CombatModeRuntime.Tick();
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            check(allocated == 0, "Stable nine-player Combat Mode has no per-frame allocations");
            Console.WriteLine($"Combat Mode: {allocated} bytes/1000 nine-player ticks");
            SessionSettings.Stop(); check(!host.IsInBattle, "Session shutdown restores native state");
        }
        finally { CombatModeRuntime.Clear(); CombatModeHooks.Uninstall(); }
    }
}
