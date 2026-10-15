using Mirror;
using SephiriaOne;

internal static class PlayTagControlsTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start(); var guest = add(90);
        UnityEngine.Time.unscaledTime = 0;
        check(SettingsActions.Execute("/one tag chaser-refill x3.25").Success &&
            SettingsActions.Execute("/one tag chaser-damage 45.5%").Success,
            "Play Tag chaser settings use the shared host dispatcher");
        check(SessionSettings.PlayTagChaser.RefillMultiplier == 3.25m && SessionSettings.PlayTagChaser.IncomingPercent == 45.5m,
            "Chaser commands retain exact configured decimals");
        long chaserRevision = CombatMatchRuntime.ChaserRevision;
        check(SettingsActions.Execute("/ONE TAG CHASER-REFILL X3.25").Success && CombatMatchRuntime.ChaserRevision == chaserRevision,
            "Case-insensitive equivalent chaser settings do not advance runtime revision");
        check(SettingsActions.Execute("/one tag chaser-refill off").Success && !SessionSettings.PlayTagChaser.RefillEnabled &&
            SessionSettings.PlayTagChaser.RefillMultiplier == 3.25m && SessionSettings.PlayTagChaser.IncomingEnabled &&
            CombatMatchRuntime.ChaserRevision != chaserRevision, "Refill toggle is independent and retains its configured value");
        check(SettingsActions.Execute("/one tag chaser-damage off").Success && !SessionSettings.PlayTagChaser.IncomingEnabled &&
            SessionSettings.PlayTagChaser.IncomingPercent == 45.5m, "Incoming toggle retains its configured value");
        check(SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("x3.25") && x.Contains("45.5%")),
            "Status includes configured chaser values even when disabled");
        check(SettingsActions.Execute("/one save").Success, "Chaser settings can be saved");
        string chaserSaved = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(chaserSaved.StartsWith("SephiriaOne preset v26\n") && SessionPolicy.TryReadPreset(chaserSaved, out var chaserPolicy, out _) &&
            chaserPolicy.PlayTagChaser.Equals(SessionSettings.PlayTagChaser) && !chaserSaved.Contains("carrier") && !chaserSaved.Contains("deadline"),
            "Saved chaser settings preserve intent only in v26");
        check(SettingsActions.Execute("/one tag chaser-refill reset").Success && SessionSettings.PlayTagChaser.RefillEnabled &&
            SessionSettings.PlayTagChaser.RefillMultiplier == 2 && !SessionSettings.PlayTagChaser.IncomingEnabled &&
            SessionSettings.PlayTagChaser.IncomingPercent == 45.5m, "Refill reset restores only refill enabled/default state");
        check(SettingsActions.Execute("/one tag chaser-damage reset").Success && SessionSettings.PlayTagChaser.Equals(default(PlayTagChaserSettings)),
            "Incoming reset restores enabled/default state");
        foreach (string command in new[] { "chaser-refill 0", "chaser-refill 10.01", "chaser-refill 1.001", "chaser-refill 2.000",
            "chaser-refill +2", "chaser-refill -1", "chaser-refill NaN", "chaser-refill 2%", "chaser-refill x", "chaser-refill 1e1",
            "chaser-damage -1", "chaser-damage 100.01", "chaser-damage 75.001", "chaser-damage 75.000", "chaser-damage x2",
            "chaser-damage +75", "chaser-damage 75%%", "chaser-refill", "chaser-damage", "chaser-refill on extra", "chaser-damage reset extra" })
            check(!SettingsActions.Execute("/one tag " + command).Success && SessionSettings.PlayTagChaser.Equals(default(PlayTagChaserSettings)),
                "Invalid chaser command cannot mutate configuration: " + command);
        foreach (string command in new[] { "chaser-refill 0.1", "chaser-refill x10", "chaser-damage 0%", "chaser-damage 100", "chaser-refill off",
            "chaser-refill on", "chaser-damage off", "chaser-damage on", "chaser-refill reset", "chaser-damage reset" })
            check(SettingsActions.Execute("/one tag " + command).Success, "Chaser bounds and toggles accepted: " + command);
        check(SettingsActions.Execute("/one tag duration 45").Success, "Play Tag duration uses the shared host dispatcher");
        check(SettingsActions.Execute("/one tag").Success && SettingsActions.Execute("/ONE TAG STATUS").Success,
            "Play Tag default and explicit status commands are recognized");
        check(SettingsActions.Execute("/one help").Messages.Any(x => x.Contains("/one tag")), "Addon help lists Play Tag");
        foreach (string value in new[] { "0", "2", "3601", "-3", "+3", "NaN", "3.5", "x2", "2147483648", "" })
            check(!SettingsActions.Execute("/one tag duration " + value).Success, "Play Tag rejects invalid duration: " + value);
        foreach (string command in new[] { "start extra", "stop extra", "status extra", "help extra", "resetkda extra", "duration reset extra", "unknown" })
            check(!SettingsActions.Execute("/one tag " + command).Success, "Play Tag rejects malformed command: " + command);
        check(SettingsActions.Execute("/one tag duration 3").Success && SettingsActions.Execute("/one tag duration 3600").Success,
            "Play Tag accepts both fuse bounds");
        check(SettingsActions.Execute("/one tag duration reset").Success &&
            SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("30")), "Play Tag duration reset restores the default fuse");
        SettingsActions.Execute("/one tag duration 45");
        check(SettingsActions.Execute("/one save").Success, "Play Tag duration can be saved");
        string preset = File.ReadAllText(Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt"));
        check(preset.StartsWith("SephiriaOne preset v25\n") && preset.Contains("tag duration 45") &&
            SessionPolicy.TryReadPreset(preset, out var saved, out _) && saved.ToPresetText() == preset,
            "Play Tag duration round-trips in preset v25");
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "test");
        guest.connectionToClient.isReady = false;
        check(SettingsActions.Execute("/one tag duration 60").Success,
            "Play Tag duration remains configurable during unrelated faults and guest initialization");
        check(SettingsActions.Execute("/one tag chaser-refill 4").Success && SettingsActions.Execute("/one tag chaser-damage 50").Success,
            "Chaser configuration stays available during unrelated faults and guest initialization");
        NetworkServer.active = false;
        check(SettingsActions.Execute("/one tag help").Success, "Play Tag help is available to guests");
        foreach (string command in new[] { "status", "duration 30", "duration reset", "start", "stop", "resetkda", "chaser-refill on", "chaser-damage off", "chaser-refill 2", "chaser-damage reset" })
            check(!SettingsActions.Execute("/one tag " + command).Success, "Guest cannot control Play Tag: " + command);
        NetworkServer.active = true;

        host = start(); guest = add(90); UnityEngine.Time.unscaledTime = 0;
        check(SettingsActions.Execute("/one tag duration 10").Success && SettingsActions.Execute("/one tag start").Success,
            "Prepare a match with a captured ten-second fuse");
        UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
        UnityEngine.Time.unscaledTime = 4; CombatMatchRuntime.Tick();
        var score = FriendlyFireKda.For(host.PlayerAvatar); score.Kills = 3; score.Deaths = 2; score.Assists = 1;
        long scoreEpoch = FriendlyFireKda.Epoch;
        string matchBeforeEdit = CombatMatchRuntime.DescribePlayTag();
        check(SettingsActions.Execute("/one tag chaser-refill x4").Success && SettingsActions.Execute("/one tag chaser-damage 25%").Success &&
            CombatMatchRuntime.IsPlayTag && SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("Initial fuse: 10s")),
            "Live chaser configuration preserves match and captured fuse");
        check(FriendlyFireKda.Epoch == scoreEpoch && score.Kills == 3 && score.Deaths == 2 && score.Assists == 1 &&
            CombatMatchRuntime.DescribePlayTag() == matchBeforeEdit,
            "Live chaser changes preserve KDA, receipts and exact remaining fuse");
        check(SettingsActions.Execute("/one tag duration 45").Success && SessionSettings.PlayTagDuration == 45 &&
            SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("Initial fuse: 10s")),
            "Duration changes leave the current match's captured fuse unchanged");
        check(SettingsActions.Execute("/one tag duration reset").Success && SessionSettings.PlayTagDuration == 30 &&
            SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("Initial fuse: 10s")),
            "Duration reset leaves the current match's captured fuse unchanged");
        SettingsActions.Execute("/one tag stop");
        check(SettingsActions.Execute("/one tag start").Success &&
            SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("Initial fuse: 30s")),
            "The next match captures the restored default fuse");
        SettingsActions.Execute("/one tag stop");

        foreach (string mode in new[] { "deathmatch", "tournament" })
        {
            host = start(); guest = add(90); UnityEngine.Time.unscaledTime = 0;
            check(SettingsActions.Execute("/one " + mode + " start").Success, "Prepare " + mode + " for Tag isolation");
            check(!SettingsActions.Execute("/one tag start").Success && !SettingsActions.Execute("/one tag stop").Success && CombatMatchRuntime.IsRunning,
                "Play Tag cannot replace or stop " + mode);
            SettingsActions.Execute("/one " + mode + " stop");
            check(SettingsActions.Execute("/one tag start").Success, "Host can start Play Tag");
            check(!SettingsActions.Execute("/one " + mode + " start").Success && !SettingsActions.Execute("/one " + mode + " stop").Success && CombatMatchRuntime.IsRunning,
                mode + " cannot replace or stop Play Tag");
            check(SettingsActions.Execute("/one tag resetkda").Success && CombatMatchRuntime.IsRunning,
                "Play Tag KDA reset preserves the active match");
            check(SettingsActions.Execute("/one tag stop").Success && !CombatMatchRuntime.IsRunning, "Play Tag stop ends its own mode");
        }
        string languageFolder = Path.Combine(Path.GetTempPath(), "SephiriaOne-PlayTagLanguage-" + Guid.NewGuid().ToString("N"));
        try
        {
            L.Initialize(languageFolder, _ => { });
            check(L.TrySetLanguage("ko", out _), "Select Korean for Play Tag controls");
            host = start(); guest = add(90);
            check(SettingsActions.Execute("/one tag duration 10").Messages.Contains("다음 술래잡기의 폭탄 시간을 10초로 설정했습니다.") &&
                SettingsActions.Execute("/one tag help").Messages.Any(x => x.Contains("술래잡기") && x.Contains("/one tag")) &&
                L.T("Play Tag") == "술래잡기" && L.T("Initial fuse in seconds (3–3600)") == "처음 폭탄 시간 (3–3600초)",
                "Play Tag feedback, help, and panel labels support Korean");
            check(SettingsActions.Execute("/one tag duration reset").Messages.Contains("다음 술래잡기의 폭탄 시간을 30초로 설정했습니다."),
                "Play Tag default fuse reset feedback supports Korean");
            check(SettingsActions.Execute("/one tag chaser-refill off").Messages.Any(x => x.Contains("술래") && x.Contains("꺼짐")) &&
                SettingsActions.Execute("/one tag chaser-damage 50%").Messages.Any(x => x.Contains("50%") && x.Contains("받는 피해")) &&
                L.T("Refill (x)") == "충전 (배율)" && L.T("Incoming (%)") == "받는 피해 (%)",
                "Chaser feedback and compact controls support Korean");
            host.PlayerAvatar.playerNameSource = "Host"; guest.PlayerAvatar.playerNameSource = "Guest";
            UnityEngine.Time.unscaledTime = 0;
            check(SettingsActions.Execute("/one tag duration 3").Success && SettingsActions.Execute("/one tag start").Success,
                "Start Korean Play Tag countdown");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            check(DungeonManager.Instance.ChatMessages.Any(x => x.Message == "술래잡기 시작!") &&
                DungeonManager.Instance.ChatMessages.Any(x => x.Message == "폭탄: 3초. 다른 플레이어를 처치해 폭탄을 넘기세요!"),
                "Play Tag sends Korean start and carrier instructions through native chat");
            check(SettingsActions.Execute("/one tag resetkda").Messages.Contains("술래잡기 K/D/A 기록을 초기화했습니다."),
                "Play Tag KDA reset feedback supports Korean");
            UnityEngine.Time.unscaledTime = 6; CombatMatchRuntime.Tick();
            check(DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("폭탄을 넘기기 전에 폭발했습니다.")) &&
                DungeonManager.Instance.ChatMessages.Any(x => x.Message == "술래잡기 종료. 상위 5명 (킬/데스/어시스트):"),
                "Play Tag initial explosion and standings support Korean without a fabricated passer");
            check(L.F("'{0}' was blown up by '{1}'s bomb.", "Victim", "Passer") ==
                "'Victim'가 'Passer'의 폭탄에 의해 폭사했습니다.",
                "Korean explosion notice preserves victim and last-passer identities");
        }
        finally { L.Shutdown(); }

        start();
        string presetFolder = Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne");
        Directory.CreateDirectory(presetFolder);
        File.WriteAllText(Path.Combine(presetFolder, "session-preset.txt"),
            "SephiriaOne preset v26\ntag chaser-refill 4\ntag chaser-damage-enabled 0\ntag chaser-damage 50\n");
        check(SettingsActions.Execute("/one tag chaser-refill off").Success &&
            SessionSettings.PlayTagChaser.Equals(new PlayTagChaserSettings(false, 4, false, 50)),
            "First command before synchronization preserves loaded preset numbers and sibling option");
        SessionSettings.Stop(); SessionSettings.Start();
        check(SettingsActions.Execute("/one tag status").Messages.Any(x => x.Contains("x4") && x.Contains("50%")),
            "First status command reads the saved chaser settings before synchronization");
    }
}
