using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Event consumers resolve policy at the moment of use; no per-avatar
        // markers, replayed effects, or dependency on the last sync frame.
        internal static RabbitPotionSettings RabbitPotionsForUse => EnsureResourceScope() ? policy.RabbitPotions : default;

        internal static RabbitPotionSettings RabbitPotionsForDisplay => enabled && NetworkServer.active &&
            dungeon && dungeon.isServer && dungeon.netId != 0 && ReferenceEquals(dungeon, DungeonManager.Instance) ?
            policy.RabbitPotions : default;

        public static bool TryExecuteRabbit(RabbitCommand command, out string message)
        {
            message = "Only the host can change Wing-Eared Rabbit settings.";
            if (!NetworkServer.active) return false;
            if (!command.IsReset && !RabbitPotionFeature.Available)
            { message = "Rabbit potion compatibility checks failed. Off/reset remain available; see Player.log."; return false; }
            if (!PrepareCommand("rabbit", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("rabbit", context.CreateBatch(), () => policy.Record(command), out message)) return false;
            message = DescribeRabbit(policy.RabbitPotions);
            return true;
        }

        internal static string DescribeRabbit(RabbitPotionSettings settings) =>
            "Wing-Eared Rabbit HP potions: infinite uses " + (settings.Infinite ? "on" : "off") +
            "; nearby healing " + (settings.Share ? "on" : "off") +
            "; " + RabbitPotionSettings.MpCostPerDrink + " MP per drink " + (settings.ConsumeMp ? "on" : "off") +
            "; Survival random-stat suppression " + (settings.SuppressSurvival ? "on" : "off") + ".";
    }
}
