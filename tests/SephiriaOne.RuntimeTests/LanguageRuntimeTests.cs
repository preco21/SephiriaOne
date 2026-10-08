using Mirror;
using SephiriaOne;

internal static class LanguageRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne-Language-" + Guid.NewGuid().ToString("N"));
        try
        {
            L.Initialize(folder, _ => { });
            start();
            check(SettingsActions.Execute("/stats luck +10").Success, "English gameplay command establishes current state");
            var before = SessionSettings.ReadSnapshot();
            var canonical = string.Join("\n", before.ActiveSettings);
            NetworkServer.active = false;
            check(SettingsActions.Execute("/one language ko").Success && L.Language == "ko", "Local language can change without host authority");
            check(SettingsActions.Execute("/one language status").Success, "Local language status works without host authority");
            NetworkServer.active = true;
            var after = SessionSettings.ReadSnapshot();
            SnapshotDetailTests.AssertSameValues(after, SessionSettings.ReadSnapshot(includeDiagnostics: false));
            check(after.Revision == before.Revision && canonical == string.Join("\n", after.ActiveSettings), "Language change preserves gameplay revision and canonical preset rows");
            check(SettingsActions.Execute("/one merchant status").Messages.Any(text => text.Contains("방랑 상인")), "Existing merchant status is Korean without replaying gameplay");
            check(!SettingsActions.Execute("/stats luck invalid").Success &&
                SettingsActions.Execute("/stats luck invalid").Messages.Any(text => text.Any(c => c >= '\uac00' && c <= '\ud7a3')), "Korean feedback preserves rejected numeric command semantics");
            foreach (string invalid in new[] { "../ko", "ja", "ko extra", "en extra" })
                check(!SettingsActions.Execute("/one language " + invalid).Success && L.Language == "ko", "Invalid language command cannot mutate language: " + invalid);
            File.WriteAllText(Path.Combine(folder, "config.json"), "{ broken");
            check(!SettingsActions.Execute("/one language reload").Success && L.Language == "ko", "Invalid reload retains last good language");
            File.WriteAllText(Path.Combine(folder, "config.json"), "{\"language\":\"en\"}");
            check(SettingsActions.Execute("/one language reload").Success && L.Language == "en", "Explicit reload reads edited config");
            check(SessionSettings.ReadSnapshot().Revision == before.Revision, "Reload never advances gameplay intent");

            File.WriteAllText(Path.Combine(folder, "en.json"), "{\" Nobody was changed.\":\"\",\"Only the host can inspect or save session settings.\":\"\"}");
            check(L.Reload(out _), "Blank custom entries use per-entry fallback");
            check(L.T(" Nobody was changed.").Length > 0, "Blank suffix cannot break native fallback recovery");
            NetworkServer.active = false;
            check(!SessionSettings.ReadSnapshot().CanMutate, "Custom text cannot enable non-host controls");
            NetworkServer.active = true;
            var penalty = start();
            penalty.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] = -50;
            check(SettingsActions.Execute("/stats cooldown x3").Success && SessionSettings.EnsureFresh() &&
                penalty.PlayerAvatar.customStats["COOLDOWNRECOVERYSPEED"] == -50,
                "Custom translations preserve exact native penalty fallback");
            check(new StatUpdate(-50, 0, "").UsesNativeFallback && !new StatUpdate(-50, 0).UsesNativeFallback,
                "Fallback classification is typed state even when translated reason becomes empty");
            foreach (string language in new[] { "ko", "en" })
            {
                check(L.TrySetLanguage(language, out _), "Select diagnostic language " + language);
                var report = SessionSettings.ReadSnapshot();
                check(report.Lines.Any(line => line.Contains("cooldown=-50" + L.T(" (native fallback)"))) &&
                    report.Lines.Any(line => line.Contains("cooldown / relative-stat: " + L.T("NativeFallback") + L.T(" (revision ") + "1)")),
                    "Diagnostic formatting retains current localization and native fallback suffix: " + language);
                check(SessionSettings.DescribeDisconnect(1).Any(line => line.Contains("cooldown / relative-stat: " + L.T("NativeFallback"))),
                    "Disconnect diagnostics share current language without replaying state: " + language);
            }
        }
        finally { NetworkServer.active = true; L.Shutdown(); }
    }
}
