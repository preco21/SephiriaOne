using SephiriaOne;

internal static class ZombieStartingCountTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        PlayerAvatar[] New(int count)
        {
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) players.Add(add(i).PlayerAvatar);
            UnityEngine.Time.unscaledTime = 0;
            return players.ToArray();
        }
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        int Zombies(PlayerAvatar[] players) => players.Count(CombatMatchRuntime.IsActiveZombie);
        bool Count(string value) => SettingsActions.Execute("/one zombie count " + value).Success;
        New(1);
        check(Count("3"), "Host can configure starting Zombie count before players join");
        check(!SettingsActions.Execute("/one zombie start").Success, "Count cannot permit a one-player Zombie match");
        check(SettingsActions.Execute("/one save").Success, "Starting count saves through shared settings");
        string text = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(text.StartsWith("SephiriaOne preset v32\n") && text.Contains("zombie count 3\n") &&
            SessionPolicy.TryReadPreset(text, out var policy, out _) && policy.ToPresetText() == text,
            "Starting count uses strict v32 round-trip without runtime teams");
        check(SettingsActions.Execute("/one zombie status").Messages.Any(m => m.Contains("Starting Zombies: 3")), "Status exposes configured starting count");
        foreach (string value in new[] { "0", "-1", "+2", "1.5", "x2", "NaN", "2147483648", "3 extra" })
            check(!Count(value), "Reject malformed starting count: " + value);
        Mirror.NetworkServer.active = false;
        check(!Count("2"), "Only host can edit starting count");
        Mirror.NetworkServer.active = true;
        check(Count("reset") && SettingsActions.Execute("/one save").Success &&
            !File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt")).Contains("zombie count "),
            "Reset restores one without saving an override");
        for (int version = 1; version <= 31; version++)
        {
            check(SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\n", out var old, out _) &&
                !old.ToPresetText().Contains("zombie count "), "Old preset keeps default count: " + version);
            check(!SessionPolicy.TryReadPreset($"SephiriaOne preset v{version}\nzombie count 2\n", out _, out _),
                "Older headers reject starting count: " + version);
        }
        foreach (string value in new[] { "0", "-1", "1.5", "2.0", "02", "2147483648", "2\nzombie count 3" })
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v32\nzombie count " + value + "\n", out _, out _),
                "Strict starting count preset rejects " + value);
        check(SessionPolicy.TryReadPreset(text, out var cleared, out _), "Read count for clear test");
        cleared.Clear(); check(!cleared.HasChanges, "Clear removes starting count");

        foreach (var scenario in new[] { (2, 1, 1), (2, 100, 1), (4, 2, 2), (9, 3, 3), (9, int.MaxValue, 8) })
        {
            var players = New(scenario.Item1);
            check(Count(scenario.Item2.ToString()) && SettingsActions.Execute("/one zombie start").Success, "Start configured Zombie roster");
            At(3);
            check(Zombies(players) == scenario.Item3 && ZombieRoleVisuals.Visible.Count == scenario.Item3,
                "Unique initial Zombies and visuals clamp to roster while retaining one Human");
            foreach (var first in players)
                foreach (var second in players)
                    check(CombatMatchRuntime.AllowsPair(first, second) == (CombatMatchRuntime.IsActiveZombie(first) != CombatMatchRuntime.IsActiveZombie(second)),
                        "Multi-Zombie hostility only crosses team boundaries");
            check(players.Where(CombatMatchRuntime.IsActiveZombie).All(p => CombatMatchRuntime.IncomingRoleScale(p) == .3), "Every starting Zombie inherits role modifiers");
            check(Count("1") && Zombies(players) == scenario.Item3, "Editing count never changes active teams");
            CombatMatchRuntime.Stop(true, false);
            check(ZombieRoleVisuals.Visible.Count == 0, "Stopping clears all initial role visuals");
            check(SettingsActions.Execute("/one zombie start").Success, "Next match uses updated count");
            At(6); check(Zombies(players) == 1, "Next match adopts count without stale teams");
        }
        var selected = New(9);
        check(SettingsActions.Execute("/one friendlyfire player 2 on").Success && SettingsActions.Execute("/one friendlyfire player 4 on").Success && Count("9"), "Select smaller eligible roster");
        check(SettingsActions.Execute("/one zombie start").Success, "Selected match starts");
        var late = add(20).PlayerAvatar;
        At(3);
        check(Zombies(selected) == 1 && !CombatMatchRuntime.AllowsParticipant(late) &&
            selected.Where(CombatMatchRuntime.IsActiveZombie).All(p => p == selected[1] || p == selected[3]), "Clamp and selection exclude spectators and late arrivals");
        var snapshot = New(5);
        check(Count("3") && SettingsActions.Execute("/one zombie start").Success && Count("1"), "Edit count during countdown");
        At(3); check(Zombies(snapshot) == 3, "Countdown retains count captured at Start");
        check(SettingsActions.Execute("/one save").Success, "Saving active match stores next-match count");
        check(!File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt")).Contains("zombie count "),
            "Active clamped count and teams never overwrite requested settings");
        var retry = New(5);
        check(Count("3") && SettingsActions.Execute("/one zombie start").Success, "Start readiness retry case");
        void InterruptActivation()
        {
            if (!SessionSettings.FriendlyFireForHit.Enabled) return;
            SessionSettings.SettingsChanged -= InterruptActivation;
            Count("1");
            retry[0].spawner.connectionToClient.isReady = false;
        }
        SessionSettings.SettingsChanged += InterruptActivation;
        At(3);
        SessionSettings.SettingsChanged -= InterruptActivation;
        check(Zombies(retry) == 0 && !SessionSettings.FriendlyFireForHit.Enabled, "Interrupted activation defers all role assignment");
        retry[0].spawner.connectionToClient.isReady = true;
        At(4); At(7.1f);
        check(Zombies(retry) == 3, "Readiness retry retains captured count despite activation callback edit");
        var combinations = new HashSet<string>();
        for (int seed = 0; seed < 32; seed++)
        {
            var round = new ZombieSurvivalRound(9, 3, new Random(seed));
            var initial = Enumerable.Range(0, 9).Where(round.IsZombie).ToArray();
            check(initial.Length == 3 && round.Humans == 6 && initial.All(i => !round.IsPending(i)),
                "Random subset has exact distinct active membership for seed " + seed);
            combinations.Add(string.Join(",", initial));
        }
        check(combinations.Count > 1, "Initial team is not fixed to roster order across random seeds");
        start();
    }
}
