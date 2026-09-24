using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            public readonly string Intent;
            public readonly int Owned;
            public Applied(string intent, int owned) { Intent = intent; Owned = owned; }
        }
        // Early consumers can run before SessionSettings tracks an avatar. A
        // disconnect in that window has no normal Forget callback; do not own
        // the avatar's lifetime merely because we recorded an early write.
        private static ConditionalWeakTable<PlayerAvatar, Dictionary<ResourceKind, Applied>> applied =
            new ConditionalWeakTable<PlayerAvatar, Dictionary<ResourceKind, Applied>>();
        private static ConditionalWeakTable<PlayerAvatar, Dictionary<ResourceKind, Applied>> restored =
            new ConditionalWeakTable<PlayerAvatar, Dictionary<ResourceKind, Applied>>();
        public static void Clear()
        {
            applied = new ConditionalWeakTable<PlayerAvatar, Dictionary<ResourceKind, Applied>>();
            restored = new ConditionalWeakTable<PlayerAvatar, Dictionary<ResourceKind, Applied>>();
        }
        public static void Forget(PlayerAvatar player) { applied.Remove(player); restored.Remove(player); }
        public static bool TryGetSetting(ResourceKind kind, out ResourceSetting setting)
        {
            setting = default;
            return SessionSettings.EnsureResourceScope() && SessionSettings.ResourcePolicy.TryGet(kind, out setting);
        }
        public static void Report(string message) => Debug.LogWarning("[SephiriaOne] " + message);

        public static void AcceptRestored(PlayerAvatar player, ResourceKind kind, string intent = "")
        {
            var values = restored.GetOrCreateValue(player);
            player.customStats.TryGetValue(ResourceCatalog.Get(kind).Marker, out int owned);
            values[kind] = new Applied(intent, owned);
            if (applied.TryGetValue(player, out var previous)) previous.Remove(kind);
        }
        public static bool HasRestored(PlayerAvatar player) => restored.TryGetValue(player, out var values) && values.Count != 0;
        public static string CheckpointIntent(PlayerAvatar player, ResourceKind kind)
        {
            player.customStats.TryGetValue(ResourceCatalog.Get(kind).Marker, out int owned);
            if ((applied.TryGetValue(player, out var values) && values.TryGetValue(kind, out Applied previous)) ||
                (restored.TryGetValue(player, out values) && values.TryGetValue(kind, out previous)))
                return previous.Owned == owned ? previous.Intent : "";
            return "";
        }
        private static void Accept(PlayerAvatar player, ResourceKind kind)
        {
            var values = applied.GetOrCreateValue(player);
            player.customStats.TryGetValue(ResourceCatalog.Get(kind).Marker, out int owned);
            values[kind] = new Applied(SessionSettings.ResourcePolicy.Intent(kind), owned);
            if (restored.TryGetValue(player, out var pending)) pending.Remove(kind);
        }
        private static bool KeepAbsolute(PlayerAvatar player, ResourceKind kind, ResourceSetting setting) =>
            setting.Mode == ResourceMode.Set && SessionSettings.ResourcePolicy.Intent(kind) != "" &&
            CheckpointIntent(player, kind) == SessionSettings.ResourcePolicy.Intent(kind);

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
                        Accept(player, definition.Kind);
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
            var accepted = new List<ResourceKind>();
            remember = () => { foreach (var kind in accepted) Accept(player, kind); };
            error = "";
            foreach (var definition in ResourceCatalog.All)
            {
                if (definition.StartingOnly || !SessionSettings.ResourcePolicy.TryGetIntent(definition.Kind, out ResourceSetting setting)) continue;
                // A rejected or partially written inheritance cannot opt a player
                // into maintenance. Explicit commands enroll only their own kind.
                if (enrolledOnly && (!applied.TryGetValue(player, out var existing) || !existing.ContainsKey(definition.Kind))) continue;
                if (!setting.Empty && !ResourceFeature.IsAvailable(definition.Kind)) { error = definition.Label + " guard unavailable."; return false; }
                ResourceSnapshot snapshot = ResourceNative.Capture(player, definition.Kind);
                // A reset is complete once ownership is absent. It must not
                // impose addon validation on unrelated native selections.
                if ((setting.Empty && snapshot.Owned == 0) || KeepAbsolute(player, definition.Kind, setting))
                { accepted.Add(definition.Kind); continue; }
                if (!ResourcePlanner.TryPlan(setting, snapshot, false, out ResourceUpdate update, out error)) return false;
                ResourceNative.AddWrites(batch, player, update);
                accepted.Add(definition.Kind);
            }
            return true;
        }

        public static ((string, bool, ResourceSnapshot) Slots, (string, bool, ResourceSnapshot) Talents,
            (string, bool, ResourceSnapshot) Fruit) Observe(PlayerAvatar player) =>
            (Observe(player, ResourceKind.Slots), Observe(player, ResourceKind.Talents), Observe(player, ResourceKind.Fruit));

        private static (string, bool, ResourceSnapshot) Observe(PlayerAvatar player, ResourceKind kind)
        {
            if (!SessionSettings.ResourcePolicy.TryGetIntent(kind, out _)) return default;
            return (SessionSettings.ResourcePolicy.Intent(kind), ResourceFeature.IsAvailable(kind), ResourceNative.Capture(player, kind));
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
            if (!SessionSettings.EnsureResourceScope() || !SessionSettings.ResourcePolicy.TryGetIntent(kind, out ResourceSetting setting)) return;
            if (setting.Empty && (!player.customStats.TryGetValue(ResourceCatalog.Get(kind).Marker, out int owned) || owned == 0)) return;
            if (!ResourceFeature.IsAvailable(kind) || !EarlyReady(player) || SessionSettings.ResourceWritesBlocked)
                throw new InvalidOperationException("Resource state is not ready for the native " + kind + " consumer.");
            ResourceSnapshot snapshot = ResourceNative.Capture(player, kind);
            if (KeepAbsolute(player, kind, setting))
            {
                if (!ResourcePlanner.TryValue(snapshot, false, out int current) || current < Math.Max(requiredMinimum, snapshot.MinimumSafe))
                    throw new InvalidOperationException("The retained " + kind + " budget cannot preserve incoming native selections.");
                return;
            }
            var constrained = new ResourceSnapshot(snapshot.Definition, snapshot.Raw, snapshot.Owned, snapshot.Bonus,
                snapshot.Amplifier, snapshot.DisplayOffset, Math.Max(requiredMinimum, snapshot.MinimumSafe), snapshot.Busy);
            if (!ResourcePlanner.TryPlan(setting, constrained, false, out ResourceUpdate update, out string error))
            {
                // A returning player's checkpoint must first restore all selections.
                // Defer only safe-decrease conflicts; invalid arithmetic still fails.
                var unconstrained = new ResourceSnapshot(snapshot.Definition, snapshot.Raw, snapshot.Owned,
                    snapshot.Bonus, snapshot.Amplifier, snapshot.DisplayOffset);
                if (restored.TryGetValue(player, out var pending) && pending.ContainsKey(kind) &&
                    ResourcePlanner.TryValue(snapshot, false, out int current) && current >= constrained.MinimumSafe &&
                    ResourcePlanner.TryPlan(setting, unconstrained, false, out _, out _)) return;
                throw new InvalidOperationException(error);
            }
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
            Accept(player, kind);
        }
        private static bool EarlyReady(PlayerAvatar player) => NetworkServer.active && player && player.isServer && player.netId != 0 &&
            player.spawner && player.spawner.isServer && ReferenceEquals(player.spawner.PlayerAvatar, player) &&
            player.localDataStorage && ReferenceEquals(player.spawner.LocalDataStorage, player.localDataStorage) &&
            DungeonManager.Instance && DungeonManager.Instance.isServer && DungeonManager.Instance.netId != 0;
    }
}
