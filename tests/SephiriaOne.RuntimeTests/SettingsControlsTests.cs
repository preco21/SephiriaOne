using System.Collections;
using Mirror;
using SephiriaOne;

internal static class SettingsControlsTests
{
    private static SettingsActionResult Execute(string command) => SettingsActions.Execute(command);
    private static SettingsSnapshot Snapshot(bool refresh = false)
    {
        var full = SessionSettings.ReadSnapshot(refresh);
        SnapshotDetailTests.AssertSameValues(full, SessionSettings.ReadSnapshot(includeDiagnostics: false));
        return full;
    }
    private static bool IsCommand(string command) => SettingsActions.IsCommand(command);
    private static IReadOnlyList<string> Active(SettingsSnapshot snapshot) => snapshot.ActiveSettings;
    private static IReadOnlyList<string> Saved(SettingsSnapshot snapshot) => snapshot.SavedSettings;
    private static string SavedSummary(SettingsSnapshot snapshot) => snapshot.SavedSummary;

    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start,
        Func<uint, int, int, int, PlayerSpawner> add)
    {
        string SavedPath() => Path.Combine(UnityEngine.Application.persistentDataPath, "SephiriaOne", "session-preset.txt");
        string[] Lines(SettingsSnapshot snapshot) => snapshot.Lines.ToArray();
        int Value(PlayerSpawner player, string key = "LUCK") => player.PlayerAvatar.customStats.GetValueOrDefault(key);

        var host = start();
        var action = Execute("/fountain +10");
        check(action != null && action.Recognized && action.Success && !action.OpenPanel &&
            host.PlayerAvatar.Inventory.dimensionPocket == 14,
            "Shared dispatcher runs the existing Fountain service");
        check(Execute("/choices all 5").Success && Value(host, "EXTRAITEMCHOICES") == 7,
            "Shared dispatcher runs the existing choices service");
        check(Execute("/stats luck +10").Success && Value(host) == 15,
            "Shared dispatcher runs the existing stats service");
        check(Execute("/stats armor 12").Success && Value(host, "DAMAGEREDUCTION") == 12,
            "Shared dispatcher preserves parser aliases");
        check(Execute("/one save").Success && File.Exists(SavedPath()), "Shared dispatcher saves presets");
        check(Execute("/one status").Success && Execute("/one forget").Success && !File.Exists(SavedPath()),
            "Shared dispatcher routes status and forget presets");

        var guest = add(2, 7, 9, 3);
        int writes = host.PlayerAvatar.customStats.Writes + guest.PlayerAvatar.customStats.Writes;
        foreach (string local in new[] { " /FoUnTaIn nope", "/choices", "/stats unknown", "/one ui", "/one unknown" })
            check(IsCommand(local), "Pure command recognition reserves each complete local prefix: " + local);
        foreach (string unknown in new[] { null, "", "ordinary chat", "/fountains 1", "/choices-extra", "/stats1", "/modded", "/mod ui", "/mod status", "/mod save" })
            check(!IsCommand(unknown), "Pure command recognition leaves other chat alone");
        check(writes == host.PlayerAvatar.customStats.Writes + guest.PlayerAvatar.customStats.Writes && Value(guest) == 7,
            "Pure recognition makes no game writes or pending-join synchronization");
        foreach (string invalid in new[] { "/fountain NaN", "/choices item 21", "/stats luck nope", "/one invalid", "/one ui extra" })
        {
            action = Execute(invalid);
            check(action.Recognized && !action.Success && !action.OpenPanel && action.Messages.Count > 0,
                "Shared dispatcher rejects invalid input: " + invalid);
        }
        check(writes == host.PlayerAvatar.customStats.Writes + guest.PlayerAvatar.customStats.Writes && Value(guest) == 7,
            "Invalid inputs do not synchronize a pending join or write native state");
        foreach (string unknown in new[] { null, "", "ordinary chat", "/fountains 1", "/another-command" })
        {
            action = Execute(unknown);
            check(!action.Recognized && !action.Success && !action.OpenPanel && action.Messages.Count == 0,
                "Unknown chat remains unrecognized");
        }

        NetworkServer.active = false;
        foreach (string denied in new[] { "/fountain 10", "/choices item 3", "/stats luck 12", "/one save", "/one status", "/one forget", "/one ui" })
        {
            action = Execute(denied);
            check(action.Recognized && !action.Success && !action.OpenPanel, "Shared dispatcher rejects guests: " + denied);
        }
        foreach (string local in new[] { "/fountain", "/choices", "/stats", "/stats list", "/one help" })
        {
            action = Execute(local);
            check(action.Recognized && action.Success && !action.OpenPanel && action.Messages.Count > 0,
                "Local help/list remain available to guests: " + local);
        }
        check(writes == host.PlayerAvatar.customStats.Writes + guest.PlayerAvatar.customStats.Writes,
            "Guest rejection and local help make no writes");

        host = start();
        int loggedErrors = UnityEngine.Debug.Errors.Count;
        host.PlayerAvatar.BeforeRead = key => throw new InvalidOperationException("simulated status read failure");
        action = Execute("/one status");
        check(action.Recognized && !action.Success && !action.OpenPanel &&
            action.Messages[0] == "Preset command failed. Check Player.log for details." && UnityEngine.Debug.Errors.Count == loggedErrors + 1,
            "Shared dispatcher catches a service exception once and returns the existing failure text");
        host.PlayerAvatar.BeforeRead = null;
        action = Execute(" /one UI ");
        check(action.Recognized && action.Success && action.OpenPanel,
            "Host UI action is a panel request without preparing the session");
        var snapshot = Snapshot();
        check(snapshot != null && snapshot.HostActive && snapshot.SessionIdentity == null && !snapshot.CanMutate,
            "Reading before first synchronization does not create a host scope");
        check(snapshot.CanForget && !snapshot.CanSave && !string.IsNullOrEmpty(snapshot.AvailabilityReason),
            "Uninitialized scope exposes the actual forget/save availability");
        SessionSettings.Synchronize();
        snapshot = Snapshot();
        check(snapshot.CanMutate && snapshot.CanSave && snapshot.CanForget &&
            ReferenceEquals((object)snapshot.SessionIdentity, DungeonManager.Instance) && snapshot.Epoch > 0,
            "Ready snapshot identifies the current initialized host session");
        long revision = snapshot.Revision;
        check(Execute("/stats luck +10").Success && Snapshot().Revision == revision + 1,
            "Snapshot revision follows successful retained intent");
        guest = add(2, 7, 9, 3);
        snapshot = Snapshot();
        check(Value(guest) == 7 && guest.PlayerAvatar.Inventory.dimensionPocket == 9 && snapshot.Players.Count == 2,
            "Read-only snapshots report a pending ready join without applying policy");
        string[] firstLines = Lines(snapshot);
        SessionSettings.TryExecutePreset(PresetAction.Status, out var status);
        check(firstLines.SequenceEqual(status), "Status and snapshot share identical formatting");
        check(Value(guest) == 7, "Shared status formatter remains read-only");

        host.PlayerAvatar.playerNameSource = "<color=#ff0000>Host</color>";
        host.PlayerAvatar.customStats["CRITICAL"] = 1234;
        host.PlayerAvatar.customStatsAmp["EXTRAITEMCHOICES"] = 100;
        snapshot = Snapshot();
        var player = snapshot.Players[0];
        var stats = (IReadOnlyDictionary<string, decimal>)player.Stats;
        var choices = (IReadOnlyDictionary<string, int>)player.ExtraChoices;
        check(player.Id == 1 && player.Name == "<color=#ff0000>Host</color>" && stats["critical"] == 12.34m &&
            stats["attackspeed"] == 100 && choices["item"] == 4,
            "Typed player values use display units and retain the untrusted raw name");
        host.PlayerAvatar.customStats["LUCK"] += 2;
        host.PlayerAvatar.Inventory.dimensionPocket += 3;
        var changed = Snapshot();
        check(stats["luck"] == 15 && player.FountainPoints == 4 && changed.Players[0].Stats["luck"] == 17 &&
            changed.Players[0].FountainPoints == 7 && changed.Revision == snapshot.Revision,
            "Native changes refresh values without modifying old snapshots or the intent revision");
        bool deniedStats = false, deniedChoices = false, deniedPlayers = false, deniedLines = false, deniedMessages = false;
        try { ((IDictionary<string, decimal>)stats)["luck"] = 99; } catch (NotSupportedException) { deniedStats = true; }
        try { ((IDictionary<string, int>)choices)["item"] = 99; } catch (NotSupportedException) { deniedChoices = true; }
        try { ((IList)snapshot.Players).Clear(); } catch (NotSupportedException) { deniedPlayers = true; }
        try { ((IList<string>)snapshot.Lines).Clear(); } catch (NotSupportedException) { deniedLines = true; }
        try { ((IList<string>)Execute("/stats").Messages).Clear(); } catch (NotSupportedException) { deniedMessages = true; }
        check(deniedStats && deniedChoices && deniedPlayers && deniedLines && deniedMessages,
            "Snapshot collections and dispatcher messages cannot be mutated through collection casts");

        guest.connectionToClient.isReady = false;
        snapshot = Snapshot();
        check(!snapshot.CanMutate && snapshot.CanSave && snapshot.Players.Count == 1 &&
            snapshot.AvailabilityReason.Contains("initializing"), "Unready player disables writes but not saving existing intent");
        guest.connectionToClient.isReady = true;
        bool writableDuringCommit = true, savableDuringCommit = true;
        host.PlayerAvatar.customStats.BeforeWrite = (key, value) =>
        {
            if (key != "LUCK") return;
            var during = Snapshot();
            writableDuringCommit = during.CanMutate; savableDuringCommit = during.CanSave;
        };
        check(Execute("/stats luck +1").Success && !writableDuringCommit && !savableDuringCommit,
            "Snapshots disable writes and saving during an active write batch");
        host.PlayerAvatar.customStats.BeforeWrite = null;
        ChoiceFeature.Available = false;
        check(!Snapshot().ChoicesAvailable && Snapshot().CanMutate && !Execute("/choices item 3").Success,
            "Unavailable choice guard disables only its feature and remains service-authoritative");
        ChoiceFeature.Available = true;

        foreach (string family in new[] { "stats", "choices", "fountain" })
        {
            host = start();
            string marker = family == "stats" ? "SEPHIRIAONE_STAT_LUCK" : family == "choices" ? "SEPHIRIAONE_EXTRAITEMCHOICES" : FountainPoints.ContributionKey;
            host.PlayerAvatar.customStats.BeforeWrite = (key, value) => { if (key == marker) throw new InvalidOperationException("panel marker fault"); };
            check(!Execute(family == "stats" ? "/stats luck +10" : family == "choices" ? "/choices item 3" : "/fountain +10").Success,
                "Shared actions preserve partial-write failure: " + family);
            snapshot = Snapshot();
            check(snapshot.FaultedFeature == family && !snapshot.CanMutate && !snapshot.CanSave && snapshot.CanForget &&
                Lines(snapshot).Any(line => line.Contains("Write journal:")), "Snapshot reports fault and recovery availability: " + family);
            host.PlayerAvatar.customStats.BeforeWrite = null;
            check(Execute("/" + family + " reset").Success && Snapshot().CanMutate && Snapshot().FaultedFeature == "",
                "Shared family-wide reset recovers a write fault: " + family);
        }

        host = start();
        SessionSettings.Synchronize();
        snapshot = Snapshot(true);
        check(snapshot.SavedValid && Lines(snapshot).Any(line => line.StartsWith("Saved preset: none.")),
            "Missing saved file is a valid empty saved state");
        Directory.CreateDirectory(Path.GetDirectoryName(SavedPath()));
        File.WriteAllText(SavedPath(), "corrupt external preset");
        check(Snapshot().SavedValid, "Polling snapshots reuse the saved file cache");
        var refreshedCompact = SessionSettings.ReadSnapshot(refreshSaved: true, includeDiagnostics: false);
        check(!refreshedCompact.SavedValid && refreshedCompact.SavedSettings.Count == 0 && refreshedCompact.Lines.Count == 0,
            "Compact explicit refresh reads external preset edits without building a status report");
        SnapshotDetailTests.AssertSameValues(SessionSettings.ReadSnapshot(), refreshedCompact);
        check(!Snapshot(true).SavedValid && !Execute("/one status").Success,
            "Explicit refresh and status expose malformed saved data");
        check(Execute("/stats luck +3").Success && Execute("/one save").Success && Snapshot().SavedValid &&
            Lines(Snapshot()).Any(line => line.Contains("Saved for future hosted sessions: stats luck offset 3")),
            "Save invalidates cached saved validity and content");
        File.WriteAllText(SavedPath(), "corrupt external preset again");
        check(Snapshot().SavedValid && !Execute("/one status").Success && !Snapshot().SavedValid,
            "Status explicitly refreshes and shares the saved cache with polling");
        check(Execute("/one forget").Success && Snapshot().SavedValid &&
            Lines(Snapshot()).Any(line => line.StartsWith("Saved preset: none.")), "Forget invalidates the saved file cache");

        snapshot = Snapshot();
        long run = snapshot.RunGeneration;
        HorayModAPI.StartSession();
        check(Snapshot().RunGeneration == run + 1, "Snapshot exposes native run generation changes");
        object originalIdentity = DungeonManager.Instance;
        DungeonManager.Instance = new DungeonManager();
        var replaced = Snapshot();
        check(replaced.SessionIdentity == null && !replaced.CanMutate && !replaced.CanSave && replaced.FaultedFeature == "" &&
            Lines(replaced)[0] == "Active session settings: none.", "Old-session state is not presented as an active replacement scope");
        check(ReferenceEquals(snapshot.SessionIdentity, originalIdentity),
            "Old snapshot retains its original identity");
        SessionSettings.Synchronize();
        check(ReferenceEquals((object)Snapshot().SessionIdentity, DungeonManager.Instance) && Snapshot().Epoch > snapshot.Epoch,
            "Synchronization publishes the new scope with a new epoch");
        NetworkServer.active = false;
        snapshot = Snapshot();
        check(!snapshot.HostActive && !snapshot.CanMutate && !snapshot.CanSave && !snapshot.CanForget && snapshot.SessionIdentity == null,
            "Authority loss is visible immediately without synchronization");
        NetworkServer.active = true;
        SessionSettings.Stop();
        snapshot = Snapshot();
        check(snapshot.HostActive && !snapshot.CanMutate && !snapshot.CanSave && !snapshot.CanForget && snapshot.SessionIdentity == null,
            "Stopped controller exposes no actionable scope while host flag still reflects Mirror");
        File.WriteAllText(SavedPath(), "malformed after stop");
        SessionSettings.Start();
        check(!Snapshot().SavedValid, "Controller restart invalidates cached saved state");

        host = start();
        SessionSettings.Synchronize();
        snapshot = Snapshot(true);
        check(Active(snapshot)?.Count == 0 && Saved(snapshot)?.Count == 0 &&
            SavedSummary(snapshot) == "Saved preset: none. Use /one save to store active settings.",
            "Preset view exposes explicit empty retained policies and a missing-file summary");
        check(Execute("/stats luck +10").Success && Execute("/one save").Success,
            "Prepare matching applied and saved preset views");
        var savedTen = Snapshot();
        check(Active(savedTen).SequenceEqual(new[] { "stats luck offset 10" }) &&
            Saved(savedTen).SequenceEqual(Active(savedTen)) &&
            SavedSummary(savedTen) == "Saved for future hosted sessions: stats luck offset 10",
            "Preset views expose retained policy descriptions without player diagnostics");
        check(Lines(savedTen).Contains(SavedSummary(savedTen)) &&
            Lines(savedTen)[0] == "Active session settings: stats luck offset 10",
            "Preset view summaries preserve the original chat status wording");
        check(Execute("/stats luck +5").Success && Active(Snapshot()).SequenceEqual(new[] { "stats luck offset 15" }) &&
            Saved(Snapshot()).SequenceEqual(new[] { "stats luck offset 10" }) &&
            Active(savedTen).SequenceEqual(new[] { "stats luck offset 10" }) && Active(snapshot).Count == 0,
            "Applied intent changes are independent of the saved copy and older snapshots");
        bool deniedActiveSettings = false, deniedSavedSettings = false;
        try { ((IList<string>)Active(savedTen)).Clear(); } catch (NotSupportedException) { deniedActiveSettings = true; }
        try { ((IList<string>)Saved(savedTen)).Clear(); } catch (NotSupportedException) { deniedSavedSettings = true; }
        check(deniedActiveSettings && deniedSavedSettings, "Retained policy view collections reject mutation");
        File.WriteAllText(SavedPath(), "external corrupt preset");
        snapshot = Snapshot();
        check(snapshot.SavedValid && Saved(snapshot).SequenceEqual(new[] { "stats luck offset 10" }) &&
            SavedSummary(snapshot) == SavedSummary(savedTen), "Preset views reuse saved content without extra file reads");
        snapshot = Snapshot(true);
        check(!snapshot.SavedValid && Saved(snapshot).Count == 0 &&
            SavedSummary(snapshot) == "Invalid saved preset; no saved settings were applied." &&
            Lines(snapshot).Contains(SavedSummary(snapshot)), "Invalid saved data exposes no misleading retained settings");
        check(Execute("/one save").Success && Saved(Snapshot()).SequenceEqual(new[] { "stats luck offset 15" }),
            "Saving invalidates the policy view cache");
        check(Execute("/one forget").Success && Saved(Snapshot()).Count == 0 &&
            SavedSummary(Snapshot()) == "Saved preset: none. Use /one save to store active settings.",
            "Forgetting clears the saved policy view and summary");
        check(Execute("/stats reset").Success && Execute("/one save").Success &&
            Active(Snapshot()).Count == 0 && Saved(Snapshot()).Count == 0 &&
            SavedSummary(Snapshot()) == "Saved for future hosted sessions: empty (no adjustments).",
            "An explicitly saved empty policy is distinguished from no saved file");
        check(Execute("/stats luck +3").Success, "Prepare retained view before scope replacement");
        DungeonManager.Instance = new DungeonManager();
        check(Active(Snapshot()).Count == 0, "Replaced session hides the old retained policy view immediately");
        NetworkServer.active = false;
        snapshot = Snapshot();
        check(Active(snapshot).Count == 0 && Saved(snapshot).Count == 0 &&
            SavedSummary(snapshot) == "Only the host can inspect or save session settings.",
            "Guest preset view has empty lists and an authority summary");
        NetworkServer.active = true;
        SessionSettings.Stop();
        snapshot = Snapshot();
        check(Active(snapshot).Count == 0 && Saved(snapshot).Count == 0 &&
            SavedSummary(snapshot) == "Session settings controller is not loaded.",
            "Unloaded preset view has empty lists and an availability summary");
    }
}
