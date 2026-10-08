using Mirror;
using SephiriaOne;

internal static class ItemRestrictionRuntimeTests
{
    public static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start();
        var enabled = SettingsActions.Execute("/one items unlock on");
        check(enabled.Recognized && enabled.Success, "Host can enable given-item unlock using shared commands");
        check(SettingsActions.Execute("/one items status").Success, "Item unlock has a status command");
        check(!SettingsActions.Execute("/one items unlock yes").Success, "Invalid toggle is rejected");
        check(SettingsActions.Execute("/one save").Success, "Item unlock can be saved");
        check(SessionSettings.ReadSnapshot().ItemUnlock && ItemRestrictionFeature.Enabled, "Panel reflects applied item settings");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(ItemRestrictionFeature.Enabled && SessionSettings.ReadSnapshot().ItemUnlock, "Saved item intent restores in a new controller lifetime");
        check(SettingsActions.Execute("/one items reset").Success, "Item unlock reset is available");
        check(!SessionSettings.ReadSnapshot().ItemUnlock, "Reset updates the panel and active policy");
        ItemRestrictionFeature.Fault = "partial preset application";
        check(SettingsActions.Execute("/one items reset").Success && ItemRestrictionFeature.Fault == null,
            "Reset executes restoration even when a failed preset left the enabled flag false");
        foreach (bool target in new[] { true, false })
        {
            start(); SessionSettings.Synchronize();
            if (!target) check(SettingsActions.Execute("/one items unlock on").Success, "Prepare enabled state");
            ItemRestrictionFeature.FailWrite = true;
            check(!SettingsActions.Execute("/one items unlock " + (target ? "on" : "off")).Success, "Failed item write is reported");
            ItemRestrictionFeature.FailWrite = false;
            check(SettingsActions.Execute("/one items reset").Success && !ItemRestrictionFeature.Enabled && ItemRestrictionFeature.Fault == null,
                "Shared journal recovers failed enable or failed disable and then restores default");
        }
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one items unlock on").Success, "Guest cannot change item unlock");
        check(!SettingsActions.Execute("/one items status").Success, "Guest cannot inspect host-only item controls");
    }
}
