using System;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // Own the native status instance, not the player's total stat. Native unequip
    // will subtract its current Value, including after toggles and run changes.
    internal static class BatCostumeRuntime
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo statuses = typeof(PlayerAvatar).GetField("costumeStats", PrivateInstance);
        private static readonly FieldInfo value = typeof(StatusInstance).GetField("value", PrivateInstance);
        private static readonly FieldInfo applied = typeof(StatusInstance).GetField("applied", PrivateInstance);
        private static readonly PropertyInfo target = typeof(StatusInstance).GetProperty("CurrentTarget", PrivateInstance);
        private static readonly Dictionary<StatusInstance, PlayerAvatar> owned = new Dictionary<StatusInstance, PlayerAvatar>();
        private static bool warned;
        internal static int TrackedCount => owned.Count;
        internal static bool ValidateFields() => statuses?.FieldType == typeof(List<StatusInstance>) && value?.FieldType == typeof(int) &&
            applied?.FieldType == typeof(bool) && target?.PropertyType == typeof(UnitAvatar);

        internal static StatusInstance Create(string metadata, PlayerAvatar player, string costumeId)
        {
            StatusInstance status = StatusDatabase.CreateStatusEntity(metadata);
            if (!NetworkServer.active || !player || !player.isServer || costumeId != "Bat" || !IsNative(status)) return status;
            // Resolve the session before recording ownership: replacing a scope
            // restores the old scope's live statuses first.
            try { if (!SessionSettings.BatReductionForUse) return status; }
            catch (Exception error)
            {
                if (!warned) { warned = true; Debug.LogWarning("[SephiriaOne] Bat costume uses native HP steal after session lookup failed: " + error); }
                return status;
            }
            owned[status] = player;
            status.SetValue(1);
            return status;
        }
        private static bool IsNative(StatusInstance status) => status != null && status.GetType() == typeof(StatusInstance_HPSteal) && status.Value == 5;
        private static List<StatusInstance> Statuses(PlayerAvatar player) => (List<StatusInstance>)statuses.GetValue(player);
        private static bool IsApplied(StatusInstance status, PlayerAvatar player) => (bool)applied.GetValue(status) && ReferenceEquals(target.GetValue(status), player);

        internal static void Append(StateWriteBatch batch, PlayerAvatar player, bool reduce)
        {
            if (!player || !player.isServer || player.currentCostume != "Bat") return;
            var list = Statuses(player);
            foreach (StatusInstance status in list)
            {
                bool ours = owned.TryGetValue(status, out PlayerAvatar owner) && ReferenceEquals(owner, player);
                if (!ours && (!reduce || !IsNative(status))) continue;
                if (!IsApplied(status, player)) continue;
                if (status.Value != 5 && status.Value != 1)
                    throw new InvalidOperationException("Tracked Bat costume HP steal changed externally; no adjustment was planned.");
                owned[status] = player;
                AppendStatus(batch, player, status, reduce ? 1 : 5, list);
            }
        }

        private static void AppendStatus(StateWriteBatch batch, PlayerAvatar player, StatusInstance status, int amount, List<StatusInstance> list)
        {
            if (amount == status.Value) return;
            player.customStats.TryGetValue("HPSTEAL", out int raw);
            int next = checked((int)((long)raw + amount - status.Value));
            bool Current() => NetworkServer.active && player && player.isServer && ReferenceEquals(Statuses(player), list) &&
                list.Contains(status) && IsApplied(status, player);
            batch.Require(Current);
            batch.RequireAfter(Current);
            // Updating Value without re-equipping avoids item grants, network
            // effects and native stat remove/reapply callbacks. The installed
            // Apply/Remove contract is checked before enabling this feature.
            batch.Add("Bat costume HP steal", () => (Raw(player), status.Value), pair =>
            {
                if (!Current()) throw new InvalidOperationException("Bat costume ownership changed during application.");
                int beforeRaw = Raw(player), beforeValue = status.Value;
                // The native dictionary notifies AFTER changing its value. Give
                // native callbacks the matching removal amount, even if one
                // switches costumes or throws before the setter returns.
                value.SetValue(status, pair.Item2);
                try { player.customStats["HPSTEAL"] = pair.Item1; }
                catch
                {
                    // A write rejected before mutation must retain the old
                    // removal amount. An after-write failure already has a
                    // consistent pair; shared readback still reports the fault.
                    if (Current() && Raw(player) == beforeRaw) value.SetValue(status, beforeValue);
                    throw;
                }
            }, (next, amount));
        }
        private static int Raw(PlayerAvatar player) => player.customStats.TryGetValue("HPSTEAL", out int raw) ? raw : 0;

        internal static void ForgetStatus(StatusInstance status) => owned.Remove(status);
        internal static void ForgetPlayer(PlayerAvatar player)
        {
            var remove = new List<StatusInstance>();
            foreach (var entry in owned) if (ReferenceEquals(entry.Value, player)) remove.Add(entry.Key);
            foreach (var status in remove) owned.Remove(status);
        }
        internal static void Clear()
        {
            if (owned.Count == 0) return;
            if (NetworkServer.active)
            {
                var batch = new StateWriteBatch(() => NetworkServer.active);
                foreach (var entry in owned)
                {
                    PlayerAvatar player = entry.Value;
                    if (!player || !player.isServer) continue;
                    var list = Statuses(player);
                    if (list.Contains(entry.Key) && IsApplied(entry.Key, player))
                    {
                        if (entry.Key.Value != 1 && entry.Key.Value != 5)
                            throw new InvalidOperationException("Cannot restore externally changed Bat costume status.");
                        AppendStatus(batch, player, entry.Key, 5, list);
                    }
                }
                if (!batch.TryCommit(out string error))
                {
                    if (batch.MayHaveWritten) SessionSettings.RecordFault("bat", batch, error);
                    throw new InvalidOperationException(error);
                }
            }
            owned.Clear();
            warned = false;
        }
    }
}
