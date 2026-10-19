using SephiriaOne;
using HarmonyLib;

internal static class ZombieSurvivalRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one zombie status").Success, "Zombie Survival exposes host status through shared commands");
        check(!SettingsActions.Execute("/one zombie start").Success, "Zombie Survival requires at least two participants");
        add(2);
        check(SettingsActions.Execute("/one zombie start").Success, "Zombie Survival starts with two current participants");
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Zombie countdown blocks PvP combat");
        check(ZombieRoleVisuals.Visible.Count == 0, "Countdown has no Zombie role indicators");
        CombatMatchRuntime.Stop(true, false);

        var harmony = new Harmony("Zombie.integration.tests");
        ReviveAllHooks.Install();
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"), prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"), prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"), finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        float time = 0;
        void At(float value) { UnityEngine.Time.unscaledTime = time = value; CombatMatchRuntime.Tick(); }
        PlayerAvatar[] New(int count = 4)
        {
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) players.Add(add(i).PlayerAvatar);
            for (int i = 0; i < count; i++) players[i].playerNameSource = "P" + (i + 1);
            At(0); return players.ToArray();
        }
        void Begin(int seconds = 120)
        {
            check(SettingsActions.Execute("/one zombie duration " + seconds).Success && SettingsActions.Execute("/one zombie start").Success, "Zombie match starts through shared dispatcher");
            At(3);
        }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        {
            var hit = new DamageInstance(); FriendlyFireRuntime.Attribute(killer, victim, hit); victim.Die(0, hit);
        }
        bool Messages(string text) => DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains(text));
        var all = New(); Begin();
        var zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
        var humans = all.Where(p => p != zombie).ToArray();
        check(ZombieRoleVisuals.Visible.SetEquals(new[] { zombie }), "Only the initial Zombie owns a sustained role visual");
        check(CombatMatchRuntime.AllowsPair(zombie, humans[0]) && CombatMatchRuntime.AllowsPair(humans[0], zombie) &&
            !CombatMatchRuntime.AllowsPair(humans[0], humans[1]) && !CombatMatchRuntime.AllowsPair(zombie, zombie), "Only opposite teams are hostile");
        var late = add(20).PlayerAvatar;
        check(!CombatMatchRuntime.AllowsPair(zombie, late) && !CombatMatchRuntime.AllowsParticipant(late), "Late joiner is a spectator");
        check(CombatMatchRuntime.RefillRoleScale(zombie) == 3 && CombatMatchRuntime.IncomingRoleScale(zombie) == .3 &&
            CombatMatchRuntime.RefillRoleScale(humans[0]) == 1, "Zombie modifiers are independent active-role values");
        Kill(zombie, humans[0]);
        check(ZombieRoleVisuals.Visible.Contains(humans[0]), "Infection indicator starts during pending conversion and follows the corpse");
        check(humans[0].IsDead && !CombatMatchRuntime.IsActiveZombie(humans[0]) &&
            !CombatMatchRuntime.AllowsPair(humans[0], humans[1]), "Infected Human is not combat active before revival");
        check(FriendlyFireKda.For(zombie).Kills == 1 && FriendlyFireKda.For(humans[0]).Deaths == 1, "Conversion retains shared kill/death credit");
        At(3.5f); check(humans[0].IsDead, "Zombie respawn waits one second");
        At(4);
        check(!humans[0].IsDead && CombatMatchRuntime.IsActiveZombie(humans[0]) &&
            CombatMatchRuntime.AllowsPair(humans[0], humans[1]) && !CombatMatchRuntime.AllowsPair(humans[0], zombie), "Successful revive applies Zombie role to every pair query");
        check(CombatMatchRuntime.IncomingRoleScale(humans[0]) == .3, "Converted Zombie immediately inherits balancing");
        Kill(humans[1], humans[0]); At(5);
        check(CombatMatchRuntime.IsActiveZombie(humans[0]), "Killed Zombie respawns on its existing team");
        humans[1].Die(0, null); At(6);
        check(!humans[1].IsDead && !CombatMatchRuntime.IsActiveZombie(humans[1]), "Default environmental death retains Human role");
        check(SettingsActions.Execute("/one zombie any-death on").Success, "Any-death conversion is configurable live");
        humans[1].Die(0, null); At(7);
        check(CombatMatchRuntime.IsActiveZombie(humans[1]), "Opt-in environmental death infects on successful respawn");
        check(SettingsActions.Execute("/one zombie refill x3.25").Success && SettingsActions.Execute("/one zombie damage 45.5%").Success &&
            CombatMatchRuntime.RefillRoleScale(zombie) == 3.25 && CombatMatchRuntime.IncomingRoleScale(humans[0]) == .455,
            "Live Zombie modifier edits affect all Zombies without changing Tag settings");
        check(SessionSettings.PlayTagChaser.Equals(default(CombatRoleSettings)), "Zombie and Tag configurations are independent");
        var roleBeforeReset = CombatMatchRuntime.RoleRevision;
        check(SettingsActions.Execute("/one zombie resetkda").Success && FriendlyFireKda.For(zombie).Kills == 0 &&
            CombatMatchRuntime.RoleRevision == roleBeforeReset && CombatMatchRuntime.IsActiveZombie(humans[0]), "KDA reset preserves teams and timer");
        Kill(zombie, humans[2]);
        At(7.1f);
        check(!CombatMatchRuntime.IsRunning && !humans[2].IsDead && !SessionSettings.FriendlyFireForHit.Enabled &&
            Messages("Zombies win!") && all.All(p => Messages(p.Name + " - Zombie (")), "Last infection ends match, restores pending players and reports every final team");
        check(all.All(p => CombatMatchRuntime.IncomingRoleScale(p) == 1 && CombatMatchRuntime.RefillRoleScale(p) == 1), "End removes every role bonus immediately");
        check(ZombieRoleVisuals.Visible.Count == 0, "Natural conclusion removes every Zombie visual");

        all = New(2); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); var reflectedHuman = all.Single(p => p != zombie);
        // Native OnHit reflection can finish the Human death before the outer
        // attack reaches the Zombie's Die. Both belong to this match recovery.
        Kill(zombie, reflectedHuman);
        zombie.Die(0, null);
        At(3.1f);
        check(!CombatMatchRuntime.IsRunning && all.All(p => !p.IsDead), "Last-Human reflection waits for surrounding lethal attack before recovery");

        all = New(2); Begin(3); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); var human = all.Single(p => p != zombie);
        UnityEngine.Time.unscaledTime = 5.5f; human.Die(0, null); At(6);
        check(!CombatMatchRuntime.IsRunning && !human.IsDead && Messages("Humans win!") && Messages(human.Name + " - Human ("), "Unconverted pending Human survives deadline and recovers");
        all = New(2); Begin(3); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.Single(p => p != zombie);
        UnityEngine.Time.unscaledTime = 6; Kill(zombie, human); At(6);
        check(Messages("Humans win!") && !Messages("Zombies win!"), "Death at deadline cannot infect or overturn timeout");

        all = New(); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); humans = all.Where(p => p != zombie).ToArray();
        FriendlyFireKda.Damage(humans[0], zombie, FriendlyFireKda.Epoch);
        Kill(zombie, humans[0]); At(4); Kill(humans[1], zombie);
        check(FriendlyFireKda.For(humans[0]).Assists == 0, "Converted player cannot retain an assist against their new teammate");

        all = New(); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.First(p => p != zombie);
        human.DieCallback = _ => SettingsActions.Execute("/one zombie any-death on");
        human.Die(0, null); At(4);
        check(!CombatMatchRuntime.IsActiveZombie(human), "Changing any-death inside a death callback applies only to later deaths");
        human.DieCallback = _ => SettingsActions.Execute("/one zombie any-death off");
        human.Die(0, null); At(5);
        check(CombatMatchRuntime.IsActiveZombie(human), "An admitted any-death conversion retains its entry policy through callbacks");

        all = New(); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.First(p => p != zombie);
        var old = FriendlyFireParticipants.Capture(human); long oldToken = CombatMatchRuntime.MatchToken;
        Kill(zombie, human); human.Revive(100);
        check(CombatMatchRuntime.IsActiveZombie(human), "Successful manual revival activates pending Zombie");
        FriendlyFireParticipants.Retire(human.spawner);
        PlayerSpawner.MultiplayerList.Remove(human.spawner); var replacement = add(human.netId).PlayerAvatar;
        CombatMatchRuntime.Revived(old, oldToken); At(4);
        check(!CombatMatchRuntime.IsRunning && !CombatMatchRuntime.IsActiveZombie(replacement) && !Messages("Zombies win!"), "Reconnect cancels without inheriting team or declaring victory");
        check(ZombieRoleVisuals.Visible.Count == 0, "Reconnected participant never inherits a stale visual");

        all = New(); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
        zombie.spawner.connectionToClient.isReady = false; At(4);
        check(CombatMatchRuntime.IsRunning && !ZombieRoleVisuals.Visible.Contains(zombie), "Loading removes a temporarily unavailable target's indicator");
        zombie.spawner.connectionToClient.isReady = true; At(5);
        check(ZombieRoleVisuals.Visible.SetEquals(new[] { zombie }), "Readiness restores only the current Zombie indicator");

        foreach (string interrupt in new[] { "selection", "toggle", "stop", "run", "authority" })
        {
            all = New(); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.First(p => p != zombie);
            Kill(zombie, human);
            if (interrupt == "selection") FriendlyFireParticipants.ResetSelection();
            else if (interrupt == "toggle") SettingsActions.Execute("/one friendlyfire off");
            else if (interrupt == "stop") SettingsActions.Execute("/one zombie stop");
            else if (interrupt == "run") SaveManager.CurrentRun = new();
            else Mirror.NetworkServer.active = false;
            At(4);
            check(!CombatMatchRuntime.IsRunning && CombatMatchRuntime.IncomingRoleScale(zombie) == 1, "Zombie cleanup on " + interrupt);
            check(ZombieRoleVisuals.Visible.Count == 0, "Zombie visual cleanup on " + interrupt);
            if (interrupt is "selection" or "toggle" or "stop") check(!human.IsDead, "Stop recovers pending Human on " + interrupt);
        }
        all = New(); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.First(p => p != zombie);
        Kill(zombie, human);
        human.ReviveCallback = () => { human.Revive(100); throw new InvalidOperationException("revival failure"); };
        try { human.Revive(100); } catch (InvalidOperationException) { }
        check(!CombatMatchRuntime.IsRunning && !CombatMatchRuntime.IsActiveZombie(human), "Exceptional outer revival cannot activate Zombie through nested no-op");

        all = New();
        check(SettingsActions.Execute("/one zombie start").Success, "Activation readiness regression starts");
        void InvalidateActivation()
        {
            if (!SessionSettings.FriendlyFireForHit.Enabled) return;
            SessionSettings.SettingsChanged -= InvalidateActivation;
            foreach (var player in all) player.IsDead = true;
        }
        SessionSettings.SettingsChanged += InvalidateActivation;
        At(3);
        SessionSettings.SettingsChanged -= InvalidateActivation;
        check(!SessionSettings.FriendlyFireForHit.Enabled && !all.Any(CombatMatchRuntime.IsActiveZombie), "Activation revalidates roster after settings callbacks");
        At(4); At(4.2f); At(7.2f);
        check(all.Count(CombatMatchRuntime.IsActiveZombie) == 1, "Interrupted activation recovers and starts a fresh countdown");

        all = New(); all[1].IsDead = true;
        check(SettingsActions.Execute("/one zombie start").Success, "Dead entrant can recover during shared warmup");
        At(0); At(1); At(4);
        check(CombatMatchRuntime.IsZombieSurvival && all.Count(CombatMatchRuntime.IsActiveZombie) == 1, "Warmup recovers all entrants before initial role assignment");
        for (int i = 0; i < 20; i++) CombatMatchRuntime.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) CombatMatchRuntime.Tick();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Stable Zombie ticks allocate no per-frame state");
        CombatMatchRuntime.Stop(true, false);
        harmony.UnpatchAll("Zombie.integration.tests"); ReviveAllHooks.Uninstall(); start();
    }
}
