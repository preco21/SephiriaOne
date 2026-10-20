using SephiriaOne;
using Mirror;

// The inventory adapter is exercised by a dedicated native-inventory fixture.
// This suite tests shared command, snapshot and persistence integration.
namespace SephiriaOne
{
    internal static class RequitalRuntime
    {
        internal static bool Available = true;
        internal static bool AvailableFor(SlateKind kind) => Available;
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
        check(SettingsActions.Execute("/one slates type heaven").Success, "Host can select Heaven");
        check(SettingsActions.Execute("/one slates type reset").Success, "Host can restore the original Requital selection");
        check(SettingsActions.Execute("/one slates count 3").Success, "Host configures Requital quantity");
        check(SettingsActions.Execute("/one slates auto on").Success, "Host enables automatic Slate loadout");
        check(SettingsActions.Execute("/one slates type heaven").Success, "Host switches active Slate loadout");
        var snapshot = SessionSettings.ReadSnapshot();
        check(snapshot.Requital.Automatic && snapshot.Requital.Count == 3 && snapshot.Requital.Kind == SlateKind.Heaven &&
            snapshot.RequitalAvailable, "Snapshot reflects configured gifts");
        check(SettingsActions.Execute("/one save").Success, "Slate loadout saves through shared preset flow");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.Requital.Equals(new RequitalSettings(true, 3, SlateKind.Heaven)), "New scope restores saved loadout");
        check(SettingsActions.Execute("/one slates give").Success && SettingsActions.Execute("/one slates status").Success, "Manual and status paths recognized");
        RequitalRuntime.Available = false;
        check(!SettingsActions.Execute("/one slates count 4").Success && !SettingsActions.Execute("/one slates give").Success,
            "Compatibility failure blocks adding and giving");
        check(!SettingsActions.Execute("/one slates type requital").Success && SessionSettings.Requital.Kind == SlateKind.Heaven,
            "Type replacement is an addition, not a reduction, when automatic grants are enabled");
        check(SettingsActions.Execute("/one slates count 2").Success && SettingsActions.Execute("/one slates auto off").Success,
            "Compatibility failure still permits reductions/retraction");
        check(SettingsActions.Execute("/one slates reset").Success && !SessionSettings.Requital.HasChanges, "Reset restores Slate defaults");
        RequitalRuntime.Available = true;
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one slates auto on").Success && !SettingsActions.Execute("/one slates give").Success,
            "Guests cannot grant or change policy");
        NetworkServer.active = true;
        check(SettingsActions.Execute("/one forget").Success, "Remove saved fixture preset");
        string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne-SlateLanguage-" + Guid.NewGuid().ToString("N"));
        try
        {
            L.Initialize(folder, _ => { });
            check(L.TrySetLanguage("ko", out _), "Select Korean Slate controls");
            check(SettingsActions.Execute("/one slates type heaven").Messages.Any(m => m.StartsWith("천국 석판:")) &&
                SettingsActions.Execute("/one slates help").Messages.Any(m => m.Contains("type requital|heaven|reset")) &&
                L.T("Heaven") == "천국" && L.T("Requital") == "보은" && L.T("Starting Slates") == "시작 석판",
                "Selected Slate name, command help and dropdown names have Korean translations");
            check(SettingsActions.Execute("/one slates type reset").Messages.Any(m => m.StartsWith("보은 석판:")),
                "Korean status follows type reset");
        }
        finally { L.Shutdown(); }
    }
}
