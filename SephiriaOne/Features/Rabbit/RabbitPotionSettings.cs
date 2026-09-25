namespace SephiriaOne
{
    internal readonly struct RabbitPotionSettings
    {
        public bool Infinite { get; }
        public bool Share { get; }
        public bool HasChanges => Infinite || Share;

        public RabbitPotionSettings(bool infinite, bool share)
        { Infinite = infinite; Share = share; }

        public RabbitPotionSettings Apply(RabbitCommand command) => command.Option == RabbitOption.Reset ? default :
            command.Option == RabbitOption.Infinite ? new RabbitPotionSettings(command.Enabled, Share) :
            new RabbitPotionSettings(Infinite, command.Enabled);
    }
}
