using System;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // Intent belongs to SessionPolicy; these observations describe only which
    // native avatar already received it. Grant/checkpoint lifetimes are separate.
    internal static class ResourceRuntime
    {
        private readonly struct Applied
        {
            public readonly ResourceSetting Setting;
            public readonly int Owned;
            public Applied(ResourceSetting setting, int owned) { Setting = setting; Owned = owned; }
        }
        private static readonly Dictionary<PlayerAvatar, Dictionary<ResourceKind, Applied>> applied =
            new Dictionary<PlayerAvatar, Dictionary<ResourceKind, Applied>>(ReferenceComparer<PlayerAvatar>.Instance);
        public static void Clear() => applied.Clear();
        public static void Forget(PlayerAvatar player) => applied.Remove(player);
        public static bool TryGetSetting(ResourceKind kind, out ResourceSetting setting)
        {
            setting = default;
            return SessionSettings.EnsureResourceScope() && SessionSettings.ResourcePolicy.TryGet(kind, out setting);
        }
        public static void Report(string message) => Debug.LogWarning("[SephiriaOne] " + message);

        public static void AcceptRestored(PlayerAvatar player, ResourceKind kind)
        {
            if (TryGetSetting(kind, out ResourceSetting setting)) Accept(player, kind, setting);
        }
        private static void Accept(PlayerAvatar player, ResourceKind kind, ResourceSetting setting)
        {
            if (!applied.TryGetValue(player, out var values)) applied[player] = values = new Dictionary<ResourceKind, Applied>();
            player.customStats.TryGetValue(ResourceCatalog.Get(kind).Marker, out int owned);
            values[kind] = new Applied(setting, owned);
        }
        private static bool KeepAbsolute(PlayerAvatar player, ResourceKind kind, ResourceSetting setting, int owned) =>
            setting.Mode == ResourceMode.Set && applied.TryGetValue(player, out var values) &&
            values.TryGetValue(kind, out Applied previous) && previous.Owned == owned &&
            previous.Setting.Mode == setting.Mode && previous.Setting.Amount == setting.Amount;

        public static bool TryExecute(ResourceCommand command, out string message)
        {
            message = "Only the host can change everyone's resources.";
            if (!NetworkServer.active) return false;
            if (!SessionSettings.PrepareCommand("resources", command.IsReset, out HostCommandContext context,
                out message, command.Definition == null)) return false;
            ResourceSetting next = SessionSettings.ResourcePolicy.Next(command);
            var batch = context.CreateBatch();
            foreach (var definition in ResourceCatalog.All)
            {
                if (command.Definition != null && command.Definition != definition) continue;
                if (!command.IsReset && !ResourceFeature.IsAvailable(definition.Kind))
                { message = definition.Label + " compatibility guards are unavailable. Check Player.log."; return false; }
                foreach (PlayerAvatar player in context.Players)
                {
                    ResourceSnapshot snapshot = ResourceNative.Capture(player, definition.Kind);
                    if (!ResourcePlanner.TryPlan(next, snapshot, command.IsReset, out ResourceUpdate update, out message)) return false;
                    if (!definition.StartingOnly) ResourceNative.AddWrites(batch, player, update);
                }
            }
            if (!SessionSettings.Commit("resources", batch, () =>
            {
                SessionSettings.ResourcePolicy.Record(command);
                foreach (PlayerAvatar player in context.Players)
                    foreach (var definition in ResourceCatalog.All)
                    {
                        if (command.Definition != null && command.Definition != definition) continue;
                        if (SessionSettings.ResourcePolicy.TryGet(definition.Kind, out ResourceSetting setting)) Accept(player, definition.Kind, setting);
                        else if (applied.TryGetValue(player, out var values)) values.Remove(definition.Kind);
                    }
            }, out message)) return false;
            message = command.Definition?.StartingOnly == true ?
                "Updated future fresh-run " + command.Definition.Label.ToLowerInvariant() + ". Current balances and grants already begun are unchanged." :
                "Updated resource settings for " + context.Players.Count + " player(s). Occupied slots and native selections were preserved.";
            return true;
        }

        // Append to the same journal as stats/choices/Fountain inheritance.
        public static bool TryAppend(PlayerAvatar player, StateWriteBatch batch, out Action remember, out string error, bool enrolledOnly = false)
        {
            var accepted = new List<(ResourceKind Kind, ResourceSetting Setting)>();
            remember = () => { foreach (var entry in accepted) Accept(player, entry.Kind, entry.Setting); };
            error = "";
            foreach (var definition in ResourceCatalog.All)
            {
                if (definition.StartingOnly || !SessionSettings.ResourcePolicy.TryGet(definition.Kind, out ResourceSetting setting)) continue;
                // A rejected or partially written inheritance cannot opt a player
                // into maintenance. Explicit commands enroll only their own kind.
                if (enrolledOnly && (!applied.TryGetValue(player, out var existing) || !existing.ContainsKey(definition.Kind))) continue;
                if (!ResourceFeature.IsAvailable(definition.Kind)) { error = definition.Label + " guard unavailable."; return false; }
                ResourceSnapshot snapshot = ResourceNative.Capture(player, definition.Kind);
                if (KeepAbsolute(player, definition.Kind, setting, snapshot.Owned)) continue;
                if (!ResourcePlanner.TryPlan(setting, snapshot, false, out ResourceUpdate update, out error)) return false;
                ResourceNative.AddWrites(batch, player, update);
                accepted.Add((definition.Kind, setting));
            }
            return true;
        }

        public static object Observe(PlayerAvatar player)
        {
            var key = new StringBuilder();
            foreach (var definition in ResourceCatalog.All)
            {
                if (definition.StartingOnly || !SessionSettings.ResourcePolicy.TryGet(definition.Kind, out ResourceSetting setting)) continue;
                var value = ResourceNative.Capture(player, definition.Kind);
                key.Append(definition.Name).Append(setting.Describe()).Append(':').Append(ResourceFeature.IsAvailable(definition.Kind))
                    .Append(':').Append(value.Raw).Append(':').Append(value.Owned).Append(':').Append(value.Bonus)
                    .Append(':').Append(value.Amplifier).Append(':').Append(value.DisplayOffset)
                    .Append(':').Append(value.MinimumSafe).Append(':').Append(value.Busy).Append(';');
            }
            return key.ToString();
        }

        public static ReconcileResult Maintain(HostPlayer subject)
        {
            if (SessionSettings.ResourceWritesBlocked) return ReconcileResult.Waiting("Another native write is unresolved.");
            var batch = new HostCommandContext(DungeonManager.Instance, new[] { subject }).CreateBatch();
            if (!TryAppend(subject.Player, batch, out Action remember, out string error, enrolledOnly: true))
                return ReconcileResult.Suspended(error + " Existing contribution retained to preserve native state.");
            if (!batch.TryCommit(out error))
            {
                if (batch.MayHaveWritten) { SessionSettings.RecordFault("resources", batch, error); return ReconcileResult.Faulted(error); }
                return ReconcileResult.Waiting(error);
            }
            remember(); return ReconcileResult.Applied();
        }

        // These native consumers precede full avatar readiness. Never call the
        // ordinary Synchronize here: doing so would enroll a partially loaded avatar.
        public static void ApplyEarly(PlayerAvatar player, ResourceKind kind, int requiredMinimum = 0)
        {
            if (!TryGetSetting(kind, out ResourceSetting setting)) return;
            if (!ResourceFeature.IsAvailable(kind) || !EarlyReady(player) || SessionSettings.ResourceWritesBlocked)
                throw new InvalidOperationException("Resource state is not ready for the native " + kind + " consumer.");
            ResourceSnapshot snapshot = ResourceNative.Capture(player, kind);
            if (KeepAbsolute(player, kind, setting, snapshot.Owned))
            {
                if (!ResourcePlanner.TryValue(snapshot, false, out int current) || current < Math.Max(requiredMinimum, snapshot.MinimumSafe))
                    throw new InvalidOperationException("The retained " + kind + " budget cannot preserve incoming native selections.");
                return;
            }
            var constrained = new ResourceSnapshot(snapshot.Definition, snapshot.Raw, snapshot.Owned, snapshot.Bonus,
                snapshot.Amplifier, snapshot.DisplayOffset, Math.Max(requiredMinimum, snapshot.MinimumSafe), snapshot.Busy);
            if (!ResourcePlanner.TryPlan(setting, constrained, false, out ResourceUpdate update, out string error))
                throw new InvalidOperationException(error);
            DungeonManager scope = DungeonManager.Instance;
            PlayerSpawner spawner = player.spawner;
            var storage = player.localDataStorage;
            var inventory = player.Inventory;
            var run = SaveManager.CurrentRun;
            var raw = player.customStats;
            var bonus = player.calculatedBonusStats;
            var amp = player.customStatsAmp;
            uint netId = player.netId, spawnerId = spawner.netId;
            string guid = spawner.playerGuid;
            int saveSlot = spawner.currentPlayerIdxForSave;
            long generation = SessionSettings.ResourceGeneration;
            var batch = new StateWriteBatch(() => EarlyReady(player) && ReferenceEquals(scope, DungeonManager.Instance) &&
                player.netId == netId && spawner.netId == spawnerId && ReferenceEquals(player.spawner, spawner) &&
                ReferenceEquals(player.localDataStorage, storage) && ReferenceEquals(player.Inventory, inventory) &&
                ReferenceEquals(SaveManager.CurrentRun, run) && SessionSettings.ResourceGeneration == generation &&
                ReferenceEquals(player.customStats, raw) && ReferenceEquals(player.calculatedBonusStats, bonus) &&
                ReferenceEquals(player.customStatsAmp, amp) && spawner.playerGuid == guid && spawner.currentPlayerIdxForSave == saveSlot);
            ResourceNative.AddWrites(batch, player, update);
            if (!batch.TryCommit(out error))
            {
                if (batch.MayHaveWritten) SessionSettings.RecordFault("resources", batch, error);
                throw new InvalidOperationException(error);
            }
            Accept(player, kind, setting);
        }
        private static bool EarlyReady(PlayerAvatar player) => NetworkServer.active && player && player.isServer && player.netId != 0 &&
            player.spawner && player.spawner.isServer && ReferenceEquals(player.spawner.PlayerAvatar, player) &&
            player.localDataStorage && ReferenceEquals(player.spawner.LocalDataStorage, player.localDataStorage) &&
            DungeonManager.Instance && DungeonManager.Instance.isServer && DungeonManager.Instance.netId != 0;
    }
}
