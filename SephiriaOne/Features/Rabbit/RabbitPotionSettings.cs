namespace SephiriaOne
{
    internal readonly struct RabbitPotionSettings
    {
        public bool Infinite { get; }
        public bool Share { get; }
        public bool ConsumeMp { get; }
        public bool SuppressSurvival { get; }
        public bool LevelUpPotion { get; }
        public const int DefaultMpCostPerDrink = 10;
        public const int MaximumMpCostPerDrink = 10000;
        // An all-zero struct still represents the native/default policy with a 10 MP fee.
        private readonly int mpCostOffset;
        public int MpCostPerDrink => DefaultMpCostPerDrink + mpCostOffset;
        public bool HasChanges => Infinite || Share || ConsumeMp || SuppressSurvival || mpCostOffset != 0 || LevelUpPotion;

        public RabbitPotionSettings(bool infinite, bool share, bool consumeMp = false, bool suppressSurvival = false,
            int mpCost = DefaultMpCostPerDrink, bool levelUpPotion = false)
        {
            if (mpCost < 0 || mpCost > MaximumMpCostPerDrink) throw new System.ArgumentOutOfRangeException(nameof(mpCost));
            Infinite = infinite; Share = share; ConsumeMp = consumeMp; SuppressSurvival = suppressSurvival;
            mpCostOffset = mpCost - DefaultMpCostPerDrink;
            LevelUpPotion = levelUpPotion;
        }

        public RabbitPotionSettings Apply(RabbitCommand command) => command.Option == RabbitOption.Reset ? default :
            command.Option == RabbitOption.Infinite ? new RabbitPotionSettings(command.Enabled, Share, ConsumeMp, SuppressSurvival, MpCostPerDrink, LevelUpPotion) :
            command.Option == RabbitOption.Share ? new RabbitPotionSettings(Infinite, command.Enabled, ConsumeMp, SuppressSurvival, MpCostPerDrink, LevelUpPotion) :
            command.Option == RabbitOption.ConsumeMp ? new RabbitPotionSettings(Infinite, Share, command.Enabled, SuppressSurvival, MpCostPerDrink, LevelUpPotion) :
            command.Option == RabbitOption.MpAmount ? new RabbitPotionSettings(Infinite, Share, command.Enabled, SuppressSurvival, command.Amount, LevelUpPotion) :
            command.Option == RabbitOption.LevelUpPotion ? new RabbitPotionSettings(Infinite, Share, ConsumeMp, SuppressSurvival, MpCostPerDrink, command.Enabled) :
            new RabbitPotionSettings(Infinite, Share, ConsumeMp, command.Enabled, MpCostPerDrink, LevelUpPotion);
    }
}
