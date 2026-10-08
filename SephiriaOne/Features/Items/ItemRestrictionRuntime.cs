using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ItemRestrictionRuntime
    {
        private static DungeonManager dungeon;
        private static ItemRestrictionOverlay overlay;
        private static readonly HashSet<int> affectedDrops = new HashSet<int>();
        private static bool changing;
        internal static bool Enabled { get; private set; }
        internal static string Fault { get; private set; }

        internal static void Bind(DungeonManager current, bool enabled)
        {
            if (ReferenceEquals(current, dungeon)) return;
            Clear();
            if (!current || !NetworkServer.active || !current.isServer) return;
            dungeon = current;
            overlay = new ItemRestrictionOverlay(current.globalItemStatTable);
            current.globalItemStatTable.OnChange += Changed;
            SetEnabled(enabled);
        }

        internal static void Clear()
        {
            if (dungeon && NetworkServer.active && dungeon.isServer) SetEnabled(false);
            if (!ReferenceEquals(dungeon, null)) dungeon.globalItemStatTable.OnChange -= Changed;
            dungeon = null; overlay = null; affectedDrops.Clear(); Enabled = false; Fault = null;
        }

        internal static void SetEnabled(bool enabled)
        {
            if (overlay == null)
            {
                if (enabled) throw new InvalidOperationException("Item restriction session is unavailable.");
                Enabled = false; return;
            }
            changing = true;
            try
            {
                if (enabled)
                    foreach (var entry in dungeon.globalItemStatTable)
                        if (ItemRestrictionOverlay.TryKey(entry.Key, out int id, out bool bound) && bound &&
                            int.TryParse(entry.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _)) affectedDrops.Add(id);
                overlay.SetEnabled(enabled);
                RefreshGround();
                Enabled = enabled; Fault = null;
                if (!enabled) affectedDrops.Clear();
            }
            catch (Exception error) { Fault = error.Message; throw; }
            finally { changing = false; }
        }

        private static void Changed(SyncIDictionary<string, string>.Operation operation, string key, string oldValue)
        {
            if (changing || !NetworkServer.active || !dungeon || !dungeon.isServer) return;
            changing = true;
            try
            {
                if (operation == SyncIDictionary<string, string>.Operation.OP_CLEAR)
                { overlay.Cleared(); affectedDrops.Clear(); return; }
                if (!ItemRestrictionOverlay.TryKey(key, out int id, out bool bound)) return;
                if (overlay.Enabled && bound) affectedDrops.Add(id);
                overlay.Changed(key, operation == SyncIDictionary<string, string>.Operation.OP_REMOVE);
                if (bound && affectedDrops.Contains(id)) RefreshGround(id);
                if (bound && operation == SyncIDictionary<string, string>.Operation.OP_REMOVE) affectedDrops.Remove(id);
            }
            catch (Exception error)
            {
                Fault = error.Message;
                Debug.LogWarning("[SephiriaOne] Item restriction synchronization failed; use /one items reset: " + error);
            }
            finally { changing = false; }
        }

        private static void RefreshGround(int? only = null)
        {
            if (Item.managedItemInstances == null) return;
            foreach (Item item in Item.managedItemInstances)
            {
                if (!item || !item.isServer || item.netId == 0 || !affectedDrops.Contains(item.itemInstanceID) ||
                    (only.HasValue && item.itemInstanceID != only.Value)) continue;
                NetworkConnectionToClient owner = null;
                if (int.TryParse(dungeon.GetGlobalItemStatValue(item.itemInstanceID, "Bound"), out int index))
                    foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                        if (spawner && spawner.currentPlayerIdx == index && spawner.connectionToClient != null &&
                            NetworkServer.connections.TryGetValue(spawner.connectionToClient.connectionId, out var live) &&
                            ReferenceEquals(live, spawner.connectionToClient)) { owner = live; break; }
                // Unlocked drops must become server-owned, as native unbound drops are.
                // Retaining guest authority would destroy them when that guest disconnects.
                if (owner == null && item.connectionToClient != null) item.netIdentity.RemoveClientAuthority();
                // If a drop was spawned unlocked, restore authority before setting the flag.
                if (owner != null && !ReferenceEquals(item.connectionToClient, owner))
                {
                    item.netIdentity.RemoveClientAuthority();
                    if (!item.netIdentity.AssignClientAuthority(owner)) throw new InvalidOperationException("Could not restore item authority.");
                }
                bool bound = owner != null;
                if (item.isBound != bound) item.NetworkisBound = bound;
            }
        }

        internal static void SaveNativeRestrictions(DungeonManager __instance)
        {
            if (!NetworkServer.active || !ReferenceEquals(__instance, dungeon) || overlay == null || !overlay.HasOverrides) return;
            // Rewrite only the save payload. Never toggle the live SyncDictionary to save.
            var native = overlay.NativeSnapshot();
            SaveManager.CurrentRun.SetInt("GlobalItemStatCount", native.Count);
            int index = 0;
            foreach (var entry in native)
            {
                SaveManager.CurrentRun.SetString($"GlobalItemStatCount{index}_Key", entry.Key);
                SaveManager.CurrentRun.SetString($"GlobalItemStatCount{index}_Value", entry.Value);
                index++;
            }
        }
    }
}
