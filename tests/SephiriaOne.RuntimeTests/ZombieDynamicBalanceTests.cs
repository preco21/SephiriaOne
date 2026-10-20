using SephiriaOne;
using HarmonyLib;

internal static class ZombieDynamicBalanceTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        Curve(check);
        start();
        check(!SessionSettings.ZombieDynamicBalance, "Dynamic balance defaults off");
        check(SettingsActions.Execute("/one zombie dynamic on").Success,
            "Host can enable Zombie Dynamic Balancing");
        check(SettingsActions.Execute("/one save").Success, "Dynamic balance saves");
        string preset = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(preset.StartsWith("SephiriaOne preset v41\n") && preset.Contains("zombie dynamic 1\n"),
            "Dynamic balance uses versioned preset intent only");
        for (int v = 1; v <= 40; v++)
            check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{v}\nzombie dynamic 1\n", out _, out _),
                "Older schema rejects dynamic option v" + v);
        foreach (string bad in new[] { "2", "-1", "1.0", "1\nzombie dynamic 0" })
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v41\nzombie dynamic " + bad + "\n", out _, out _),
                "Malformed dynamic setting rejected: " + bad);
        check(SessionPolicy.TryReadPreset(preset, out var saved, out _) && saved.ToPresetText() == preset,
            "Dynamic preset round-trips");
        saved.Clear();
        check(saved.ToPresetText() == "SephiriaOne preset v1\n", "Full reset clears dynamic intent");
        check(SettingsActions.Execute("/one zombie dynamic reset").Success, "Dynamic reset accepted");
        Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one zombie dynamic on").Success, "Guest cannot enable dynamic modifiers");
        Mirror.NetworkServer.active = true;
        foreach (string bad in new[] { "yes", "1", "on extra" })
            check(!SettingsActions.Execute("/one zombie dynamic " + bad).Success, "Malformed command rejected: " + bad);

        PlayerAvatar[] New(int count)
        {
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) players.Add(add(i).PlayerAvatar);
            UnityEngine.Time.unscaledTime = 0;
            return players.ToArray();
        }
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        void Begin(int zombies = 1)
        {
            check(SettingsActions.Execute("/one zombie count " + zombies).Success &&
                SettingsActions.Execute("/one zombie dynamic on").Success &&
                SettingsActions.Execute("/one zombie start").Success, "Start dynamic Zombie match");
            At(3);
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .000001;
        var all = New(6); Begin();
        var zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
        check(Near(CombatMatchRuntime.RefillRoleScale(zombie), 37d / 27) &&
            Near(CombatMatchRuntime.IncomingRoleScale(zombie), 23d / 27), "1:5 yields bounded meaningful advantage");
        check(all.Where(p => p != zombie).All(p => CombatMatchRuntime.RefillRoleScale(p) == 1 &&
            CombatMatchRuntime.IncomingRoleScale(p) == 1), "Humans retain native role modifiers");
        long initialRevision = CombatMatchRuntime.RoleRevision;
        check(SettingsActions.Execute("/one zombie refill x7").Success && SettingsActions.Execute("/one zombie damage 12%").Success &&
            Near(CombatMatchRuntime.RefillRoleScale(zombie), 37d / 27) && CombatMatchRuntime.RoleRevision == initialRevision,
            "Dormant manual edits neither override dynamic values nor invalidate budgets");
        check(SettingsActions.Execute("/one zombie dynamic off").Success &&
            CombatMatchRuntime.RefillRoleScale(zombie) == 7 && CombatMatchRuntime.IncomingRoleScale(zombie) == .12,
            "Disabling resumes exact static settings immediately");
        check(SettingsActions.Execute("/one zombie dynamic on").Success && SettingsActions.Execute("/one zombie refill off").Success &&
            SettingsActions.Execute("/one zombie damage off").Success &&
            CombatMatchRuntime.RefillRoleScale(zombie) == 1 && CombatMatchRuntime.IncomingRoleScale(zombie) == 1,
            "Independent modifier switches override dynamic curve");
        SettingsActions.Execute("/one zombie refill on"); SettingsActions.Execute("/one zombie damage on");
        CombatMatchRuntime.Stop(true, false);
        check(all.All(p => CombatMatchRuntime.RefillRoleScale(p) == 1 && CombatMatchRuntime.IncomingRoleScale(p) == 1),
            "Stop removes all dynamic role modifiers");
        all = New(9); Begin(8);
        zombie = all.First(CombatMatchRuntime.IsActiveZombie);
        check(Near(CombatMatchRuntime.RefillRoleScale(zombie), 2371d / 3645) &&
            Near(CombatMatchRuntime.IncomingRoleScale(zombie), 1852d / 1215), "New nine-player match cannot reuse old ratio");
        var lastHuman = all.Single(p => !CombatMatchRuntime.IsActiveZombie(p));
        check(Near(CombatMatchRuntime.IncomingRoleScale(lastHuman), 2371d / 3645) &&
            CombatMatchRuntime.RefillRoleScale(lastHuman) == 1,
            "Last Human gains direct protection; offense comes from Zombie vulnerability, not a second refill boost");
        check(ZombieSurvivalCommand.DescribeModifiers().Contains("[H] Incoming damage: 65.05%."),
            "Dynamic status reports current minority-Human compensation");
        var spectator = add(99).PlayerAvatar;
        check(CombatMatchRuntime.IncomingRoleScale(spectator) == 1 && CombatMatchRuntime.RefillRoleScale(spectator) == 1,
            "Late spectator cannot acquire minority-Human protection");
        lastHuman.IsDead = true;
        check(CombatMatchRuntime.IncomingRoleScale(lastHuman) == 1, "Dead Human cannot retain an active combat modifier");
        lastHuman.IsDead = false;
        check(SettingsActions.Execute("/one zombie damage off").Success &&
            all.All(p => CombatMatchRuntime.IncomingRoleScale(p) == 1),
            "Incoming switch disables dynamic protection/vulnerability for both teams");
        check(SettingsActions.Execute("/one zombie damage on").Success &&
            Near(CombatMatchRuntime.IncomingRoleScale(lastHuman), 2371d / 3645),
            "Incoming switch resumes current Human protection without resetting composition");
        check(SettingsActions.Execute("/one zombie dynamic off").Success &&
            CombatMatchRuntime.IncomingRoleScale(lastHuman) == 1 &&
            CombatMatchRuntime.IncomingRoleScale(zombie) == SessionSettings.ZombieModifiers.IncomingScale,
            "Static mode removes Human protection immediately and restores exact manual Zombie policy");
        SettingsActions.Execute("/one zombie dynamic on");
        CombatMatchRuntime.Stop(true, false);
        var harmony = new Harmony("Zombie.dynamic.tests");
        ReviveAllHooks.Install();
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"), prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"), prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"), finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        try
        {
            all = New(6); Begin();
            zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
            var human = all.First(p => p != zombie);
            double before = CombatMatchRuntime.RefillRoleScale(zombie);
            var hit = new DamageInstance(); FriendlyFireRuntime.Attribute(zombie, human, hit); human.Die(0, hit);
            check(human.IsDead && Near(CombatMatchRuntime.RefillRoleScale(zombie), before) &&
                CombatMatchRuntime.RefillRoleScale(human) == 1, "Conversion retargets continuously; corpse gets no active bonus");
            long revision = CombatMatchRuntime.RoleRevision;
            UnityEngine.Time.unscaledTime = 3.5f;
            check(Near(CombatMatchRuntime.RefillRoleScale(zombie), 1.2765878874031307) &&
                Near(CombatMatchRuntime.IncomingRoleScale(zombie), 0.8893648450387477) &&
                CombatMatchRuntime.RoleRevision == revision, "Pending infection counts immediately; smoothing does not invalidate budgets");
            At(4);
            check(!human.IsDead && CombatMatchRuntime.IsActiveZombie(human) &&
                Near(CombatMatchRuntime.RefillRoleScale(human), CombatMatchRuntime.RefillRoleScale(zombie)),
                "Revived Zombie inherits shared smoothed values without resetting the curve");
            var environmental = all.First(p => p != zombie && p != human);
            double prior = CombatMatchRuntime.RefillRoleScale(zombie);
            environmental.Die(0, null);
            check(Near(CombatMatchRuntime.RefillRoleScale(zombie), prior), "Nonconverting death cannot manipulate ratio");
            At(6);
            check(Near(CombatMatchRuntime.RefillRoleScale(zombie), 1.1408265603210406),
                "Three seconds approaches new composition without abrupt jump");
            SettingsActions.Execute("/one zombie any-death on");
            environmental.Die(0, null);
            double secondStart = CombatMatchRuntime.RefillRoleScale(zombie);
            At(7);
            check(CombatMatchRuntime.RefillRoleScale(zombie) < secondStart &&
                CombatMatchRuntime.RefillRoleScale(environmental) == CombatMatchRuntime.RefillRoleScale(zombie),
                "Opt-in environmental conversion uses the same curve");
            var manual = all.First(p => !CombatMatchRuntime.IsActiveZombie(p));
            manual.Die(0, null);
            double beforeRevive = CombatMatchRuntime.RefillRoleScale(zombie);
            manual.Revive(100);
            check(CombatMatchRuntime.IsActiveZombie(manual) && Near(CombatMatchRuntime.RefillRoleScale(manual), beforeRevive),
                "Manual revival activates current smoothed value without resetting team balance");
            check(SettingsActions.Execute("/one zombie resetkda").Success &&
                Near(CombatMatchRuntime.RefillRoleScale(zombie), beforeRevive),
                "KDA reset keeps the active team composition and smoothing state");
            // No extra messages/roster allocations from the role-query hot path.
            var remainingHuman = all.First(p => !CombatMatchRuntime.IsActiveZombie(p));
            QueryRoles(zombie); QueryRoles(remainingHuman);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            QueryRoles(zombie); QueryRoles(remainingHuman);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Console.WriteLine($"Dynamic Zombie/Human role queries: {bytes} bytes/20000 pairs");
            check(bytes == 0, "Dynamic role queries allocate no memory");
            var old = human.spawner;
            FriendlyFireParticipants.Retire(old); PlayerSpawner.MultiplayerList.Remove(old);
            var replacement = add(human.netId).PlayerAvatar;
            At(7.2f);
            check(!CombatMatchRuntime.IsRunning && CombatMatchRuntime.RefillRoleScale(replacement) == 1 &&
                CombatMatchRuntime.IncomingRoleScale(zombie) == 1, "Reconnection cancels dynamic state with the shared roster");
            foreach (string end in new[] { "selection", "toggle", "run", "authority", "timeout", "conversion" })
            {
                all = New(2); Begin(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
                if (end == "selection") FriendlyFireParticipants.ResetSelection();
                else if (end == "toggle") SettingsActions.Execute("/one friendlyfire off");
                else if (end == "run") SaveManager.CurrentRun = new();
                else if (end == "authority") Mirror.NetworkServer.active = false;
                else if (end == "conversion")
                {
                    human = all.First(p => p != zombie);
                    hit = new DamageInstance(); FriendlyFireRuntime.Attribute(zombie, human, hit); human.Die(0, hit);
                }
                At(end == "timeout" ? 123 : 3.1f);
                check(!CombatMatchRuntime.IsRunning && all.All(p => CombatMatchRuntime.RefillRoleScale(p) == 1 &&
                    CombatMatchRuntime.IncomingRoleScale(p) == 1), "Dynamic modifiers removed on " + end);
            }
        }
        finally { CombatMatchRuntime.Stop(true, false); harmony.UnpatchAll("Zombie.dynamic.tests"); ReviveAllHooks.Uninstall(); }
        start();
    }

    private static void QueryRoles(PlayerAvatar zombie)
    {
        for (int i = 0; i < 10000; i++) { CombatMatchRuntime.RefillRoleScale(zombie); CombatMatchRuntime.IncomingRoleScale(zombie); }
    }

    private static void Curve(Action<bool, string> check)
    {
        bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;
        var curve = new ZombieDynamicBalance();
        check(curve.RefillScale(0) == 1 && curve.IncomingScale(0) == 1, "Absent match has neutral dynamic policy");
        foreach (var example in new[]
        {
            (z: 1, h: 1, refill: 1d, incoming: 1d),
            (z: 1, h: 5, refill: 37d / 27, incoming: 23d / 27),
            (z: 5, h: 1, refill: 19d / 27, incoming: 13d / 9),
            (z: 8, h: 1, refill: 2371d / 3645, incoming: 1852d / 1215)
        })
        {
            curve = default; curve.Retarget(example.z, example.h, 0);
            check(Near(curve.RefillScale(0), example.refill) && Near(curve.IncomingScale(0), example.incoming),
                $"Minority curve target {example.z}:{example.h} is neutral at parity and favors the outnumbered side");
            check(Near(curve.HumanIncomingScale(0), example.z > example.h ? example.refill : 1),
                "Only a Human minority receives incoming-damage compensation");
        }
        for (int zombies = 1; zombies <= 100; zombies++)
        {
            double last = .6, lastIncoming = 1.6, lastHuman = .6;
            for (int humans = 1; humans <= 100; humans++)
            {
                curve = default; curve.Retarget(zombies, humans, 0);
                double refill = curve.RefillScale(0), incoming = curve.IncomingScale(0);
                check(refill >= last && refill <= 1.5 && incoming >= .8 && incoming <= lastIncoming &&
                    curve.HumanIncomingScale(0) >= lastHuman && curve.HumanIncomingScale(0) <= 1,
                    "Ratio curve is bounded and monotone");
                last = refill; lastIncoming = incoming; lastHuman = curve.HumanIncomingScale(0);
            }
        }
        curve = default; curve.Retarget(int.MaxValue, int.MaxValue, 0);
        check(curve.RefillScale(0) == 1 && curve.IncomingScale(0) == 1 && curve.HumanIncomingScale(0) == 1, "Large counts cannot overflow total");
        curve = default; curve.Retarget(1, 5, 0); curve.Retarget(2, 4, 1);
        double initial = curve.RefillScale(1);
        check(Near(initial, 37d / 27), "Retarget does not jump");
        var partitioned = curve;
        for (int i = 1; i <= 20; i++) partitioned.RefillScale(1 + i / 10d);
        check(Near(curve.RefillScale(3), partitioned.RefillScale(3)), "Smoothing independent of query/frame frequency");
        curve.Retarget(3, 3, 3);
        check(Near(curve.RefillScale(4), 1.0263757016404627), "Repeated conversion settles previous segment");
        double valid = curve.RefillScale(4);
        curve.Retarget(0, 3, 5); curve.Retarget(3, -1, 5); curve.Retarget(1, 5, double.NaN);
        check(Near(curve.RefillScale(double.NaN), valid) && Near(curve.RefillScale(double.PositiveInfinity), valid) &&
            Near(curve.RefillScale(2), valid), "Invalid inputs/backward time cannot corrupt smoothing");
        curve = default; curve.Retarget(1, int.MaxValue, 0);
        check(curve.RefillScale(0) <= 1.5 && curve.IncomingScale(0) >= .8 && curve.HumanIncomingScale(0) == 1, "Extreme outnumbering saturates below bounds");
        curve = default; curve.Retarget(int.MaxValue, 1, 0);
        check(curve.RefillScale(0) >= .6 && curve.IncomingScale(0) <= 1.6 && curve.HumanIncomingScale(0) >= .6,
            "Last-Human compensation stays finite and never grants invulnerability");
        foreach (int humans in new[] { 999999, 1000001 })
        {
            curve = default; curve.Retarget(1000000, humans, 0);
            check(Math.Abs(curve.RefillScale(0) - 1) < 1e-10 && Math.Abs(curve.IncomingScale(0) - 1) < 1e-10 &&
                Math.Abs(curve.HumanIncomingScale(0) - 1) < 1e-10, "No slope jump or bonus step at team parity");
        }
        curve = default; curve.Retarget(1, 5, 0);
        double priorZombie = curve.IncomingScale(0), priorHuman = curve.HumanIncomingScale(0);
        curve.Retarget(5, 1, 0);
        check(curve.IncomingScale(0) == priorZombie && curve.HumanIncomingScale(0) == priorHuman,
            "Crossing parity changes targets without jumping current protection");
        for (int i = 1; i <= 30; i++)
        {
            double now = i / 10d, nextZombie = curve.IncomingScale(now), nextHuman = curve.HumanIncomingScale(now);
            check(nextZombie >= priorZombie && nextHuman <= priorHuman && nextHuman >= .6,
                "Conversions smoothly transfer advantage from Zombies to Humans");
            priorZombie = nextZombie; priorHuman = nextHuman;
        }
    }
}
