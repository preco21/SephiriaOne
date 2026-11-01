using System;

namespace SephiriaOne
{
    internal readonly struct PvpEvasionSettings
    {
        public const decimal DefaultSoftPercent = 30m;
        public const decimal DefaultHardPercent = 50m;
        private static readonly PvpStatLimit DefaultLimit = new PvpStatLimit(DefaultSoftPercent, DefaultHardPercent);
        private readonly bool customLimits;
        private readonly PvpStatLimit limit;
        private PvpStatLimit ActiveLimit => customLimits ? limit : DefaultLimit;
        private readonly bool disabled;
        public bool Enabled => !disabled;
        public decimal SoftPercent => ActiveLimit.Soft;
        public decimal HardPercent => ActiveLimit.Hard;
        public string SoftNumber => ActiveLimit.SoftNumber;
        public string HardNumber => ActiveLimit.HardNumber;
        public bool HasChanges => disabled || customLimits;
        public PvpEvasionSettings(bool enabled, decimal softPercent = DefaultSoftPercent,
            decimal hardPercent = DefaultHardPercent)
        {
            if (!ValidLimits(softPercent, hardPercent)) throw new ArgumentOutOfRangeException(nameof(hardPercent));
            disabled = !enabled;
            customLimits = softPercent != DefaultSoftPercent || hardPercent != DefaultHardPercent;
            limit = customLimits ? new PvpStatLimit(softPercent, hardPercent) : default;
        }
        public static bool ValidLimits(decimal soft, decimal hard) => hard <= 100m && PvpStatLimit.ValidLimits(soft, hard);
        public float Limit(float nativeChance) => Enabled ? ActiveLimit.Limit(nativeChance) : nativeChance;
    }

    internal readonly struct PvpEvasionChange
    {
        public bool? Enabled { get; }
        public decimal? SoftPercent { get; }
        public decimal? HardPercent { get; }
        public bool Reset { get; }
        public bool IsValid => SoftPercent.HasValue == HardPercent.HasValue &&
            (!SoftPercent.HasValue || HardPercent.HasValue && PvpEvasionSettings.ValidLimits(SoftPercent.Value, HardPercent.Value));
        public bool IsReset => Reset || Enabled == false && !SoftPercent.HasValue && !HardPercent.HasValue;
        public PvpEvasionChange(bool? enabled = null, decimal? softPercent = null, decimal? hardPercent = null, bool reset = false)
        { Enabled = enabled; SoftPercent = softPercent; HardPercent = hardPercent; Reset = reset; }
        public PvpEvasionSettings Apply(PvpEvasionSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(SoftPercent));
            return Reset ? default : new PvpEvasionSettings(Enabled ?? settings.Enabled,
                SoftPercent ?? settings.SoftPercent, HardPercent ?? settings.HardPercent);
        }
    }

}
