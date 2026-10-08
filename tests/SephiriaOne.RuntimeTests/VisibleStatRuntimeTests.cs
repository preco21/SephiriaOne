using Mirror;
using SephiriaOne;

internal static class VisibleStatRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        foreach (var stat in StatCatalog.All.Where(s => s.RequiresExpandedPreset))
        {
            var host = start().PlayerAvatar;
            host.customStats[stat.Key] = 10;
            var guestSpawner = add(21); var guest = guestSpawner.PlayerAvatar;
            guest.customStats[stat.Key] = 20;
            bool Command(string text) => SettingsActions.Execute("/stats " + stat.Name + " " + text).Success;
            decimal Value(PlayerAvatar p) => stat.Display(p.GetCustomStatUnsafe(stat.Key));
            check(Command("x2") && Value(host) == (10 + stat.Offset) * 2 && Value(guest) == (20 + stat.Offset) * 2, "Distinct visible-stat baselines " + stat.Name);
            SessionSettings.Synchronize();
            check(Command("x2") && Value(host) == (10 + stat.Offset) * 2, "Repeated command never compounds " + stat.Name);
            PlayerSpawner.MultiplayerList.Remove(guestSpawner); SessionSettings.Synchronize();
            guest = add(21).PlayerAvatar; guest.customStats[stat.Key] = 15;
            SessionSettings.Synchronize();
            check(Value(guest) == (15 + stat.Offset) * 2, "Same-ID re-entry uses fresh native baseline " + stat.Name);
            host.customStats[stat.Key] += 5; // Native costume/menu/passive input changes.
            SessionSettings.Synchronize();
            check(Value(host) == (15 + stat.Offset) * 2, "Native raw changes trigger relative reconciliation " + stat.Name);
            host.customStatsAmp[stat.Key] = 100;
            SessionSettings.Synchronize();
            check(Value(host) == (30 + stat.Offset) * 2, "Native amplifier change preserves displayed multiplier " + stat.Name);
            var snapshot = SessionSettings.ReadSnapshot();
            check(snapshot.Players.Single(p => p.Id == host.netId).Stats[stat.Name] == Value(host), "Snapshot reports menu units " + stat.Name);
            check(SettingsActions.Execute("/one save").Success, "Expanded stats save " + stat.Name);
            HorayModAPI.StartSession(); SessionSettings.Synchronize();
            check(Value(host) == (30 + stat.Offset) * 2, "Second run retains policy without stacking " + stat.Name);
            SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
            check(Value(host) == (30 + stat.Offset) * 2, "Saved policy reload owns only its contribution " + stat.Name);
            check(Command("set 40") && Value(host) == 40 && Value(guest) == 40, "Set uses shared target " + stat.Name);
            check(Command("+10") && Command("+4") && Value(host) == 30 + stat.Offset + 14 && Value(guest) == 15 + stat.Offset + 14, "Set-to-relative switch and accumulated offsets " + stat.Name);
            check(Command("-4") && Value(host) == 30 + stat.Offset + 10, "Subtract changes retained native offset " + stat.Name);
            check(Command("reset") && host.customStats[stat.Key] == 15 && guest.customStats[stat.Key] == 15, "Reset preserves native changes " + stat.Name);
            check(SettingsActions.Execute("/one forget").Success, "Clear saved expanded intent " + stat.Name);
            NetworkServer.active = false;
            check(!Command("x2"), "Guests cannot change visible stat modifiers " + stat.Name);
        }

        BatCostumeHooks.Install();
        try
        {
            var host = start().PlayerAvatar;
            host.UpdateCostumeData("Bat", true);
            check(SettingsActions.Execute("/stats hpsteal x2").Success && host.GetCustomStatUnsafe("HPSTEAL") == 10, "Life-steal multiplier sees native Bat costume");
            check(SettingsActions.Execute("/one bat hp-steal on").Success, "Bat reduction can coexist with stats");
            SessionSettings.Synchronize();
            check(host.GetCustomStatUnsafe("HPSTEAL") == 2, "Bat reduction changes native baseline to one");
            check(SettingsActions.Execute("/one bat hp-steal off").Success, "Bat reduction can be disabled");
            SessionSettings.Synchronize();
            check(host.GetCustomStatUnsafe("HPSTEAL") == 10, "Disabling Bat reduction restores five-point native baseline");
            host.UpdateCostumeData("HolyRabbit", true); SessionSettings.Synchronize();
            check(host.GetCustomStatUnsafe("HPSTEAL") == 0, "Switching costume removes native and derived life-steal contribution");
        }
        finally { BatCostumeHooks.Uninstall(); }
    }
}
