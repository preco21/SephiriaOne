using SephiriaOne;

internal static class PvpMaxHpRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
        void Command(string command) => check(SettingsActions.Execute("/one friendlyfire " + command).Success, command);
        var host = start().PlayerAvatar; var guest = add(2).PlayerAvatar;
        host.MaxHp = 500; host.hp = 250; guest.MaxHp = 200; guest.hp = 100;
        PvpMaxHpFeature.Available = true;
        Command("max-hp on"); Command("balance on");
        check(host.MaxHp == 500, "Preference and balancing alone do not alter health");
        Command("on"); PvpMaxHpRuntime.Tick();
        check(Near(host.MaxHp, 200) && Near(host.hp, 100) && Near(guest.MaxHp, 187.5f) && Near(guest.hp, 93.75f),
            "Host/guest normalization preserves fraction and shared curve");
        int writes = host.HealthWrites + guest.HealthWrites;
        for (int i = 0; i < 100; i++) PvpMaxHpRuntime.Tick();
        check(writes == host.HealthWrites + guest.HealthWrites, "Stable ticks do not write synced health");
        host.hp = 60;
        check(Near(PvpMaxHpRuntime.NativeHpForSave(host), 150) && Near(host.hp, 60), "Saving exports native-scale current health without mutation");
        PvpMaxHpRuntime.BeginNative(host);
        check(Near(host.MaxHp, 500) && Near(host.hp, 150), "Native mutation begins with native health");
        PvpMaxHpRuntime.BeginNative(host); host.maxHp += 100; host.hp = host.MaxHp * 0.3f;
        PvpMaxHpRuntime.EndNative(host);
        check(Near(host.MaxHp, 600), "Nested mutation cannot reapply early");
        PvpMaxHpRuntime.EndNative(host);
        check(Near(host.MaxHp, 200) && Near(host.hp, 60), "Native growth rebases without compounding");
        Command("max-hp off");
        check(Near(host.MaxHp, 600) && Near(host.hp, 180), "Off restores updated native maximum and current percentage, not old HP");
        Command("max-hp on");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(host), true); PvpMaxHpRuntime.Tick();
        check(Near(host.MaxHp, 200) && Near(guest.MaxHp, 200), "Selection excludes guest");
        FriendlyFireParticipants.ResetSelection(); PvpMaxHpRuntime.Tick();
        guest.loadingScreenType = 0; PvpMaxHpRuntime.Tick();
        check(Near(guest.MaxHp, 200), "Loading releases projection");
        guest.loadingScreenType = -1; PvpMaxHpRuntime.Tick();
        check(Near(guest.MaxHp, 187.5f), "Loading completion reapplies once");
        guest.IsDead = true; guest.hp = 0; Command("max-hp off");
        check(guest.IsDead && guest.hp == 0, "Restoration cannot revive a dead player");
        Command("max-hp on"); guest.IsDead = false; guest.hp = guest.MaxHp;
        FriendlyFireParticipants.Retire(guest.spawner);
        check(Near(guest.MaxHp, 200), "Retirement immediately restores exact old avatar");
        PlayerSpawner.MultiplayerList.Remove(guest.spawner);
        var replacement = add(2).PlayerAvatar; replacement.MaxHp = 300; replacement.hp = 150; PvpMaxHpRuntime.Tick();
        check(Near(replacement.MaxHp, 200) && Near(guest.MaxHp, 200), "Reused network ID gets independent health lease");
        PvpMaxHpRuntime.BeginNative(host); Command("max-hp off"); Command("max-hp on");
        check(Near(host.MaxHp, 600), "Policy callbacks cannot project inside native scope");
        PvpMaxHpRuntime.EndNative(host); check(Near(host.MaxHp, 200), "Current policy resumes after native scope");
        Command("off"); check(Near(host.MaxHp, 600) && Near(replacement.MaxHp, 300), "Master off restores all participants");

        var p = new PvpMaxHpProjection(); var actor = host;
        actor.MaxHp = 500; actor.hp = 250;
        actor.isHPCursed = 1; actor.cursedMaxHp = 201; actor.hp = 100.5f;
        p.Apply(actor, new PvpMaxHpSettings(true));
        check(actor.isHPCursed == 1 && actor.cursedMaxHp == 187 && Near(actor.hp, 93.5f) && p.HasNativeCurse(actor, 201),
            "Cursed HP rounds down without removing native curse semantics");
        p.Release(actor); check(actor.cursedMaxHp == 201 && Near(actor.hp, 100.5f), "Native curse restores exactly");
        actor.MaxHp = 100; actor.hp = 120; p.Apply(actor, new PvpMaxHpSettings(true));
        check(actor.MaxHp == 100 && actor.hp == 120, "No HP increase or removal of native overheal below threshold");
        actor.MaxHp = 500; actor.hp = 600; p.Apply(actor, new PvpMaxHpSettings(true));
        check(Near(actor.hp, 240), "Overheal percentage remains native");
        actor.maxHp = 700; p.Release(actor); check(actor.maxHp == 700, "Restoration does not overwrite an external replacement");
        foreach (float percent in new[] { -75f, 20f, 100f, 333.33f, 10000f })
        {
            actor.MaxHp = 500; actor.finalMaxHp = percent;
            float nativeMax = actor.MaxHp; actor.hp = nativeMax * 0.37f;
            for (int i = 0; i < 20; i++)
            {
                p.Apply(actor, new PvpMaxHpSettings(true));
                check(actor.MaxHp <= 200 && Near(actor.hp / actor.MaxHp, 0.37f), "Native final-HP multiplier composes with normalization");
                check(Near(p.NativeHp(actor) / nativeMax, 0.37f), "Save conversion preserves native fraction with percentage multipliers");
                p.Release(actor);
                check(actor.maxHp == 500 && actor.finalMaxHp == percent && Near(actor.hp / actor.MaxHp, 0.37f),
                    "Repeated restoration retains native fields and health fraction without compounding");
            }
        }
        PvpMaxHpRuntime.Clear();

        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            UnityEngine.Time.unscaledTime = 0;
            var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar, add(4).PlayerAvatar };
            foreach (var player in players) { player.MaxHp = 500; player.hp = 250; }
            Command("max-hp on"); Command("balance on");
            check(SettingsActions.Execute("/one " + mode + " start").Success, mode + " starts");
            check(players.All(player => player.MaxHp == 500), mode + " countdown keeps native health");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            var fighters = players.Where(PvpParticipation.IsActive).ToArray();
            check(fighters.Length == (mode == "tournament" ? 2 : 4) &&
                players.All(player => Near(player.MaxHp, fighters.Contains(player) ? 200 : 500)),
                mode + " normalizes active fighters immediately at activation, including both Zombie teams");
            Command("balance off"); check(players.All(player => player.MaxHp == 500), mode + " balance off restores immediately");
            Command("balance on"); check(fighters.All(player => Near(player.MaxHp, 200)), mode + " live balancing reapplies");
            var dead = fighters[0]; dead.IsDead = true; dead.hp = 0;
            dead.ReviveCallback = () => check(dead.MaxHp == 500 && dead.hp == 500, mode + " restores before end-of-match native recovery");
            check(SettingsActions.Execute("/one " + mode + " stop").Success, mode + " stops");
            check(players.All(player => player.MaxHp == 500), mode + " stop restores all participants");
        }

        host = start().PlayerAvatar; host.MaxHp = 500; host.hp = 250;
        for (uint i = 2; i <= 9; i++) { var player = add(i).PlayerAvatar; player.MaxHp = 500; player.hp = 250; }
        Command("max-hp on"); Command("balance on"); Command("on");
        for (int i = 0; i < 100; i++) PvpMaxHpRuntime.Tick();
        writes = PlayerSpawner.MultiplayerList.Sum(s => s.PlayerAvatar.HealthWrites);
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) PvpMaxHpRuntime.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;
        check(allocated == 0 && PlayerSpawner.MultiplayerList.Sum(s => s.PlayerAvatar.HealthWrites) == writes,
            "Nine-player stable reconciliation allocates zero bytes and performs zero health writes");
        Console.WriteLine("PvP Max HP: " + allocated + " bytes/1000 nine-player ticks");
        var initialization = FriendlyFireParticipants.Initializing(host.spawner);
        check(host.MaxHp == 500, "Initialization retires old health projection immediately");
        host.MaxHp = 300; host.hp = 90;
        FriendlyFireParticipants.Initialized(initialization, true); PvpMaxHpRuntime.Tick();
        check(Near(host.MaxHp, 200) && Near(host.hp, 60), "Fresh initialized stats become the new baseline");
        PvpMaxHpRuntime.BeginNative(host);
        host.maxHp += 20; // a native failure leaves a partially updated, but finite maximum
        PvpMaxHpRuntime.EndNative(host, false);
        check(host.MaxHp == 320, "Failed native operation exits scope without reapplying inside its exception path");
        PvpMaxHpRuntime.Tick();
        check(host.MaxHp <= 200, "Subsequent normal maintenance is not stuck in failed native nesting");
        PvpMaxHpRuntime.BeginNative(host); host.maxHp = 300; host.hp = 90; PvpMaxHpRuntime.EndNative(host);
        SaveManager.CurrentRun = new SaveData(); PvpMaxHpRuntime.Tick();
        check(Near(PvpMaxHpRuntime.NativeHpForSave(host), 90), "Run scope replacement cannot adopt a projected baseline");
        PvpMaxHpFeature.Available = false; PvpMaxHpRuntime.Tick();
        check(host.MaxHp == 300 && Near(host.hp, 90), "Feature shutdown restores without requiring a current receipt");
        PvpMaxHpFeature.Available = true;
        host = start().PlayerAvatar; host.maxHp = 400; host.finalMaxHp = 25; host.hp = 200;
        Command("max-hp on"); Command("balance on"); Command("on");
        PvpMaxHpRuntime.BeginNative(host); PvpMaxHpRuntime.BeginNative(host);
        host.finalMaxHp = 50; host.hp = host.MaxHp * 0.4f;
        PvpMaxHpRuntime.EndNative(host); check(host.MaxHp == 600, "Nested native percentage update stays in native units");
        PvpMaxHpRuntime.EndNative(host);
        check(Near(host.MaxHp, 200) && Near(host.hp, 80) && Near(PvpMaxHpRuntime.NativeHpForSave(host), 240),
            "Percentage mutation reprojects and saves against its new native maximum");
        PvpMaxHpRuntime.BeginNative(host); host.isHPCursed = 1; host.cursedMaxHp = 250; PvpMaxHpRuntime.EndNative(host);
        check(Near(host.MaxHp, 200) && Near(host.hp, 192), "Curse transition respects native clamping semantics and effective ratio");
        PvpMaxHpRuntime.BeginNative(host); host.isHPCursed = 0; host.cursedMaxHp = 0; PvpMaxHpRuntime.EndNative(host);
        check(Near(host.MaxHp, 200) && Near(host.hp, 80), "Curse removal restores native percentage inputs before reprojecting");
        Command("off"); check(host.MaxHp == 600 && Near(host.hp, 240), "Curse mutation did not corrupt subsequent native restoration");
        start();
    }
}
