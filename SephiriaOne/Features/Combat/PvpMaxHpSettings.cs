using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct PvpMaxHpSettings
    {
        public const decimal DefaultBaseline = 100m;
        public const decimal DefaultSoft = 50m;
        public const decimal DefaultHard = 100m;
        private readonly int baselineOffset;
        private readonly PvpStatLimit limit;
        public bool Enabled { get; }
        public decimal Baseline => (baselineOffset + 10000) / 100m;
        public decimal Soft => limit.Soft;
        public decimal Hard => limit.Hard;
        public string BaselineNumber => Baseline.ToString("0.##", CultureInfo.InvariantCulture);
        public string SoftNumber => limit.SoftNumber;
        public string HardNumber => limit.HardNumber;
        public bool HasChanges => Enabled || baselineOffset != 0 || limit.HasChanges;

        public PvpMaxHpSettings(bool enabled, decimal baseline = DefaultBaseline,
            decimal soft = DefaultSoft, decimal hard = DefaultHard)
        {
            if (!ValidLimits(baseline, soft, hard)) throw new ArgumentOutOfRangeException(nameof(hard));
            Enabled = enabled;
            baselineOffset = (int)(baseline * 100) - 10000;
            limit = new PvpStatLimit(soft, hard);
        }

        public static bool ValidLimits(decimal baseline, decimal soft, decimal hard) =>
            baseline >= 1 && baseline <= 10000 && baseline * 100 == decimal.Truncate(baseline * 100) &&
            PvpStatLimit.ValidLimits(soft, hard);

        public float Limit(float nativeMaxHp)
        {
            float baseline = (baselineOffset + 10000) * 0.01f;
            return !Enabled || nativeMaxHp <= baseline ? nativeMaxHp : baseline + limit.Limit(nativeMaxHp - baseline);
        }

        internal static bool TryNumber(string text, out decimal value)
        {
            string number = (text ?? "").Trim();
            int point = number.IndexOf('.'); value = 0;
            return (point < 0 || number.Length - point - 1 <= 2) &&
                decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) &&
                value >= 0 && value <= 10000;
        }
    }

    internal readonly struct PvpMaxHpChange
    {
        public bool? Enabled { get; }
        public decimal? Baseline { get; }
        public decimal? Soft { get; }
        public decimal? Hard { get; }
        public bool Reset { get; }
        public bool IsValid => Baseline.HasValue == Soft.HasValue && Soft.HasValue == Hard.HasValue &&
            (!Baseline.HasValue || PvpMaxHpSettings.ValidLimits(Baseline.Value, Soft.GetValueOrDefault(), Hard.GetValueOrDefault()));
        public bool IsReset => Reset || Enabled == false && !Baseline.HasValue && !Soft.HasValue && !Hard.HasValue;
        public PvpMaxHpChange(bool? enabled = null, decimal? baseline = null, decimal? soft = null, decimal? hard = null, bool reset = false)
        { Enabled = enabled; Baseline = baseline; Soft = soft; Hard = hard; Reset = reset; }
        public PvpMaxHpSettings Apply(PvpMaxHpSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(Baseline));
            return Reset ? default : new PvpMaxHpSettings(Enabled ?? settings.Enabled,
                Baseline ?? settings.Baseline, Soft ?? settings.Soft, Hard ?? settings.Hard);
        }
    }
}
