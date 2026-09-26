namespace SephiriaOne
{
    internal readonly struct RabbitPotionSettings
    {
        public bool Infinite { get; }
        public bool Share { get; }
        public bool ConsumeMp { get; }
        public bool SuppressSurvival { get; }
        public const int MpCostPerDrink = 10;
        public bool HasChanges => Infinite || Share || ConsumeMp || SuppressSurvival;

        public RabbitPotionSettings(bool infinite, bool share, bool consumeMp = false, bool suppressSurvival = false)
        { Infinite = infinite; Share = share; ConsumeMp = consumeMp; SuppressSurvival = suppressSurvival; }

        public RabbitPotionSettings Apply(RabbitCommand command) => command.Option == RabbitOption.Reset ? default :
            command.Option == RabbitOption.Infinite ? new RabbitPotionSettings(command.Enabled, Share, ConsumeMp, SuppressSurvival) :
            command.Option == RabbitOption.Share ? new RabbitPotionSettings(Infinite, command.Enabled, ConsumeMp, SuppressSurvival) :
            command.Option == RabbitOption.ConsumeMp ? new RabbitPotionSettings(Infinite, Share, command.Enabled, SuppressSurvival) :
            new RabbitPotionSettings(Infinite, Share, ConsumeMp, command.Enabled);
    }
}
