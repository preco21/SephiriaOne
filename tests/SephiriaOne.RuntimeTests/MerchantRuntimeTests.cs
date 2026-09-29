using Mirror;
using SephiriaOne;

internal static class MerchantRuntimeTests
{
    public static void Run(Func<PlayerSpawner> start, Action<bool, string> check)
    {
        start(); MerchantFeature.Available = true; MerchantFeature.RefreshCalls = 0;
        bool Command(string text) => SettingsActions.Execute("/one merchant " + text).Success;
        bool Enabled() => SessionSettings.ReadSnapshot().ActiveSettings.Contains("merchant spawns 1");
        check(Command("papyrus on") && !SessionSettings.ReadSnapshot().MerchantSpawns,
            "Enabling another merchant type preserves the legacy Wandering toggle");
        check(Command("papyrus reset"), "Selected merchant reset restores only that type");
        check(Command("chance 75") && !Enabled() && SessionSettings.ReadSnapshot().ActiveSettings.Contains("merchant chance 75"),
            "Host can set spawn chance independently while merchants remain off");
        check(Command("reset") && !SessionSettings.ReadSnapshot().ActiveSettings.Any(line => line.StartsWith("merchant ")),
            "Reset restores merchant chance to its default and disables spawning");
        check(SessionSettings.ReadSnapshot().MerchantSpawnChance == 25 && SessionSettings.MerchantSpawnChanceForUse == 25,
            "Fresh session and reset expose the 25 percent default");
        foreach (int chance in new[] { 0, 100, 25 })
            check(Command("chance " + chance) && !Enabled() && SessionSettings.MerchantSpawnChanceForUse == chance &&
                SessionSettings.ReadSnapshot().MerchantSpawnChance == chance, "Valid percentage preserves disabled toggle: " + chance);
        MerchantFeature.RefreshCalls = 0;
        bool refreshObservedCommit = false;
        MerchantFeature.OnRefresh = () => refreshObservedCommit = SessionSettings.ReadSnapshot().CanSave && Enabled();
        check(Command("on"), "Host can enable extra merchants through shared controls");
        check(Enabled() && SessionSettings.MerchantSpawnsForUse && MerchantFeature.RefreshCalls == 1 && refreshObservedCommit,
            "Refresh evaluates eligible loaded floors after the command transaction commits");
        MerchantFeature.OnRefresh = null;
        check(SessionSettings.ReadSnapshot().MerchantSpawns && SessionSettings.ReadSnapshot().MerchantsAvailable,
            "Shared panel snapshot exposes merchant intent and availability");
        int calls = MerchantFeature.RefreshCalls;
        long revision = SessionSettings.ReadSnapshot().Revision;
        foreach (string text in new[] { "status", "help", "" }) check(Command(text), "Merchant read-only command: " + text);
        check(SessionSettings.ReadSnapshot().Revision == revision && MerchantFeature.RefreshCalls == calls,
            "Status and help never refresh gameplay or mutate settings");
        check(SettingsActions.Execute("/one help").Messages.Any(line => line.Contains("/one merchant")), "General help exposes merchant controls");
        foreach (string text in new[] { "yes", "1", "x2", "on extra", "reset on", "status extra", "chance", "chance -1", "chance 101", "chance 1.5", "chance +25", "chance 25%", "chance x2", "chance 100 extra" })
            check(!Command(text) && Enabled() && SessionSettings.ReadSnapshot().Revision == revision,
                "Malformed merchant command leaves intent unchanged: " + text);
        check(Command("off") && !Enabled(), "Off stops future merchant spawns");
        check(Command("on") && Command("reset") && !Enabled(), "Reset returns merchant setting to off");
        check(SettingsActions.Execute(" /ONE MERCHANT ON ").Success && Enabled(), "Merchant parser accepts case and whitespace");
        check(Command("chance 75") && Enabled() && Command("off") && SessionSettings.MerchantSpawnChanceForUse == 75 &&
            Command("on") && SessionSettings.MerchantSpawnChanceForUse == 75,
            "Chance changes preserve an enabled toggle and off/on retain the percentage");
        check(SettingsActions.Execute("/one merchant status").Messages.Any(line => line.Contains("75%")), "Status includes the current chance");
        check(SettingsActions.Execute("/one save").Success, "Merchant intent saves with shared preset action");
        check(Command("reset") && !Enabled(), "Reset leaves the saved copy separate");
        calls = MerchantFeature.RefreshCalls;
        DungeonManager.Instance = new DungeonManager();
        check(!Enabled() && MerchantFeature.RefreshCalls == calls, "Snapshot does not load a new dungeon scope or spawn merchants");
        check(SessionSettings.MerchantSpawnsForUse && Enabled() && SessionSettings.MerchantSpawnChanceForUse == 75 && MerchantFeature.RefreshCalls == calls + 1,
            "First native use loads fresh saved merchant intent and refreshes after loading");
        SessionSettings.Synchronize();
        MerchantFeature.Available = false;
        calls = MerchantFeature.RefreshCalls;
        check(!Command("on") && !Command("chance 0") && Enabled() && !SessionSettings.ReadSnapshot().MerchantsAvailable && MerchantFeature.RefreshCalls == calls,
            "Unavailable hooks reject enabling before policy mutation and update the panel snapshot");
        check(Command("status") && SettingsActions.Execute("/one merchant status").Messages.Any(line => line.Contains("unavailable")),
            "Merchant status explains hook availability");
        check(Command("off") && !Enabled() && Command("reset"), "Off and reset remain available with failed compatibility");
        MerchantFeature.Available = true;
        check(Command("on"), "Compatible merchant feature can be enabled again");
        NetworkServer.active = false;
        calls = MerchantFeature.RefreshCalls;
        check(!Command("on") && !Command("chance 50") && !Command("off") && !Command("reset") && !Command("status") && MerchantFeature.RefreshCalls == calls &&
            !SessionSettings.MerchantSpawnsForUse && SessionSettings.MerchantSpawnChanceForUse == 25,
            "Guests cannot inspect or change host merchant policy");
        NetworkServer.active = true;
        SessionSettings.Stop();
        check(!Enabled() && !SessionSettings.MerchantSpawnsForUse, "Unload clears the displayed and native-use merchant policy");

        var pendingHost = start();
        pendingHost.connectionToClient.isReady = false;
        calls = MerchantFeature.RefreshCalls;
        check(!Command("on") && !Enabled() && MerchantFeature.RefreshCalls == calls,
            "The shared command batch requires ready host participants before recording merchant intent");
        pendingHost.connectionToClient.isReady = true;
        check(Command("on") && Enabled(), "Merchant command succeeds once the shared host context is ready");
        check(SettingsActions.Execute("/one rabbit share on").Success && Command("reset") &&
            SessionSettings.RabbitPotionsForUse.Share, "Merchant reset preserves independent Rabbit policy");

        start();
        string presetPath = Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(presetPath));
        File.WriteAllText(presetPath, "SephiriaOne preset v7\nmerchant spawns 1\nmerchant spawns 0\n");
        calls = MerchantFeature.RefreshCalls;
        check(!SessionSettings.MerchantSpawnsForUse && MerchantFeature.RefreshCalls == calls,
            "An invalid saved merchant preset applies no intent and does not refresh gameplay");

