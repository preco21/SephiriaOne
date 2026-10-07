using SephiriaOne;

internal static class NameLocalizationRuntimeTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne-NameLanguage-" + Guid.NewGuid().ToString("N"));
        var translations = new Dictionary<string, string>
        {
            ["native name {0}: {1}"] = "게임 이름 {0}: {1}",
            ["WaitingForReadiness"] = "준비 대기 중",
            ["Suspended"] = "일시 중단",
            ["waiting for owned avatar"] = "본인 캐릭터 대기 중",
            ["awaiting native name readback"] = "게임 이름 반영 확인 대기 중",
            ["native local readback matches; peer rendering unverified"] = "로컬 게임 이름 반영 확인됨; 다른 플레이어의 표시 여부는 미확인",
            ["native name acknowledgment timed out after 3 attempts; peer rendering unverified"] = "3회 시도 후 게임 이름 반영 확인 시간 초과; 다른 플레이어의 표시 여부는 미확인",
            ["Required state or authority is not ready."] = "필요한 상태 또는 권한이 준비되지 않았습니다."
        };
        var name = new MultiplayerNameColor();
        try
        {
            L.Initialize(folder, _ => { });
            check(L.TrySetLanguage("en", out _), "Name diagnostic transition starts in English");
            File.WriteAllText(Path.Combine(folder, "ko.json"), System.Text.Json.JsonSerializer.Serialize(translations));
            check(L.Reload(out _), "Name diagnostic test catalog loads");
            var host = start();
            _ = add(2, 7, 2, 0);
            host.PlayerAvatar.playerNameSource = "Alice";
            SaveManager.Current = new();
            SaveManager.Current.SetString("PlayerName", "Alice");
            UnityEngine.Time.unscaledTime = 0;
            name.Update(host);
            const string englishWaiting = "native name WaitingForReadiness: awaiting native name readback";
            check(name.Status == englishWaiting, "English waiting diagnostic remains byte identical");
            int requests = host.PlayerAvatar.NameRequests.Count;
            check(L.TrySetLanguage("ko", out _) && name.Status == "게임 이름 준비 대기 중: 게임 이름 반영 확인 대기 중",
                "Reading a cached diagnostic follows the new language without updating name state");
            check(host.PlayerAvatar.NameRequests.Count == requests, "Language selection and diagnostic reads do not send names");
            string cached = name.Status;
            for (int i = 0; i < 1000; i++) { name.Update(host); _ = name.Status; }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 5000; i++) { name.Update(host); _ = name.Status; }
            check(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024 && ReferenceEquals(cached, name.Status) &&
                host.PlayerAvatar.NameRequests.Count == requests, "Translated name diagnostics allocate no repeated strings between native retry deadlines");
            translations["awaiting native name readback"] = "수정된 이름 확인 대기";
            File.WriteAllText(Path.Combine(folder, "ko.json"), System.Text.Json.JsonSerializer.Serialize(translations));
            check(L.Reload(out _) && name.Status == "게임 이름 준비 대기 중: 수정된 이름 확인 대기" &&
                host.PlayerAvatar.NameRequests.Count == requests, "Catalog revision refreshes cached diagnostics without reconciliation");
            check(L.TrySetLanguage("en", out _) && name.Status == englishWaiting,
                "Switching back restores the exact English waiting diagnostic");
            host.PlayerAvatar.playerNameSource = host.PlayerAvatar.NameRequests.Last();
            name.Update(host);
            check(name.Status == "native local readback matches; peer rendering unverified", "Acknowledged English diagnostic is unchanged");
            check(L.TrySetLanguage("ko", out _) && name.Status == translations["native local readback matches; peer rendering unverified"],
                "Acknowledged source text refreshes without another name poll");
            name.Restore();
            check(name.Status == "본인 캐릭터 대기 중", "Restored inactive name diagnostic translates");
        }
        finally { name.Restore(); L.Shutdown(); }
    }
}
