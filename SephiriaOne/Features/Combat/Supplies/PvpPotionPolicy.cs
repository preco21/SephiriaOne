namespace SephiriaOne
{
    internal enum HpPotionRule { Costume, PvpFinite, PvpInfinite }

    internal static class PvpPotionPolicy
    {
        // Shared live combat admission excludes spectators/countdowns. Re-read
        // the option after admission: pruning stale selections can stop a match.
        // No costume setting or legacy preset may override active PvP policy.
        internal static HpPotionRule RuleFor(PlayerAvatar player) => !PvpParticipation.IsActive(player)
            ? HpPotionRule.Costume : SessionSettings.FriendlyFireForHit.InfiniteHpPotions
                ? HpPotionRule.PvpInfinite : HpPotionRule.PvpFinite;

        internal static bool BlocksRabbitInfinite(PlayerAvatar player) => RuleFor(player) != HpPotionRule.Costume;
        internal static bool Applies(PlayerAvatar player) => RuleFor(player) == HpPotionRule.PvpInfinite;
    }
}
