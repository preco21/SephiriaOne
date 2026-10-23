namespace SephiriaOne
{
    internal static class PvpPotionPolicy
    {
        // Shared live combat admission excludes spectators/countdowns. Re-read
        // the option after admission: pruning stale selections can stop a match.
        internal static bool BlocksRabbitInfinite(PlayerAvatar player) => SessionSettings.FriendlyFireForHit.BlockRabbitInfinite &&
            PvpParticipation.IsActive(player) && SessionSettings.FriendlyFireForHit.BlockRabbitInfinite;

        internal static bool Applies(PlayerAvatar player) => SessionSettings.FriendlyFireForHit.InfiniteHpPotions &&
            PvpParticipation.IsActive(player) && SessionSettings.FriendlyFireForHit.InfiniteHpPotions;
    }
}
