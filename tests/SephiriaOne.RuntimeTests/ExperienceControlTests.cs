using SephiriaOne;
using Mirror;

namespace SephiriaOne { internal static class ExperienceFeature { internal static bool Available = true; } }

internal static class ExperienceControlTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one xp block on").Success, "Host can block new XP");
        check(SettingsActions.Execute("/one xp rabbit-exempt on").Success, "Host can exempt Wing-Eared Rabbit");
        var active = SessionSettings.ReadSnapshot().ActiveSettings;
        check(active.Contains("xp block 1") && active.Contains("xp rabbit-exempt 1"), "XP toggles share session policy");
        check(SettingsActions.Execute("/one save").Success, "XP settings persist");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("xp block 1"), "Saved XP rule restores on a new session");
        check(SettingsActions.Execute("/one xp status").Success, "XP status recognized");
        ExperienceFeature.Available = false;
        check(!SettingsActions.Execute("/one xp block on").Success, "Unavailable hook rejects activation");
        check(SettingsActions.Execute("/one xp rabbit-exempt reset").Success &&
            !SessionSettings.ReadSnapshot().Experience.RabbitExempt, "Exception can reset independently while hook is unavailable");
        check(!SessionSettings.ReadSnapshot().ExperienceAvailable, "Snapshot reports unavailable XP hook");
        check(SettingsActions.Execute("/one xp block off").Success &&
            !SessionSettings.ReadSnapshot().ActiveSettings.Contains("xp block 1"), "Off immediately removes XP restriction");
        check(SettingsActions.Execute("/one xp reset").Success &&
            !SessionSettings.ReadSnapshot().ActiveSettings.Any(s => s.StartsWith("xp ")), "Reset clears both XP toggles");
        ExperienceFeature.Available = true;
        foreach (string command in new[] { "/one xp block yes", "/one xp rabbit-exempt on extra", "/one xp unknown", "/one xp reset extra" })
            check(!SettingsActions.Execute(command).Success, "Malformed XP command rejected: " + command);
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one xp block on").Success, "Guest cannot change XP policy");
        NetworkServer.active = true;
        check(SettingsActions.Execute("/one forget").Success, "Remove saved XP fixture");
    }
}
