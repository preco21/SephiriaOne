namespace SephiriaOne
{
    internal readonly struct PvpAmplificationSettings
    {
        public const decimal DefaultSoftPercent = PvpStatLimit.DefaultSoft;
        public const decimal DefaultHardPercent = PvpStatLimit.DefaultHard;
        private readonly PvpStatLimit limit;
        public bool Enabled { get; }
        public decimal SoftPercent => limit.Soft;
        public decimal HardPercent => limit.Hard;
        public string SoftNumber => limit.SoftNumber;
        public string HardNumber => limit.HardNumber;
        public bool HasChanges => Enabled || limit.HasChanges;
        public PvpAmplificationSettings(bool enabled, decimal softPercent = DefaultSoftPercent,
            decimal hardPercent = DefaultHardPercent)
        { Enabled = enabled; limit = new PvpStatLimit(softPercent, hardPercent); }
        public static bool ValidLimits(decimal soft, decimal hard) => PvpStatLimit.ValidLimits(soft, hard);
        public float Limit(float nativeBonus) => Enabled ? limit.Limit(nativeBonus) : nativeBonus;
    }
}
