using SephiriaOne;

internal static class DeathmatchTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        ReviveAllHooks.Install();
        var harmony = new HarmonyLib.Harmony("Deathmatch.integration.tests");
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyLib.HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"));
        UnityEngine.Time.unscaledTime = 0;
        var host = start(); var guest = add(90);
        check(SettingsActions.Execute("/one deathmatch duration 10").Success, "Set match duration in seconds");
        check(SettingsActions.Execute("/one deathmatch start").Success, "Host can start a temporary deathmatch through the shared command");
        check(!SettingsActions.Execute("/one deathmatch start").Success, "Cannot restart an ongoing countdown");
        check(!SessionSettings.FriendlyFireForHit.Enabled && !SessionSettings.ReadSnapshot().FriendlyFire.Enabled, "Warmup and UI both keep friendly fire off");
        void At(float time) { UnityEngine.Time.unscaledTime = time; DeathmatchRuntime.Tick(); }
        At(1); At(2); At(3);
        check(SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.ReadSnapshot().FriendlyFire.Enabled, "Match enables friendly fire and toggle together");
        var chat = DungeonManager.Instance.ChatMessages;
        check(chat.Take(4).Select(x => x.Message).SequenceEqual(new[] { "3...", "2...", "1...", "Deathmatch start!" }), "Opening countdown sends each second and start message");
        host.PlayerAvatar.playerNameSource = "Host"; guest.PlayerAvatar.playerNameSource = "Guest";
        FriendlyFireKda.Damage(host.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch);
        var death = FriendlyFireKda.BeforeDeath(guest.PlayerAvatar, host.PlayerAvatar, FriendlyFireKda.Epoch);
        guest.PlayerAvatar.IsDead = true; FriendlyFireKda.CompleteDeath(guest.PlayerAvatar, death); DeathmatchRuntime.Died(guest.PlayerAvatar);
        At(4); At(5);
        check(guest.PlayerAvatar.IsDead, "Player remains dead for full three-second respawn countdown");
        At(6);
        check(!guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveRpcCalls == 1, "Respawn uses native revival RPC at three seconds");
        check(chat.Where(x => x.Player == guest.PlayerAvatar).Select(x => x.Message).SequenceEqual(new[] { "Respawn in: 3s", "Respawn in: 2s", "Respawn in: 1s", "Respawned!" }), "Native avatar chat gets exact respawn countdown and replaces it on recovery");
        var damageEdit = SettingsActions.Execute("/one friendlyfire damage 75");
        check(damageEdit.Messages.Any(m => m.Contains("Friendly fire: on")), "Damage command reports the effective match toggle, not underlying preset state");
        check(FriendlyFireKda.Label(host.PlayerAvatar, "Host") == "Host(1/0/0)", "Damage edit during match preserves scoreboard identity and totals");
        check(SettingsActions.Execute("/one deathmatch duration 20").Success, "Editing duration is allowed during match");
        At(12); guest.PlayerAvatar.IsDead = true; DeathmatchRuntime.Died(guest.PlayerAvatar);
        At(13);
        check(!DeathmatchRuntime.IsRunning && !SessionSettings.FriendlyFireForHit.Enabled && !guest.PlayerAvatar.IsDead, "Expiry uses original duration, turns off friendly fire, revives pending players");
        check(chat.Any(x => x.Message.Contains("(1/0/0)")) && chat.Any(x => x.Message.Contains("(0/1/0)")), "Final standings survive toggle score reset");
        check(FriendlyFireKda.Label(host.PlayerAvatar, "Host") == "Host(0/0/0)", "Normal friendly-fire KDA resets after match ends");

        foreach (string toggle in new[] { "on", "off", "reset" })
        {
            host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
            guest.PlayerAvatar.IsDead = true; DeathmatchRuntime.Died(guest.PlayerAvatar);
            check(SettingsActions.Execute("/one friendlyfire " + toggle).Success && !DeathmatchRuntime.IsRunning && !guest.PlayerAvatar.IsDead,
                "Manual toggle stops match and recovers pending players: " + toggle);
            check(SessionSettings.FriendlyFireForHit.Enabled == (toggle == "on"), "Manual toggle selection wins: " + toggle);
            int revives = guest.PlayerAvatar.ReviveCalls; At(10);
            check(guest.PlayerAvatar.ReviveCalls == revives, "Old respawn timer cannot fire after toggle: " + toggle);
        }
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one friendlyfire on"); SettingsActions.Execute("/one deathmatch start");
        SettingsActions.Execute("/one friendlyfire damage 50");
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Damage edits cannot arm the warmup"); At(3);
        check(DeathmatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.DamagePercent == 50, "Damage slider edits do not stop match");
        var joiner = add(91); joiner.PlayerAvatar.IsDead = true; At(4); At(7);
        check(!joiner.PlayerAvatar.IsDead, "Newly joined dead player gets complete match respawn state");
        guest.PlayerAvatar.IsDead = true; DeathmatchRuntime.Died(guest.PlayerAvatar); PlayerSpawner.MultiplayerList.Remove(guest);
        var replacement = add(90); At(8); At(11);
        check(guest.PlayerAvatar.ReviveCalls == 0 && replacement.PlayerAvatar.ReviveCalls == 0, "Disconnected/reused slot does not inherit old respawn timer");
        guest = replacement; guest.PlayerAvatar.IsDead = true; DeathmatchRuntime.Died(guest.PlayerAvatar);
        HorayModAPI.StartSession(); At(12);
        check(!DeathmatchRuntime.IsRunning && !SessionSettings.FriendlyFireForHit.Enabled && guest.PlayerAvatar.ReviveCalls == 0, "New run cancels without reviving old avatars");

        host = start(); At(0); SettingsActions.Execute("/one deathmatch duration 45"); SettingsActions.Execute("/one deathmatch start"); At(3);
        check(SettingsActions.Execute("/one save").Success, "Can save settings during match");
        string preset = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(preset.Contains("deathmatch duration 45") && !preset.Contains("friendlyfire enabled 1"), "Preset saves duration, not temporary match-enabled friendly fire");
        check(SessionPolicy.TryReadPreset(preset, out var saved, out _) && saved.DeathmatchDuration == 45, "Duration round-trips in new preset");
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v17\n", out saved, out _) && saved.DeathmatchDuration == 300, "Old presets get default duration");
        foreach (string value in new[] { "0", "9", "3601", "-10", "NaN", "10.5", "x2", "2147483648" })
            check(!SettingsActions.Execute("/one deathmatch duration " + value).Success, "Reject invalid duration: " + value);
        Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one deathmatch stop").Success, "Guest cannot stop host match"); Mirror.NetworkServer.active = true;
        DeathmatchRuntime.Stop(true, false);
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        host.PlayerAvatar.IsDead = guest.PlayerAvatar.IsDead = true;
        DeathmatchRuntime.Died(host.PlayerAvatar); DeathmatchRuntime.Died(guest.PlayerAvatar);
        host.PlayerAvatar.ReviveCallback = () => SettingsActions.Execute("/one friendlyfire off");
        At(6); At(6.1f);
        check(!DeathmatchRuntime.IsRunning && !host.PlayerAvatar.IsDead && !guest.PlayerAvatar.IsDead &&
            host.PlayerAvatar.ReviveRpcCalls == 1 && guest.PlayerAvatar.ReviveRpcCalls == 1,
            "Toggle inside native revival completes current recovery and drains remaining respawns without stale RPCs");
        foreach (bool countdown in new[] { true, false })
        {
            host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); if (!countdown) At(3);
            guest.PlayerAvatar.DieCallback = _ =>
            {
                SettingsActions.Execute("/one friendlyfire off");
                check(guest.PlayerAvatar.ReviveCalls == 0 && DeathmatchRuntime.ProtectDeath(guest.PlayerAvatar), "In-flight native death stays protected until its tail finishes");
            };
            guest.PlayerAvatar.Die(1, new());
            check(!DeathmatchRuntime.IsRunning && !guest.PlayerAvatar.IsDead &&
                guest.PlayerAvatar.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }), "Stopping inside a death callback preserves death-before-revival RPC order: " + countdown);
        }
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start");
        guest.PlayerAvatar.Die(1, new()); SettingsActions.Execute("/one deathmatch stop");
        check(!guest.PlayerAvatar.IsDead, "Stopping countdown recovers a dead player before respawn registration");
        host = start(); guest = add(90); host.PlayerAvatar.IsDead = true; At(0); SettingsActions.Execute("/one deathmatch start"); At(1);
        guest.PlayerAvatar.Die(1, new()); At(3);
        check(!host.PlayerAvatar.IsDead && guest.PlayerAvatar.IsDead, "Initially dead host respawns when warmup ends, while later warmup death keeps its deadline");
        At(4); check(!guest.PlayerAvatar.IsDead, "Death during opening countdown also gets exactly three seconds");
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        guest.PlayerAvatar.Die(1, new()); At(4); guest.PlayerAvatar.Revive(100); At(5); guest.PlayerAvatar.Die(1, new()); At(6);
        check(guest.PlayerAvatar.IsDead, "Native manual revival cancels old countdown; new death gets a fresh three seconds"); At(8);
        check(!guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveCalls == 2, "New life timer revives exactly once");
        DeathmatchRuntime.Stop(true, false);
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        host.PlayerAvatar.Die(1, new()); guest.PlayerAvatar.Die(1, new());
        host.PlayerAvatar.ReviveCallback = () => { guest.PlayerAvatar.Revive(100); guest.PlayerAvatar.Die(1, new()); };
        At(6);
        check(guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveCalls == 1, "Copied due timer cannot consume a replacement life created by another player's callback");
        At(9); check(!guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveCalls == 2, "Replacement life retains its own complete countdown");
        DeathmatchRuntime.Stop(true, false);
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        host.PlayerAvatar.Die(1, new()); guest.PlayerAvatar.Die(1, new());
        guest.PlayerAvatar.ReviveCallback = () => { host.PlayerAvatar.Revive(100); host.PlayerAvatar.Die(1, new()); };
        SettingsActions.Execute("/one deathmatch stop");
        check(host.PlayerAvatar.IsDead && host.PlayerAvatar.ReviveCalls == 1, "Stop cleanup cannot revive a later normal life after a callback already revived its captured death");
        host = start(); guest = add(90); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        var loading = add(91); loading.connectionToClient.isReady = false;
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "test");
        check(SettingsActions.Execute("/one friendlyfire on").Success && !DeathmatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.Enabled,
            "Manual toggle wins despite initializing joiner and unrelated fault");
        host = start(); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        host.PlayerAvatar.DieCallback = _ =>
        {
            HorayModAPI.StartSession();
            check(!SettingsActions.Execute("/one deathmatch start").Success, "Cannot start a new match inside an old run's dying stack");
        };
        host.PlayerAvatar.Die(1, new());
        check(!DeathmatchRuntime.IsRunning && host.PlayerAvatar.ReviveCalls == 0, "Old run finalizer cannot revive into new run");

        host = start(); At(0); var ranked = new List<PlayerSpawner> { host };
        for (uint id = 2; id <= 6; id++) ranked.Add(add(id));
        foreach (var p in ranked) p.PlayerAvatar.playerNameSource = "P" + p.netId;
        SettingsActions.Execute("/one deathmatch start"); At(3);
        for (int i = 0; i < ranked.Count; i++) FriendlyFireKda.For(ranked[i].PlayerAvatar).Kills = 6 - i;
        FriendlyFireKda.For(ranked[1].PlayerAvatar).Kills = 6;
        FriendlyFireKda.For(ranked[1].PlayerAvatar).Deaths = 1;
        PlayerSpawner.MultiplayerList.Remove(ranked[2]); At(4);
        SettingsActions.Execute("/one deathmatch stop");
        var standings = DungeonManager.Instance.ChatMessages.Where(x => x.Message.Length > 2 && char.IsDigit(x.Message[0]) && x.Message[1] == '.' && x.Message[2] == ' ').Select(x => x.Message).ToArray();
        check(standings.Length == 5 && standings[0].Contains("P1(6/0/0)") && standings[1].Contains("P2(6/1/0)") &&
            standings[2].Contains("P3(4/0/0)") && !standings.Any(s => s.Contains("P6")), "Top five rank ties by fewer deaths and retain departed participant results");

        host = start(); At(0); SettingsActions.Execute("/one deathmatch start"); At(3); At(3.2f);
        for (int i = 0; i < 50; i++) At(4 + i * 0.11f);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) At(10 + i * 0.11f);
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed active match tick with living players allocates zero bytes");
        DeathmatchRuntime.Stop(false, false);
        allocated = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) DeathmatchRuntime.Tick();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Idle match tick allocates zero bytes");
        foreach (string presetText in new[] { "SephiriaOne preset v17\ndeathmatch duration 60\n", "SephiriaOne preset v18\ndeathmatch duration 60\ndeathmatch duration 61\n", "SephiriaOne preset v18\ndeathmatch duration 9\n" })
            check(!SessionPolicy.TryReadPreset(presetText, out _, out _), "Reject noncanonical/duplicate/invalid duration preset");
        DeathmatchProgressTests.Run(start, add, check);
        harmony.UnpatchAll("Deathmatch.integration.tests"); ReviveAllHooks.Uninstall(); start();
    }
}
