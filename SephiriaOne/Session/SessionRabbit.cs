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
            message = L.T("Only the host can change Wing-Eared Rabbit settings.");
            if (!NetworkServer.active) return false;
            bool available = command.Option == RabbitOption.LevelUpPotion ? RabbitLevelUpFeature.Available : RabbitPotionFeature.Available;
            if (!command.IsReset && !available)
            {
                message = command.Option == RabbitOption.LevelUpPotion ?
                    L.T("Rabbit level-up potion compatibility checks failed. Off/reset remain available; see Player.log.") :
                    L.T("Rabbit potion compatibility checks failed. Off/reset remain available; see Player.log.");
                return false;
            }
            if (!PrepareCommand("rabbit", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("rabbit", context.CreateBatch(), () => policy.Record(command), out message)) return false;
            message = DescribeRabbit(policy.RabbitPotions);
            return true;
        }

        internal static string DescribeRabbit(RabbitPotionSettings settings) =>
            L.F("Wing-Eared Rabbit HP potions: infinite uses {0}; nearby healing {1}; {2} MP per drink {3}; Survival random-stat suppression {4}.",
                L.T(settings.Infinite ? "on" : "off"), L.T(settings.Share ? "on" : "off"), settings.MpCostPerDrink,
                L.T(settings.ConsumeMp ? "on" : "off"), L.T(settings.SuppressSurvival ? "on" : "off")) + " " +
            L.F("Level-up non-HP/MP potion: {0}.", L.T(settings.LevelUpPotion ? "on" : "off"));
    }
}
