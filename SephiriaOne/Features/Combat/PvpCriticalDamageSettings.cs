using System;

namespace SephiriaOne
{
    internal readonly struct PvpCriticalDamageSettings
    {
        public const decimal DefaultSoftPercent = 30m;
        public const decimal DefaultHardPercent = 70m;
        private static readonly PvpStatLimit DefaultLimit = new PvpStatLimit(DefaultSoftPercent, DefaultHardPercent);
        private readonly bool customLimits;
        private readonly PvpStatLimit limit;
        private PvpStatLimit ActiveLimit => customLimits ? limit : DefaultLimit;
        public bool Enabled { get; }
        public decimal SoftPercent => ActiveLimit.Soft;
        public decimal HardPercent => ActiveLimit.Hard;
        public string SoftNumber => ActiveLimit.SoftNumber;
        public string HardNumber => ActiveLimit.HardNumber;
        public bool HasChanges => Enabled || customLimits;
        public PvpCriticalDamageSettings(bool enabled, decimal softPercent = DefaultSoftPercent,
            decimal hardPercent = DefaultHardPercent)
        {
            if (!ValidLimits(softPercent, hardPercent)) throw new ArgumentOutOfRangeException(nameof(hardPercent));
            Enabled = enabled;
            customLimits = softPercent != DefaultSoftPercent || hardPercent != DefaultHardPercent;
            limit = customLimits ? new PvpStatLimit(softPercent, hardPercent) : default;
        }
        public static bool ValidLimits(decimal soft, decimal hard) => PvpStatLimit.ValidLimits(soft, hard);
        public float Limit(float additionalBonus) => Enabled ? ActiveLimit.Limit(additionalBonus) : additionalBonus;
    }

    internal readonly struct PvpCriticalDamageChange
    {
        public bool? Enabled { get; }
        public decimal? SoftPercent { get; }
        public decimal? HardPercent { get; }
        public bool Reset { get; }
        public bool IsValid => SoftPercent.HasValue == HardPercent.HasValue &&
            (!SoftPercent.HasValue || PvpCriticalDamageSettings.ValidLimits(SoftPercent.Value, HardPercent.Value));
        public bool IsReset => Reset || Enabled == false && !SoftPercent.HasValue && !HardPercent.HasValue;
        public PvpCriticalDamageChange(bool? enabled = null, decimal? softPercent = null, decimal? hardPercent = null, bool reset = false)
        { Enabled = enabled; SoftPercent = softPercent; HardPercent = hardPercent; Reset = reset; }
        public PvpCriticalDamageSettings Apply(PvpCriticalDamageSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(SoftPercent));
            return Reset ? default : new PvpCriticalDamageSettings(Enabled ?? settings.Enabled,
                SoftPercent ?? settings.SoftPercent, HardPercent ?? settings.HardPercent);
        }
    }

}
