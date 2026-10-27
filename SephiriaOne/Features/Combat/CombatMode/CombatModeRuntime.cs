using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    // A reversible overlay of the native replicated combat counter, not a
    // second menu/input protocol. Stock guests enforce their own battle gates.
    internal static class CombatModeRuntime
    {
        private sealed class Lease
        {
            internal readonly FriendlyFireParticipants.Receipt Receipt;
            internal sbyte Native;
            internal bool Applied;
            internal Lease(FriendlyFireParticipants.Receipt receipt) { Receipt = receipt; }
            internal void Release()
            {
                var player = Receipt.Player;
                // Do not overwrite an unexpected external writer's value.
                if (Applied && player && player.isInBattle == 1) player.NetworkisInBattle = Native;
                Applied = false;
            }
            internal void Apply()
            {
                var player = Receipt.Player;
                if (Applied || !player || player.isInBattle > 0) return;
                Native = player.isInBattle;
                player.NetworkisInBattle = 1;
                Applied = true;
            }
        }
        private static readonly Dictionary<PlayerAvatar, Lease> leases = new Dictionary<PlayerAvatar, Lease>(ReferenceComparer<PlayerAvatar>.Instance);
        // Kept through Clear: native callbacks may disable/re-enable the option
        // inside StartBattle. Never insert the overlay midway through that call.
        private static readonly Dictionary<UnitAvatar, int> nativeDepth = new Dictionary<UnitAvatar, int>(ReferenceComparer<UnitAvatar>.Instance);
        private static readonly List<PlayerAvatar> scratch = new List<PlayerAvatar>();
        private static bool ticking;
        private static long revision;

        internal static bool Restricted(PlayerAvatar player)
        {
            var settings = SessionSettings.FriendlyFireForDisplay;
            if (!CombatModeFeature.Available || !FriendlyFireFeature.Available || !settings.CombatMode) return false;
            var receipt = FriendlyFireParticipants.Capture(player);
            if (receipt == null) return false;
            // Keep menus locked through countdowns, deaths and waiting between
            // Tournament pairings. Match roster excludes spectators/late joins.
            return CombatMatchRuntime.IsRunning ? CombatMatchRuntime.IsSupplyParticipant(player) :
                settings.Enabled && FriendlyFireParticipants.Includes(player, requireReady: false);
        }

        internal static void Tick()
        {
            if (ticking) return;
            ticking = true;
            try
            {
                var settings = SessionSettings.FriendlyFireForDisplay;
                if (!NetworkServer.active || !CombatModeFeature.Available || !settings.CombatMode ||
                    !settings.Enabled && !CombatMatchRuntime.IsRunning)
                { if (leases.Count != 0) Clear(); return; }
                // Establish the participant scope before snapshotting our revision.
                // Scope creation itself releases leases from the previous session.
                _ = FriendlyFireParticipants.Epoch;
                long version = revision;
                // Snapshot keys: receipt validation may retire the entire scope.
                foreach (var player in leases.Keys) scratch.Add(player);
                foreach (var player in scratch)
                {
                    if (version != revision) return;
                    if (!leases.TryGetValue(player, out var lease)) continue;
                    if (!FriendlyFireParticipants.IsCurrent(lease.Receipt) || !Restricted(player))
                    { lease.Release(); leases.Remove(player); }
                }
                scratch.Clear();
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                    if (spawner && spawner.PlayerAvatar) scratch.Add(spawner.PlayerAvatar);
                foreach (var player in scratch)
                {
                    if (version != revision) return;
                    if (!player || nativeDepth.ContainsKey(player) || !Restricted(player)) continue;
                    var receipt = FriendlyFireParticipants.Capture(player);
                    if (receipt == null || version != revision) continue;
                    if (!leases.TryGetValue(player, out var lease))
                    { lease = new Lease(receipt); leases.Add(player, lease); }
                    lease.Apply();
                }
            }
            finally { scratch.Clear(); ticking = false; }
        }

        internal static void Release(PlayerAvatar player)
        {
            if (ReferenceEquals(player, null) || !leases.TryGetValue(player, out var lease)) return;
            lease.Release(); leases.Remove(player);
        }

        internal static void Clear()
        {
            revision++;
            foreach (var lease in leases.Values) lease.Release();
            leases.Clear();
            if (!ticking) scratch.Clear();
        }

        internal static void BeforeNativeBattle(UnitAvatar player)
        {
            if (!(player is PlayerAvatar avatar)) return;
            nativeDepth.TryGetValue(player, out int depth);
            nativeDepth[player] = depth + 1;
            if (depth == 0 && leases.TryGetValue(avatar, out var lease)) lease.Release();
        }
        internal static void AfterNativeBattle(UnitAvatar player)
        {
            if (!nativeDepth.TryGetValue(player, out int depth)) return;
            if (depth > 1) { nativeDepth[player] = depth - 1; return; }
            nativeDepth.Remove(player);
            Tick();
        }
    }
}
