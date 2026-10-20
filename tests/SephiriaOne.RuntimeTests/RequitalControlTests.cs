using SephiriaOne;
using Mirror;

// The inventory adapter is exercised by a dedicated native-inventory fixture.
// This suite tests shared command, snapshot and persistence integration.
namespace SephiriaOne
{
    internal static class RequitalRuntime
    {
        internal static bool Available = true;
        internal static bool CanExecute => Available && NetworkServer.active && SessionSettings.EnsureResourceScope();
        internal static void Tick() { }
        internal static void Clear() { }
        internal static bool GiveAll(out string message) { message = "fixture"; return CanExecute; }
    }
}

internal static class RequitalControlTests
{
    internal static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one slates count 3").Success, "Host configures Requital quantity");
        check(SettingsActions.Execute("/one slates auto on").Success, "Host enables automatic Slate loadout");
        var snapshot = SessionSettings.ReadSnapshot();
        check(snapshot.Requital.Automatic && snapshot.Requital.Count == 3 && snapshot.RequitalAvailable, "Snapshot reflects configured gifts");
        check(SettingsActions.Execute("/one save").Success, "Slate loadout saves through shared preset flow");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.Requital.Equals(new RequitalSettings(true, 3)), "New scope restores saved loadout");
        check(SettingsActions.Execute("/one slates give").Success && SettingsActions.Execute("/one slates status").Success, "Manual and status paths recognized");
        RequitalRuntime.Available = false;
        check(!SettingsActions.Execute("/one slates count 4").Success && !SettingsActions.Execute("/one slates give").Success,
            "Compatibility failure blocks adding and giving");
        check(SettingsActions.Execute("/one slates count 2").Success && SettingsActions.Execute("/one slates auto off").Success,
            "Compatibility failure still permits reductions/retraction");
        check(SettingsActions.Execute("/one slates reset").Success && !SessionSettings.Requital.HasChanges, "Reset restores Slate defaults");
        RequitalRuntime.Available = true;
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one slates auto on").Success && !SettingsActions.Execute("/one slates give").Success,
            "Guests cannot grant or change policy");
        NetworkServer.active = true;
        check(SettingsActions.Execute("/one forget").Success, "Remove saved fixture preset");
    }
}
