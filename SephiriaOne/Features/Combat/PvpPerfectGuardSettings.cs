using System;
using System.Globalization;

namespace SephiriaOne
{
    // Exact hundredths with an offset keep default(T) at the shipped off/x2 policy.
    internal readonly struct PvpPerfectGuardSettings
    {
        public const decimal DefaultMultiplier = 2m;
        private readonly int multiplierOffset;
        public bool Enabled { get; }
        public decimal Multiplier => (multiplierOffset + 200) / 100m;
        public int MultiplierHundredths => multiplierOffset + 200;
        public double Scale => (multiplierOffset + 200) / 100d;
        public string Number => Multiplier.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => Enabled || multiplierOffset != 0;

        public PvpPerfectGuardSettings(bool enabled = false, decimal multiplier = DefaultMultiplier)
        {
            if (!ValidMultiplier(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
            Enabled = enabled;
            multiplierOffset = (int)(multiplier * 100) - 200;
        }

        public static bool ValidMultiplier(decimal value) => value >= 1m && value <= 100m &&
            value * 100 == decimal.Truncate(value * 100);

        internal static bool TryMultiplier(string text, out decimal multiplier)
        {
            string number = (text ?? "").Trim();
            if (number.StartsWith("x", StringComparison.OrdinalIgnoreCase)) number = number.Substring(1);
            int point = number.IndexOf('.');
            multiplier = 0;
            return (point < 0 || number.Length - point - 1 <= 2) &&
                decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out multiplier) &&
                ValidMultiplier(multiplier);
        }
    }

    internal readonly struct PvpPerfectGuardChange
    {
        public bool? Enabled { get; }
        public decimal? Multiplier { get; }
        public bool Reset { get; }
        public bool IsValid => !Multiplier.HasValue || PvpPerfectGuardSettings.ValidMultiplier(Multiplier.Value);
        public bool IsReset => Reset || Enabled != true &&
            (!Multiplier.HasValue || Multiplier.Value == PvpPerfectGuardSettings.DefaultMultiplier);
        public PvpPerfectGuardChange(bool? enabled = null, decimal? multiplier = null, bool reset = false)
        { Enabled = enabled; Multiplier = multiplier; Reset = reset; }
        public PvpPerfectGuardSettings Apply(PvpPerfectGuardSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(Multiplier));
            return Reset ? default : new PvpPerfectGuardSettings(Enabled ?? settings.Enabled, Multiplier ?? settings.Multiplier);
        }
    }
}
