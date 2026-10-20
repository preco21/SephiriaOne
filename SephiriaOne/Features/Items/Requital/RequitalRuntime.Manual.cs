using System;
using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static partial class RequitalRuntime
    {
        internal static bool GiveAll(out string error)
        {
            error = L.T("Only the host can give Requital Slates.");
            if (!NetworkServer.active) return false;
            error = L.T("Requital Slates require a ready hosted session; see Player.log if unavailable.");
            if (!CanExecute) return false;
            int count = SessionSettings.Requital.Count;
            var targets = new HashSet<FriendlyFireParticipants.Receipt>();
            int waiting = 0, granted = 0, failed = 0;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                var receipt = spawner ? FriendlyFireParticipants.Capture(spawner.PlayerAvatar) : null;
                if (receipt == null) waiting++; else targets.Add(receipt);
            }
            var dungeon = DungeonManager.Instance;
            var run = SaveManager.CurrentRun;
            long generation = SessionSettings.ResourceGeneration, observedScope = scope;
            writing = true;
            try
            {
                foreach (var receipt in targets)
                {
                    var inventory = receipt.Player.Inventory;
                    bool Current() => scope == observedScope && generation == SessionSettings.ResourceGeneration &&
                        NetworkServer.active && ReferenceEquals(dungeon, DungeonManager.Instance) &&
                        ReferenceEquals(run, SaveManager.CurrentRun) && ReviveAllAction.IsRunOpen(dungeon, run) &&
                        FriendlyFireParticipants.IsCurrent(receipt) && HostStateAdapter.IsReady(receipt.Spawner) &&
                        inventory && ReferenceEquals(inventory, receipt.Player.Inventory);
                    if (!Current()) { waiting++; continue; }
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            Require(Current());
                            if (!ValidItem()) throw new InvalidOperationException("Requital database identity changed.");
                            // Permission is non-reentrant: borrow an existing one.
                            using ((bool)permission.GetValue(inventory) ? null : new GridInventory.Permission(inventory))
                            {
                                Require(Current() && (bool)permission.GetValue(inventory));
                                int id = ItemDatabase.GenerateInstanceID(random);
                                if (!inventory.LocalAddItem(id, ItemId, 1, 0, notification: true, isReward: false))
                                {
                                    Require(Current() && (bool)permission.GetValue(inventory) && ValidItem());
                                    inventory.temporaryInventory.Add(new ItemMetadata(id, ItemId, 1));
                                }
                                // Exceptions never trigger overflow or another
                                // attempt: native callbacks may already have written.
                                granted++;
                                Require(Current());
                            }
                        }
                    }
                    catch (Exception exception) { failed++; Warn("manual grant failed", exception); }
                }
            }
            finally { writing = false; }
            error = L.F("Requital Slates: granted {0}; not ready {1}; failed {2}.", granted, waiting, failed);
            return waiting == 0 && failed == 0;
        }
    }
}
