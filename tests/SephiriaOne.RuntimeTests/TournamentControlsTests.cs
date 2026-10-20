using Mirror;
using SephiriaOne;

internal static class TournamentControlsTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start(); var guest = add(90);
        UnityEngine.Time.unscaledTime = 0;
        check(SettingsActions.Execute("/one tournament duration 45").Success,
            "Tournament duration uses the shared host command dispatcher");
        check(SettingsActions.Execute("/one tournament").Success && SettingsActions.Execute("/ONE TOURNAMENT STATUS").Success,
            "Tournament default and explicit status commands are recognized");
        check(SettingsActions.Execute("/one help").Messages.Any(x => x.Contains("/one tournament")),
            "Addon help lists the Tournament command family");
        foreach (string value in new[] { "0", "9", "3601", "-10", "+10", "NaN", "10.5", "x2", "2147483648", "" })
            check(!SettingsActions.Execute("/one tournament duration " + value).Success,
                "Tournament rejects invalid duration: " + value);
        foreach (string command in new[] { "start extra", "stop extra", "status extra", "help extra", "resetkda extra", "unknown" })
            check(!SettingsActions.Execute("/one tournament " + command).Success,
                "Tournament rejects malformed command: " + command);
        check(SettingsActions.Execute("/one tournament duration 10").Success &&
            SettingsActions.Execute("/one tournament duration 3600").Success,
            "Tournament accepts both duration bounds");

        SettingsActions.Execute("/one tournament duration 45");
        SettingsActions.Execute("/one friendlyfire damage 0.1");
        check(SettingsActions.Execute("/one save").Success, "Tournament duration can be saved");
        string preset = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(preset.StartsWith("SephiriaOne preset v20\n") && preset.Contains("tournament duration 45") &&
            SessionPolicy.TryReadPreset(preset, out var saved, out _) && saved.ToPresetText() == preset,
            "Tournament duration round-trips in preset v20 together with fractional combat settings");
        for (int version = 1; version <= 19; version++)
        {
            check(SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\n", out saved, out _) &&
                !saved.DescribeSettings().Any(x => x.StartsWith("tournament ")),
                "Old preset keeps default Tournament duration: v" + version);
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v" + version + "\ntournament duration 45\n", out _, out _),
                "Old preset rejects unsupported Tournament field: v" + version);
        }
        foreach (string body in new[] { "tournament duration 9", "tournament duration 3601", "tournament duration 30.0",
            "tournament duration +30", "tournament duration 030", "tournament seconds 30", "tournament duration 10\ntournament duration 20" })
            check(!SessionPolicy.TryReadPreset("SephiriaOne preset v20\n" + body + "\n", out _, out _),
                "Preset rejects invalid Tournament field: " + body);
        check(SessionPolicy.TryReadPreset("SephiriaOne preset v20\ntournament duration 30\n", out saved, out _) && !saved.HasChanges,
            "Default Tournament duration is omitted from saved changes");
        SessionPolicy.TryReadPreset(preset, out saved, out _); saved.Clear();
        check(!saved.HasChanges && !saved.ToPresetText().Contains("tournament"), "Policy clear restores default Tournament duration");

        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "test");
        guest.connectionToClient.isReady = false;
        check(SettingsActions.Execute("/one tournament duration 60").Success,
            "Tournament duration remains available during unrelated faults and guest initialization");
        NetworkServer.active = false;
        check(SettingsActions.Execute("/one tournament help").Success, "Tournament help is available to guests");
        foreach (string command in new[] { "status", "duration 30", "start", "stop", "resetkda" })
            check(!SettingsActions.Execute("/one tournament " + command).Success,
                "Guest cannot control Tournament: " + command);
        NetworkServer.active = true;

        host = start(); guest = add(90); UnityEngine.Time.unscaledTime = 0;
        check(SettingsActions.Execute("/one deathmatch start").Success, "Prepare Deathmatch for mode isolation");
        check(!SettingsActions.Execute("/one tournament start").Success &&
            !SettingsActions.Execute("/one tournament stop").Success && CombatMatchRuntime.IsRunning,
            "Tournament controls cannot replace or stop Deathmatch");
        SettingsActions.Execute("/one deathmatch stop");
        check(SettingsActions.Execute("/one tournament start").Success, "Host can start Tournament");
        check(!SettingsActions.Execute("/one deathmatch start").Success &&
            !SettingsActions.Execute("/one deathmatch stop").Success && CombatMatchRuntime.IsRunning,
            "Deathmatch controls cannot replace or stop Tournament");
        check(SettingsActions.Execute("/one tournament resetkda").Success && CombatMatchRuntime.IsRunning,
            "Tournament KDA reset preserves the match");
        check(SettingsActions.Execute("/one tournament stop").Success && !CombatMatchRuntime.IsRunning,
            "Tournament stop ends its own mode");
        host = start();
        check(SettingsActions.Execute("/one tournament status").Messages.Any(x => x.Contains("30")),
            "Fresh sessions use the default 30-second Tournament duration");

        string languageFolder = Path.Combine(Path.GetTempPath(), "SephiriaOne-TournamentLanguage-" + Guid.NewGuid().ToString("N"));
        try
        {
            L.Initialize(languageFolder, _ => { });
            check(L.TrySetLanguage("ko", out _), "Select Korean for Tournament controls");
            host = start(); guest = add(90); UnityEngine.Time.unscaledTime = 0;
            host.PlayerAvatar.playerNameSource = "Host"; guest.PlayerAvatar.playerNameSource = "Guest";
            check(SettingsActions.Execute("/one tournament duration 10").Messages.Contains("다음 토너먼트의 라운드 시간을 10초로 설정했습니다.") &&
                SettingsActions.Execute("/one tournament help").Messages.Any(x => x.Contains("토너먼트") && x.Contains("resetkda")) &&
                L.T("Tournament") == "토너먼트" && L.T("Round duration in seconds (10–3600)") == "라운드 시간 (10–3600초)",
                "Tournament help, duration feedback and panel labels support Korean");
            check(SettingsActions.Execute("/one tournament start").Success &&
                DungeonManager.Instance.ChatMessages.Any(x => x.Message == "[토너먼트] 라운드 1 | 상대는 \"Guest\" 님입니다! 경기 시작까지: 3..."),
                "Tournament sends localized opponent countdown through native chat");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            check(DungeonManager.Instance.ChatMessages.Any(x => x.Message == "[토너먼트] 경기 시작: \"Guest\" 님을 처치하세요! (10초)"),
                "Tournament sends localized round-start text with player and duration placeholders");
            UnityEngine.Time.unscaledTime = 4; CombatMatchRuntime.Tick();
            foreach (var player in new[] { host.PlayerAvatar, guest.PlayerAvatar })
                check(DungeonManager.Instance.ChatMessages.Any(x => x.Player == player && x.Message ==
                    $"[토너먼트] 상대: \"{(player == host.PlayerAvatar ? "Guest" : "Host")}\" | 남은 시간: 9초"),
                    "Both fighters receive localized live opponent and time status");
            UnityEngine.Time.unscaledTime = 13; CombatMatchRuntime.Tick();
            check(DungeonManager.Instance.ChatMessages.Count(x => x.Message == "[토너먼트] 경기가 종료되었습니다.") == 2,
                "Korean terminal notice replaces both expired match clocks");
            UnityEngine.Time.unscaledTime = 13.25f; CombatMatchRuntime.Tick();
            UnityEngine.Time.unscaledTime = 14.25f; CombatMatchRuntime.Tick();
            UnityEngine.Time.unscaledTime = 15.25f; CombatMatchRuntime.Tick();
            foreach (var player in new[] { host.PlayerAvatar, guest.PlayerAvatar })
            foreach (int seconds in new[] { 3, 2, 1 })
                check(DungeonManager.Instance.ChatMessages.Any(x => x.Player == player && x.Message ==
                    $"[토너먼트] 라운드 1 | 재대결 | 상대는 \"{(player == host.PlayerAvatar ? "Guest" : "Host")}\" 님입니다! 경기 시작까지: {seconds}..."),
                    "Korean draw rematch includes round and 재대결 for both players throughout the countdown");
            check(SettingsActions.Execute("/one tournament resetkda").Messages.Contains("토너먼트 K/D/A 기록을 초기화했습니다.") &&
                DungeonManager.Instance.ChatMessages.Any(x => x.Message == "토너먼트 K/D/A 기록을 초기화했습니다."),
                "Tournament score reset feedback and native guest notice support Korean");
            check(SettingsActions.Execute("/one tournament stop").Messages.Contains("토너먼트를 중지했습니다.") &&
                DungeonManager.Instance.ChatMessages.Any(x => x.Message == "토너먼트 종료. 상위 5명 (킬/데스/어시스트):"),
                "Tournament stop feedback and standings support Korean");
        }
        finally { L.Shutdown(); }
    }
}
