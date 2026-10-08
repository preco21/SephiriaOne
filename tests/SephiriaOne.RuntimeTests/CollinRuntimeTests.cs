using SephiriaOne;
using Mirror;

namespace SephiriaOne { internal static class CollinFeature { internal static bool Available = true; } }

internal static class CollinRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one collin on").Success, "Host can enable Collin starting gifts");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("collin starting 1"), "Collin intent appears in snapshot");
        check(SettingsActions.Execute("/one save").Success, "Collin saves through shared preset command");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("collin starting 1"), "Saved Collin intent loads into next scope");
        check(SettingsActions.Execute("/one collin status").Success, "Collin status is available");
        check(SettingsActions.Execute("/one collin reset").Success && !SessionSettings.ReadSnapshot().ActiveSettings.Contains("collin starting 1"), "Reset clears current intent");
        CollinFeature.Available = false;
        check(!SettingsActions.Execute("/one collin on").Success && SettingsActions.Execute("/one collin off").Success, "Compatibility failure blocks enable but allows off");
        CollinFeature.Available = true;
        check(!SettingsActions.Execute("/one collin on extra").Success, "Malformed Collin command is rejected");
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one collin on").Success, "Guest cannot change Collin policy"); NetworkServer.active = true;
        check(SettingsActions.Execute("/one forget").Success, "Forget explicit saved policy");
    }
}
