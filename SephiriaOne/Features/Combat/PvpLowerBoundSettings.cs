using System;
using System.Globalization;

namespace SephiriaOne
{
    // Offset storage makes default(T) the disabled policy with its normal tuning.
    internal readonly struct PvpLowerBoundSettings
    {
        public const decimal DefaultReferencePercent = 4m;
        public const decimal DefaultRefillPercent = 8m;
        private readonly int referenceOffset, refillOffset;
        public bool Enabled { get; }
        public decimal ReferencePercent => (referenceOffset + 400) / 100m;
        public decimal RefillPercent => (refillOffset + 800) / 100m;
        public double ReferenceScale => (referenceOffset + 400) / 10000d;
        public double RefillScale => (refillOffset + 800) / 10000d;
        public string ReferenceNumber => ReferencePercent.ToString("0.##", CultureInfo.InvariantCulture);
        public string RefillNumber => RefillPercent.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => Enabled || referenceOffset != 0 || refillOffset != 0;
        public PvpLowerBoundSettings(bool enabled, decimal referencePercent = DefaultReferencePercent,
            decimal refillPercent = DefaultRefillPercent)
        {
            if (!ValidReferencePercent(referencePercent)) throw new ArgumentOutOfRangeException(nameof(referencePercent));
            if (!ValidRefillPercent(refillPercent)) throw new ArgumentOutOfRangeException(nameof(refillPercent));
            Enabled = enabled;
            referenceOffset = (int)(referencePercent * 100) - 400;
            refillOffset = (int)(refillPercent * 100) - 800;
        }
        public static bool ValidReferencePercent(decimal value) => ValidPercent(value, 25m);
        public static bool ValidRefillPercent(decimal value) => ValidPercent(value, 100m);
        private static bool ValidPercent(decimal value, decimal maximum) =>
            value >= 0.1m && value <= maximum && value * 100 == decimal.Truncate(value * 100);
        internal static bool TryPercent(string text, bool reference, out decimal value)
        {
            string number = (text ?? "").Trim();
            int point = number.IndexOf('.'); value = 0;
            return (point < 0 || number.Length - point - 1 <= 2) &&
                decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) &&
                (reference ? ValidReferencePercent(value) : ValidRefillPercent(value));
        }
    }

    internal readonly struct PvpLowerBoundChange
    {
        public bool? Enabled { get; }
        public decimal? ReferencePercent { get; }
        public decimal? RefillPercent { get; }
        public bool Reset { get; }
        public bool IsValid => (!ReferencePercent.HasValue || PvpLowerBoundSettings.ValidReferencePercent(ReferencePercent.Value)) &&
            (!RefillPercent.HasValue || PvpLowerBoundSettings.ValidRefillPercent(RefillPercent.Value));
        public bool IsReset => Reset || Enabled != true &&
            (!ReferencePercent.HasValue || ReferencePercent.Value == PvpLowerBoundSettings.DefaultReferencePercent) &&
            (!RefillPercent.HasValue || RefillPercent.Value == PvpLowerBoundSettings.DefaultRefillPercent);
        public PvpLowerBoundChange(bool? enabled = null, decimal? referencePercent = null, decimal? refillPercent = null, bool reset = false)
        { Enabled = enabled; ReferencePercent = referencePercent; RefillPercent = refillPercent; Reset = reset; }
        public PvpLowerBoundSettings Apply(PvpLowerBoundSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(ReferencePercent));
            return Reset ? default : new PvpLowerBoundSettings(Enabled ?? settings.Enabled,
                ReferencePercent ?? settings.ReferencePercent, RefillPercent ?? settings.RefillPercent);
        }
    }
}
