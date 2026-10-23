using SephiriaOne;
using Mirror;

namespace SephiriaOne
{
    internal static class ProjectileLimitFeature { internal static bool Available = true; }
    internal static class ProjectileLimitRuntime
    {
        internal static int Refreshes, Clears;
        internal static void Refresh() { Refreshes++; _ = SessionSettings.ProjectilesForUse; }
        internal static void Clear() { Clears++; }
    }
}

internal static class ProjectileLimitControlTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one projectiles rate on").Success &&
            SettingsActions.Execute("/one projectiles rate total 80").Success &&
            SettingsActions.Execute("/one projectiles rate source 20").Success, "Host can configure rates before active caps are enabled");
        var rateOnly = SessionSettings.ReadSnapshot(SnapshotContent.None).Projectiles;
        check(rateOnly.RateEnabled && !rateOnly.Enabled && rateOnly.PlayerRate == 80 && rateOnly.SourceRate == 20,
            "Compact snapshot exposes rates independently of active caps");
        check(SettingsActions.Execute("/one projectiles on").Success, "Host can enable projectile limits");
        check(SettingsActions.Execute("/one projectiles total 40").Success &&
            SettingsActions.Execute("/one projectiles source default 10").Success &&
            SettingsActions.Execute("/one projectiles source liberation 6").Success, "Host can set both caps and source overrides");
        var snapshot = SessionSettings.ReadSnapshot(SnapshotContent.None);
        check(snapshot.Projectiles.Enabled && snapshot.Projectiles.PlayerLimit == 40 &&
            snapshot.Projectiles.GetSourceLimit("liberation") == 6 &&
            snapshot.Projectiles.GetSourceLimit("meteor") == 10 && snapshot.Projectiles.RateEnabled &&
            snapshot.Projectiles.PlayerRate == 80 && snapshot.Projectiles.SourceRate == 20, "Compact snapshot exposes immutable projectile settings");
        check(ProjectileLimitRuntime.Refreshes >= 4, "Committed edits refresh runtime");
        check(SettingsActions.Execute("/one save").Success, "Projectile intent saves");
        int before = ProjectileLimitRuntime.Refreshes;
        SessionSettings.Stop(); SessionSettings.Start();
        check(SettingsActions.Execute("/one projectiles total 50").Success &&
            SessionSettings.ProjectilesForUse.GetSourceLimit("liberation") == 6 &&
            SessionSettings.ProjectilesForUse.Enabled && SessionSettings.ProjectilesForUse.RateEnabled &&
            SessionSettings.ProjectilesForUse.PlayerRate == 80 && SessionSettings.ProjectilesForUse.SourceRate == 20,
            "Early command composes against loaded preset");
        check(ProjectileLimitRuntime.Refreshes > before && ProjectileLimitRuntime.Clears > 0, "Load refreshes without recursion and teardown clears runtime");
        check(SettingsActions.Execute("/one projectiles status").Success, "Projectile status recognized");
        before = ProjectileLimitRuntime.Refreshes;
        ProjectileLimitFeature.Available = false;
        check(!SettingsActions.Execute("/one projectiles on").Success &&
            !SettingsActions.Execute("/one projectiles total 80").Success &&
            !SettingsActions.Execute("/one projectiles rate on").Success &&
            !SettingsActions.Execute("/one projectiles rate source 30").Success &&
            ProjectileLimitRuntime.Refreshes == before, "Unavailable hooks reject edits without changing runtime");
        check(SettingsActions.Execute("/one projectiles rate off").Success && !SessionSettings.ProjectilesForUse.RateEnabled &&
            SessionSettings.ProjectilesForUse.PlayerRate == 80 && SessionSettings.ProjectilesForUse.SourceRate == 20,
            "Rate off remains available without compatible hooks and preserves configured rates");
        check(SettingsActions.Execute("/one projectiles rate reset").Success &&
            SessionSettings.ProjectilesForUse.Enabled && SessionSettings.ProjectilesForUse.PlayerLimit == 50 &&
            SessionSettings.ProjectilesForUse.GetSourceLimit("liberation") == 6 && !SessionSettings.ProjectilesForUse.HasRateChanges,
            "Rate reset recovery preserves active caps and overrides");
        check(SettingsActions.Execute("/one projectiles off").Success &&
            SessionSettings.ProjectilesForUse.PlayerLimit == 50, "Off remains available and keeps limits");
        check(SettingsActions.Execute("/one projectiles reset").Success &&
            !SessionSettings.ProjectilesForUse.HasChanges, "Reset remains available and clears all limits");
        ProjectileLimitFeature.Available = true;
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one projectiles on").Success &&
            !SettingsActions.Execute("/one projectiles rate on").Success &&
            !SettingsActions.Execute("/one projectiles rate off").Success &&
            !SettingsActions.Execute("/one projectiles status").Success, "Guests cannot change or inspect host projectile policy");
        NetworkServer.active = true;
        check(SettingsActions.Execute("/one forget").Success, "Remove saved projectile fixture");
    }
}
