using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal struct FriendlyFireSettings
    {
        public bool Enabled, Balance, InfiniteHpPotions;
        private bool allowRabbitInfinite;
        public bool BlockRabbitInfinite { get => !allowRabbitInfinite; set => allowRabbitInfinite = !value; }
    }
    internal static class FriendlyFireFeature { internal static bool Available = true; }
    internal static class FriendlyFireParticipants
    {
        internal sealed class Receipt { internal PlayerAvatar Player; internal Mirror.NetworkConnectionToClient Connection; }
        internal static Receipt Capture(PlayerAvatar player) => new() { Player = player, Connection = player.spawner.connectionToClient };
        internal static bool IsCurrent(Receipt receipt) => receipt?.Player &&
            ReferenceEquals(receipt.Connection, receipt.Player.spawner.connectionToClient) &&
            Mirror.NetworkServer.connections.ContainsValue(receipt.Connection);
        internal static readonly HashSet<PlayerAvatar> Selected = new();
        internal static bool Includes(PlayerAvatar player, bool requireReady = true) =>
            (Selected.Count == 0 || Selected.Contains(player)) && HostStateAdapter.IsReady(player.spawner);
    }
    internal static class CombatMatchRuntime
    {
        internal static Func<PlayerAvatar, bool> ParticipantRule;
        internal static bool AllowsParticipant(PlayerAvatar player) => ParticipantRule?.Invoke(player) ?? true;
    }
}
