using SephiriaOne;

// Compose the actual checkpoint hooks, runtime, policy and coordinator. The
// native handshake/transport are fixtures; IDs deliberately repeat across joins.
internal static class ReentryRuntimeTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        bool Command(string command) => SettingsActions.Execute(command).Success;
        PlayerSpawner Guest(uint id = 2)
        {
            var guest = add(id, 7, 2, 3);
            guest.playerGuid = "returning-player";
            guest.currentPlayerIdxForSave = 1;
            return guest;
        }
        int Total(PlayerSpawner p, ResourceKind kind) => kind == ResourceKind.Slots ? p.PlayerAvatar.Inventory.CurrentInventoryStorage : p.PlayerAvatar.maxPassivePoint;
        void SaveAndLeave(PlayerSpawner p)
        {
            p.SaveCurrentSessionData();
            TalentResourceCheckpoint.Save(p);
            PlayerSpawner.MultiplayerList.Remove(p);
        }
        void Restore(PlayerSpawner p, ResourceKind kind, int incomingTalentCost = 0)
        {
            p.PlayerAvatar.Inventory.canBroadcast = 0;
            if (kind == ResourceKind.Slots) p.RestoreInventory();
            else
            {
                TalentResourceCheckpoint.Restore(p.PlayerAvatar);
                ResourceRuntime.ApplyEarly(p.PlayerAvatar, kind, incomingTalentCost);
            }
        }
        void Ready(PlayerSpawner p)
        {
            p.PlayerAvatar.Inventory.canBroadcast = 1;
            SessionSettings.Synchronize();
            SessionSettings.Synchronize();
        }

        InventoryResourceHooks.Install();
        foreach (var kind in new[] { ResourceKind.Slots, ResourceKind.Talents })
        {
            string name = kind == ResourceKind.Slots ? "slots" : "talents";
            int native = kind == ResourceKind.Slots ? 24 : 5;
            start();
            var guest = Guest();
            check(Command($"/resources {name} set 36"), "Initial returning-player policy: " + name);
            foreach (string edit in new[] { "set 48", "x2", "reset" })
            {
                int saved = Total(guest, kind);
                SaveAndLeave(guest);
                check(Command($"/resources {name} {edit}"), "Offline host edit: " + name + " " + edit);
                guest = Guest();
                Restore(guest, kind);
                if (kind == ResourceKind.Slots)
                    check(guest.RestoredSlots == saved, "Saved slots restored before enumeration despite newer policy");
                SessionSettings.Synchronize();
                check(guest.PlayerAvatar.Inventory.canBroadcast == 0, "Readiness gate remains closed during restore");
                Ready(guest);
                int expected = edit == "set 48" ? 48 : edit == "x2" ? native * 2 : native;
                check(Total(guest, kind) == expected, "Repeated reconnect receives current intent once: " + name + " " + edit);
            }

            // Unchanged Set preserves native gains; reissuing it is a new command.
            start(); guest = Guest();
            check(Command($"/resources {name} set 36"), "Absolute restore policy: " + name);
            if (kind == ResourceKind.Slots) guest.PlayerAvatar.Inventory.CurrentInventoryStorage += 6;
            else guest.PlayerAvatar.maxPassivePoint += 6;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                SaveAndLeave(guest);
                guest = Guest((uint)(20 + cycle));
                Restore(guest, kind); Ready(guest);
                check(Total(guest, kind) == 42, "Unchanged absolute policy preserves native gains through reconnect " + name);
            }
            check(Command($"/resources {name} x2") && Total(guest, kind) == (native + 6) * 2,
                "Restored native gains stay in the character baseline for multipliers: " + name);
            check(Command($"/resources {name} reset") && Total(guest, kind) == native + 6,
                "Reset cannot remove restored native gains: " + name);
            check(Command($"/resources {name} set 42"), "Prepare same-value reissue after restored gains");
            if (kind == ResourceKind.Slots) guest.PlayerAvatar.Inventory.CurrentInventoryStorage += 6;
            else guest.PlayerAvatar.maxPassivePoint += 6;
            SaveAndLeave(guest);
            check(Command($"/resources {name} set 42"), "Same-value command while absent: " + name);
            guest = Guest(); Restore(guest, kind); Ready(guest);
            check(Total(guest, kind) == 42, "Same-value reissued command supersedes old checkpoint: " + name);

            // Reset-all survives an empty active preset without sacrificing data.
            start(); guest = Guest();
            check(Command($"/resources {name} set 36"), "Conflicting reset checkpoint: " + name);
            SaveAndLeave(guest);
            check(Command("/resources reset") && !SessionSettings.ResourcePolicy.HasChanges,
                "Reset-all clears active settings but retains session reset intent");
            check(Command("/stats luck +10"), "Other feature awaits complete inheritance");
            guest = Guest(); Restore(guest, kind, kind == ResourceKind.Talents ? 30 : 0);
            if (kind == ResourceKind.Slots)
                guest.PlayerAvatar.Inventory.inventoryMatrix[new ItemPosition { x = 5, y = 5 }] = new NewItemOwnInstance();
            else guest.PlayerAvatar.passiveStats[1] = 30;
            Ready(guest);
            check(Total(guest, kind) == 36 && guest.PlayerAvatar.customStats["LUCK"] == 7,
                "Unsafe offline reset preserves checkpoint and defers whole inheritance: " + name);
            check(SessionSettings.ReadSnapshot().Lines.Any(line => line.Contains("Restored inventory/talent data retained")),
                "Pending restored-state conflict is visible: " + name);
            // Leaving again while suspended must save the old applied proof,
            // never label the preserved checkpoint with the pending reset.
            SaveAndLeave(guest);
            guest = Guest(); Restore(guest, kind, kind == ResourceKind.Talents ? 30 : 0);
            if (kind == ResourceKind.Slots)
                guest.PlayerAvatar.Inventory.inventoryMatrix[new ItemPosition { x = 5, y = 5 }] = new NewItemOwnInstance();
            else guest.PlayerAvatar.passiveStats[1] = 30;
            Ready(guest);
            check(Total(guest, kind) == 36 && guest.PlayerAvatar.customStats["LUCK"] == 7,
                "Rejoining again while reset is pending cannot acknowledge stale state: " + name);
            guest.PlayerAvatar.Inventory.inventoryMatrix.Clear();
            guest.PlayerAvatar.passiveStats.Clear();
            SessionSettings.Synchronize(); SessionSettings.Synchronize();
            check(Total(guest, kind) == native && guest.PlayerAvatar.customStats["LUCK"] == 17,
                "Clearing native conflict completes all current state once: " + name);

            // 0.15.0 checkpoints have no proof of which intent they reflect.
            start(); guest = Guest();
            check(Command($"/resources {name} set 36"), "Legacy checkpoint policy: " + name);
            SaveAndLeave(guest);
            foreach (string key in SaveManager.CurrentRun.Values.Keys.Where(k => k.EndsWith(".Intent")).ToArray())
                SaveManager.CurrentRun.Values.Remove(key);
            check(Command($"/resources {name} set 48"), "Legacy checkpoint offline edit: " + name);
            guest = Guest(); Restore(guest, kind); Ready(guest);
            check(Total(guest, kind) == 48, "Legacy checkpoint receives current policy: " + name);
        }

        // Existing non-checkpoint features need no code change. Exercise offline
        // edits/reset, a new connection, and reused GUID/save-slot/network ID.
        start();
        var returning = Guest();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            PlayerSpawner.MultiplayerList.Remove(returning);
            bool reset = cycle == 2;
            check(Command(reset ? "/stats reset" : $"/stats luck x{cycle + 2}") &&
                Command(reset ? "/choices reset" : $"/choices all {cycle + 4}") &&
                Command(reset ? "/fountain reset" : $"/fountain set {cycle + 20}") &&
                Command(reset ? "/resources fruit reset" : $"/resources fruit x{cycle + 2}"),
                "Change all live non-checkpoint families while guest is absent");
            returning = Guest();
            returning.connectionToClient.isReady = false;
            SessionSettings.Synchronize();
            check(returning.PlayerAvatar.customStats["LUCK"] == 7, "Fresh connection still awaits ready");
            returning.connectionToClient.isReady = true;
            Ready(returning);
            check(returning.PlayerAvatar.customStats["LUCK"] == (reset ? 7 : 7 * (cycle + 2)) &&
                returning.PlayerAvatar.Inventory.dimensionPocket == (reset ? 2 : cycle + 20) &&
                returning.PlayerAvatar.customStats["EXTRAITEMCHOICES"] == (reset ? 3 : cycle + 7) &&
                returning.PlayerAvatar.GetCustomStatUnsafe("FRUITCOUNT") + 6 == (reset ? 6 : 6 * (cycle + 2)),
                "Repeated re-entry receives complete current non-checkpoint state without stacking");
        }
        PlayerSpawner.MultiplayerList.Clear();
        InventoryResourceHooks.Uninstall();

        start();
        check(Command("/resources reset"), "Reset history with no addon ownership");
        returning = Guest();
        // Native drafts may exceed the unchanged native allowance. A historical
        // reset is not permission to take over an otherwise unmanaged selection.
        for (int i = 0; i < 7; i++) returning.LocalDataStorage.fruitSkewerBonus.Add(new());
        Ready(returning);
        check(SessionSettings.EnsureFresh(), "Reset history does not reject unmanaged native selections");
    }
}
