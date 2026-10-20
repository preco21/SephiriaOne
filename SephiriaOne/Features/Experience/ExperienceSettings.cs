namespace SephiriaOne
{
    internal readonly struct ExperienceSettings
    {
        public bool Block { get; }
        public bool RabbitExempt { get; }
        public bool HasChanges => Block || RabbitExempt;
        public ExperienceSettings(bool block, bool rabbitExempt)
        { Block = block; RabbitExempt = rabbitExempt; }
        public bool Blocks(string costume) => Block && !(RabbitExempt && costume == "HolyRabbit");
        public ExperienceSettings Apply(ExperienceCommand command) =>
            command.Operation == ExperienceOperation.Reset ? default :
            command.Operation == ExperienceOperation.Block ? new ExperienceSettings(command.Enabled, RabbitExempt) :
            new ExperienceSettings(Block, command.Enabled);
    }
}
