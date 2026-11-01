namespace SephiriaOne
{
    internal enum HpPotionRule { Costume, PvpFinite, PvpInfinite }

    internal readonly struct HpPotionUsePolicy
    {
        internal readonly HpPotionRule Rule;
        internal readonly RabbitPotionSettings Rabbit;
        internal HpPotionUsePolicy(HpPotionRule rule, RabbitPotionSettings rabbit)
        { Rule = rule; Rabbit = rabbit; }
        internal bool RequiresContext => Rule == HpPotionRule.PvpInfinite || Rabbit.Infinite ||
            Rabbit.Share || Rabbit.ConsumeMp || Rabbit.SuppressSurvival;
        // Finite PvP overrides costume retention, never the independently configured fee.
        internal int MpCost => Rule == HpPotionRule.PvpInfinite || !Rabbit.ConsumeMp ? 0 : Rabbit.MpCostPerDrink;
        internal bool CostumeInfinite => Rule == HpPotionRule.Costume && Rabbit.Infinite;
    }

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

        internal static HpPotionUsePolicy ResolveUse(PlayerAvatar player) => new HpPotionUsePolicy(
            RuleFor(player), player.currentCostume == "HolyRabbit" ? SessionSettings.RabbitPotionsForUse : default);
    }
}
