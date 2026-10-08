using Mirror;
using SephiriaOne;

internal static class UpdateRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        string root = Path.Combine(Path.GetTempPath(), "SephiriaOne-update-runtime-" + Guid.NewGuid().ToString("N"));
        var baseline = SessionSettings.ReadSnapshot();
        using var service = new UpdateCoordinator(new Version(1, 0, 0), Path.Combine(root, "updates.json"),
            _ => Task.FromResult<UpdateRelease>(null), (_, _) => throw new Exception("No implicit installation"), () => new Version(1, 0, 0));
        try
        {
            UpdateFeature.Bind(service);
            NetworkServer.active = false;
            check(SettingsActions.IsCommand("/one update status"), "Update command is consumed before native chat");
            var result = SettingsActions.Execute("/one update status");
            check(result.Recognized && result.Success && !result.OpenPanel, "Local updater status needs no host authority or session UI");
            check(!SettingsActions.Execute("/one update install").Success, "No candidate cannot invoke install");
            check(!SettingsActions.Execute("/one update install extra").Success, "Invalid updater suffix is rejected");
            NetworkServer.active = true;
            check(SessionSettings.ReadSnapshot().Revision == baseline.Revision, "Local updater commands never mutate session settings");
        }
        finally { NetworkServer.active = true; UpdateFeature.Stop(); }
    }
}
