using SephiriaOne;

internal static class PvpArtifactRangeControlsTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            UnityEngine.Time.unscaledTime = 0;
            start(); add(2); add(3); add(4); SessionSettings.EnsureResourceScope();
            check(SettingsActions.Execute("/one friendlyfire balance on").Success &&
                SettingsActions.Execute("/one " + mode + " start").Success && CombatMatchRuntime.IsRunning,
                "Start shared mode for range controls: " + mode);
            check(SettingsActions.Execute("/one friendlyfire artifact-range 7.25").Success && CombatMatchRuntime.IsRunning &&
                SessionSettings.FriendlyFireForHit.ArtifactRange.Meters == 7.25m, "Live range edit keeps active mode");
            Mirror.NetworkServer.active = false;
            check(!SettingsActions.Execute("/one friendlyfire artifact-range 2").Success, "Guest cannot change host range");
            Mirror.NetworkServer.active = true;
            check(SettingsActions.Execute("/one friendlyfire artifact-range reset").Success && CombatMatchRuntime.IsRunning &&
                SessionSettings.FriendlyFireForHit.ArtifactRange.Meters == 5, "Range reset preserves mode and restores 5 m");
            check(SettingsActions.Execute("/one " + mode + " stop").Success, "Stop shared mode");
        }
        start();
    }
}
