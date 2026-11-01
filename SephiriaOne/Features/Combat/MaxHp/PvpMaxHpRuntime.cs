using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    // Shared participant lifetimes, never account IDs or a saved pre-match HP snapshot.
    internal static class PvpMaxHpRuntime
    {
        private sealed class Lease
        {
            internal readonly FriendlyFireParticipants.Receipt Receipt;
            internal readonly PvpMaxHpProjection Projection = new PvpMaxHpProjection();
            internal Lease(FriendlyFireParticipants.Receipt receipt) { Receipt = receipt; }
        }
        private static readonly Dictionary<PlayerAvatar, Lease> leases = new Dictionary<PlayerAvatar, Lease>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<UnitAvatar, int> nativeDepth = new Dictionary<UnitAvatar, int>(ReferenceComparer<UnitAvatar>.Instance);
        private static readonly List<PlayerAvatar> scratch = new List<PlayerAvatar>();
        private static bool ticking;
        private static long revision;

        internal static void Tick()
        {
            if (ticking) return;
            ticking = true;
            try
            {
                var settings = SessionSettings.FriendlyFireForDisplay;
                if (!NetworkServer.active || !PvpMaxHpFeature.Available || !settings.Balance || !settings.MaxHp.Enabled ||
                    (!settings.Enabled && !CombatMatchRuntime.IsRunning))
                { if (leases.Count != 0) Clear(); return; }
                _ = FriendlyFireParticipants.Epoch;
                long version = revision;
                foreach (var player in leases.Keys) scratch.Add(player);
                foreach (var player in scratch)
                { if (version != revision) return; Reconcile(player); }
                scratch.Clear();
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                    if (spawner && spawner.PlayerAvatar) scratch.Add(spawner.PlayerAvatar);
                foreach (var player in scratch)
                { if (version != revision) return; if (!leases.ContainsKey(player)) Reconcile(player); }
            }
            finally { scratch.Clear(); ticking = false; }
        }

        internal static void Reconcile(PlayerAvatar player)
        {
            if (ReferenceEquals(player, null)) return;
            var settings = SessionSettings.FriendlyFireForDisplay;
            if (!player || !NetworkServer.active || !PvpMaxHpFeature.Available || !FriendlyFireFeature.Available ||
                (!settings.Enabled && !CombatMatchRuntime.IsRunning) || !settings.Balance || !settings.MaxHp.Enabled || player.loadingScreenType >= 0)
            { Release(player); return; }
            if (nativeDepth.ContainsKey(player)) return;
            _ = FriendlyFireParticipants.Epoch;
            long version = revision;
            if (!PvpParticipation.IsActive(player)) { Release(player); return; }
            var receipt = FriendlyFireParticipants.Capture(player);
            settings = SessionSettings.FriendlyFireForHit;
            if (version != revision || receipt == null) { Release(player); return; }
            if (!settings.Enabled || !settings.Balance || !settings.MaxHp.Enabled) { Release(player); return; }
            if (leases.TryGetValue(player, out var lease) && !ReferenceEquals(lease.Receipt, receipt))
            { lease.Projection.Release(player); leases.Remove(player); lease = null; }
            if (lease == null) { lease = new Lease(receipt); leases.Add(player, lease); }
            lease.Projection.Apply(player, settings.MaxHp);
        }

        internal static void Release(PlayerAvatar player)
        {
            if (ReferenceEquals(player, null) || !leases.TryGetValue(player, out var lease)) return;
            lease.Projection.Release(player); leases.Remove(player);
        }
        internal static void ReleaseConnection(NetworkConnectionToClient connection)
        {
            // A disconnect is rare; avoid a separate mutable scratch list during nested participant cleanup.
            foreach (var lease in leases.Values)
                if (ReferenceEquals(lease.Receipt.Connection, connection)) lease.Projection.Release(lease.Receipt.Player);
        }
        internal static void Clear()
        {
            revision++;
            foreach (var lease in leases.Values) lease.Projection.Release(lease.Receipt.Player);
            leases.Clear();
            if (!ticking) scratch.Clear();
            // Native nesting intentionally survives scope resets and settings callbacks.
        }
        internal static void BeginNative(UnitAvatar player)
        {
            if (!NetworkServer.active || !(player is PlayerAvatar avatar) || !avatar) return;
            nativeDepth.TryGetValue(player, out int depth); nativeDepth[player] = depth + 1;
            if (depth == 0) Release(avatar);
        }
        internal static void EndNative(UnitAvatar player, bool reapply = true)
        {
            if (ReferenceEquals(player, null) || !nativeDepth.TryGetValue(player, out int depth)) return;
            if (depth > 1) { nativeDepth[player] = depth - 1; return; }
            nativeDepth.Remove(player);
            if (reapply && player is PlayerAvatar avatar) Reconcile(avatar);
        }
        internal static float NativeHpForSave(UnitAvatar player) => player is PlayerAvatar avatar && leases.TryGetValue(avatar, out var lease)
            ? lease.Projection.NativeHp(avatar) : player.hp;
        internal static bool HasNativeCurse(UnitAvatar player, int expected) => player is PlayerAvatar avatar &&
            leases.TryGetValue(avatar, out var lease) && lease.Projection.HasNativeCurse(player, expected);
    }
}