        start(); MerchantFeature.Available = true;
        presetPath = Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt");
        check(Command("papyrus chance 65") && Command("papyrus from 3") && Command("papyrus limit 2") &&
            Command("papyrus on") && Command("taz on") && Command("on"), "Independent merchant types accept all configured conditions");
        var detached = SessionSettings.ReadSnapshot();
        check(detached.Merchants.Count == MerchantCatalog.All.Count && detached.Merchants["papyrus"].Equals(new MerchantSettings(true, 65, 3, 2)) &&
            SessionSettings.AnyMerchantSpawnsForUse && SessionSettings.GetMerchantSettingsForUse("papyrus").Equals(detached.Merchants["papyrus"]),
            "Panel snapshots and native-use reads expose each independent merchant setting");
        SnapshotDetailTests.AssertSameValues(detached, SessionSettings.ReadSnapshot(includeDiagnostics: false));
        bool readOnly = false;
        try { ((IDictionary<string, MerchantSettings>)detached.Merchants)["papyrus"] = new MerchantSettings(false, 25); }
        catch (NotSupportedException) { readOnly = true; }
        check(readOnly, "Snapshot merchant collection cannot mutate session policy");
        revision = detached.Revision; calls = MerchantFeature.RefreshCalls;
        var allStatus = SettingsActions.Execute("/one merchant status");
        var typedStatus = SettingsActions.Execute("/one merchant papyrus status");
        check(allStatus.Success && MerchantCatalog.All.All(definition => allStatus.Messages.Any(text => text.Contains(definition.Name))) &&
            typedStatus.Success && typedStatus.Messages.Any(text => text.Contains("Papyrus") && text.Contains("65%")) &&
            typedStatus.Messages.All(text => !text.Contains("Wandering Merchant") && !text.Contains("Taz")) &&
            SessionSettings.ReadSnapshot().Revision == revision && MerchantFeature.RefreshCalls == calls,
            "Bare status covers all types, selected status only its type, and neither replays gameplay");
        foreach (string text in new[] { "unknown on", "papyrus from 0", "papyrus from 1001", "papyrus limit -1", "papyrus limit 1001", "taz chance 101" })
            check(!Command(text) && SessionSettings.ReadSnapshot().Revision == revision && MerchantFeature.RefreshCalls == calls,
                "Invalid typed command leaves every setting and refresh count unchanged: " + text);
        check(Command("papyrus off") && SessionSettings.GetMerchantSettingsForUse("papyrus").Equals(new MerchantSettings(false, 65, 3, 2)) &&
            detached.Merchants["papyrus"].Enabled && Command("reset") && SessionSettings.GetMerchantSettingsForUse("taz").Enabled &&
            !SessionSettings.MerchantSpawnsForUse, "Off retains selected conditions, legacy reset only changes Wandering, and older snapshots stay detached");
        check(Command("papyrus on") && SettingsActions.Execute("/one save").Success &&
            File.ReadAllText(presetPath).StartsWith("SephiriaOne preset v8\n"), "All type settings save atomically as v8");
        check(Command("papyrus reset") && !SessionSettings.GetMerchantSettingsForUse("papyrus").Enabled &&
            SessionSettings.GetMerchantSettingsForUse("taz").Enabled, "Selected reset keeps other enabled types and the saved copy");
        calls = MerchantFeature.RefreshCalls;
        DungeonManager.Instance = new DungeonManager();
        check(SessionSettings.ReadSnapshot().Merchants.Values.All(value => !value.Enabled) && MerchantFeature.RefreshCalls == calls,
            "A snapshot for a replacement session does not load or apply saved merchant types");
        check(SessionSettings.AnyMerchantSpawnsForUse && SessionSettings.GetMerchantSettingsForUse("papyrus").Equals(new MerchantSettings(true, 65, 3, 2)) &&
            SessionSettings.GetMerchantSettingsForUse("taz").Enabled && !SessionSettings.MerchantSpawnsForUse && MerchantFeature.RefreshCalls == calls + 1,
            "First native use restores the complete v8 preset exactly once after the session changes");
        SessionSettings.Synchronize();
        MerchantFeature.Available = false;
        revision = SessionSettings.ReadSnapshot().Revision; calls = MerchantFeature.RefreshCalls;
        check(!Command("papyrus from 5") && !Command("taz limit 3") && !Command("papyrus on") &&
            SessionSettings.ReadSnapshot().Revision == revision && MerchantFeature.RefreshCalls == calls &&
            Command("papyrus off") && Command("papyrus reset") && SessionSettings.GetMerchantSettingsForUse("taz").Enabled,
            "Unavailable hooks reject typed condition changes while selected off/reset preserve independent types");
        MerchantFeature.Available = true;
        NetworkServer.active = false; calls = MerchantFeature.RefreshCalls;
        foreach (string text in new[] { "papyrus on", "papyrus off", "papyrus reset", "papyrus chance 30", "papyrus from 4", "papyrus limit 2", "papyrus status", "status" })
            check(!Command(text), "Guests cannot inspect or mutate typed host settings: " + text);
        check(MerchantFeature.RefreshCalls == calls && !SessionSettings.AnyMerchantSpawnsForUse &&
            SessionSettings.GetMerchantSettingsForUse("papyrus").Equals(MerchantSettings.Defaults(MerchantCatalog.Find("papyrus"))) &&
            SessionSettings.ReadSnapshot().Merchants.Values.All(value => !value.Enabled), "Guest reads expose defaults and do not refresh merchant gameplay");
        NetworkServer.active = true;
        SessionSettings.Stop();
        check(SessionSettings.ReadSnapshot().Merchants.Values.All(value => !value.Enabled) && detached.Merchants["papyrus"].Enabled,
            "Stopping clears every active type without changing earlier snapshots");
        pendingHost = start(); pendingHost.connectionToClient.isReady = false; calls = MerchantFeature.RefreshCalls;
        check(!Command("papyrus on") && !Command("taz from 3") && !SessionSettings.ReadSnapshot().Merchants["papyrus"].Enabled &&
            MerchantFeature.RefreshCalls == calls, "Pending host initialization rejects all typed settings before policy mutation");
    }
}
