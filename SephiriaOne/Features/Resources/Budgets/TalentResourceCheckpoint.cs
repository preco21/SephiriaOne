using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Mirror;

namespace SephiriaOne
{
    internal static class TalentResourceCheckpoint
    {
        private static readonly ConditionalWeakTable<PlayerAvatar, LoadStamp> loaded = new ConditionalWeakTable<PlayerAvatar, LoadStamp>();

        private sealed class LoadStamp
        {
            public readonly PlayerSpawner Spawner;
            public readonly SaveData Run;
            public readonly string Guid;
            public readonly int Slot;
            public readonly uint NetId;
            public readonly long Generation;
            public LoadStamp(PlayerAvatar player)
            {
                Spawner = player.spawner; Run = SaveManager.CurrentRun; Guid = Spawner.playerGuid;
                Slot = Spawner.currentPlayerIdxForSave; NetId = player.netId; Generation = SessionSettings.ResourceGeneration;
            }
            // First departure can advance ResourceGeneration within this same
            // avatar/save. It invalidates write guards, but must not replay an old
            // checkpoint over an explicit budget reduction. Native restarts replace Run.
            public bool Matches(PlayerAvatar player) => ReferenceEquals(player.spawner, Spawner) &&
                ReferenceEquals(SaveManager.CurrentRun, Run) && player.netId == NetId &&
                Spawner.playerGuid == Guid && Spawner.currentPlayerIdxForSave == Slot;
        }

        public static bool Restore(PlayerAvatar player)
        {
            ResourceRuntime.TryGetSetting(ResourceKind.Talents, out _);
            PlayerSpawner spawner = player.spawner;
            if (!spawner) return false;
            if (loaded.TryGetValue(player, out LoadStamp previous) && previous.Matches(player)) return false;
            var stamp = new LoadStamp(player);
            SaveData run = SaveManager.CurrentRun;
            string key = Key(spawner);
            if (run == null || key == null || !run.ContainsKey(key + "Version"))
            {
                Remember(player, stamp);
                return false;
            }
            if (SessionSettings.ResourceWritesBlocked)
                throw new InvalidOperationException("Resolve the pending state-write recovery before restoring the talent budget.");
            if (run.GetInt(key + "Version", 0) != 1 || !run.ContainsKey(key + "Total") || !run.ContainsKey(key + "Owned"))
                throw new InvalidOperationException("The addon talent checkpoint is incomplete or unsupported.");
            int saved = run.GetInt(key + "Total", -1);
            int savedOwned = run.GetInt(key + "Owned", 0);
            if (saved < 0 || (long)saved - savedOwned < 0 || (long)saved - savedOwned > int.MaxValue)
                throw new InvalidOperationException("The saved talent budget or its native baseline is invalid.");
            ResourceSnapshot before = ResourceBudgets.Capture(player, ResourceKind.Talents);
            int native = checked(before.Raw - before.Owned);
            if (native < 0) throw new InvalidOperationException("The live talent ownership baseline is invalid.");
            int target = Math.Max(saved, checked(native + savedOwned));
            // A saved native award remains native even if initialization has
            // not replayed it. Reset/multipliers must retain that baseline.
            int owned = savedOwned;
            string guid = spawner.playerGuid;
            int slot = spawner.currentPlayerIdxForSave;
            uint netId = player.netId;
            var batch = new StateWriteBatch(() => NetworkServer.active && player && player.isServer && player.netId == netId && netId != 0 &&
                spawner && spawner.isServer && ReferenceEquals(player.spawner, spawner) && ReferenceEquals(spawner.PlayerAvatar, player) &&
                ReferenceEquals(SaveManager.CurrentRun, run) && SessionSettings.ResourceGeneration == stamp.Generation &&
                spawner.playerGuid == guid && spawner.currentPlayerIdxForSave == slot);
            ResourceBudgets.AddWrites(batch, player, new ResourceUpdate(before.Definition, target, owned, target));
            if (!batch.TryCommit(out string error))
            {
                if (batch.MayHaveWritten) SessionSettings.RecordFault("resources", batch, error);
                throw new InvalidOperationException(error);
            }
            ResourceRuntime.AcceptRestored(player, ResourceKind.Talents, run.GetString(key + "Intent", ""));
            Remember(player, stamp);
            return true;
        }

        private static void Remember(PlayerAvatar player, LoadStamp stamp)
        {
            loaded.Remove(player);
            loaded.Add(player, stamp);
        }

        public static void Save(PlayerSpawner spawner)
        {
            if (!NetworkServer.active || !spawner || !spawner.isServer || !spawner.PlayerAvatar) return;
            PlayerAvatar player = spawner.PlayerAvatar;
            string key = Key(spawner);
            SaveData run = SaveManager.CurrentRun;
            player.customStats.TryGetValue(ResourceCatalog.Get(ResourceKind.Talents).Marker, out int owned);
            bool exists = key != null && run != null && run.ContainsKey(key + "Version");
            if (owned == 0 && !exists && !ResourceRuntime.TryGetSetting(ResourceKind.Talents, out _)) return;
            if (key == null || run == null)
                throw new InvalidOperationException("A resolved native player GUID, save slot and run save are required to preserve the talent budget.");
            ResourceSnapshot snapshot = ResourceBudgets.Capture(player, ResourceKind.Talents);
            if (snapshot.Raw < snapshot.MinimumSafe)
                throw new InvalidOperationException("The talent budget is below its allocated points; no replacement checkpoint was written.");
            run.SetInt(key + "Total", snapshot.Raw);
            run.SetInt(key + "Owned", snapshot.Owned);
            run.SetString(key + "Intent", ResourceRuntime.CheckpointIntent(player, ResourceKind.Talents));
            run.SetInt(key + "Version", 1);
        }

        private static string Key(PlayerSpawner spawner)
        {
            if (spawner.currentPlayerIdxForSave < 0 || string.IsNullOrWhiteSpace(spawner.playerGuid)) return null;
            return "SephiriaOne.ResourceTalents.Player" + spawner.currentPlayerIdxForSave.ToString(CultureInfo.InvariantCulture) + "." +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(spawner.playerGuid)) + ".";
        }
    }
}
