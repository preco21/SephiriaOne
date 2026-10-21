using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct PvpArtifactRateSettings
    {
        public const decimal DefaultSeconds = 2m;
        private readonly int intervalOffset;
        public bool Enabled { get; }
        public decimal Seconds => (intervalOffset + 200) / 100m;
        public float Interval => (intervalOffset + 200) / 100f;
        public string Number => Seconds.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => Enabled || intervalOffset != 0;
        public PvpArtifactRateSettings(bool enabled, decimal seconds = DefaultSeconds)
        {
            if (!ValidSeconds(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Enabled = enabled; intervalOffset = (int)(seconds * 100) - 200;
        }
        public static bool ValidSeconds(decimal value) => value >= 0.05m && value <= 60m && value * 100 == decimal.Truncate(value * 100);
        internal static bool TrySeconds(string text, out decimal value)
        {
            string number = (text ?? "").Trim();
            int point = number.IndexOf('.'); value = 0;
            return (point < 0 || number.Length - point - 1 <= 2) &&
                decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && ValidSeconds(value);
        }
    }

    internal readonly struct PvpArtifactRateChange
    {
        public bool? Enabled { get; }
        public decimal? Seconds { get; }
        public bool Reset { get; }
        public bool IsValid => !Seconds.HasValue || PvpArtifactRateSettings.ValidSeconds(Seconds.Value);
        public bool IsReset => Reset || Enabled != true && (!Seconds.HasValue || Seconds.Value == PvpArtifactRateSettings.DefaultSeconds);
        public PvpArtifactRateChange(bool? enabled = null, decimal? seconds = null, bool reset = false)
        { Enabled = enabled; Seconds = seconds; Reset = reset; }
        public PvpArtifactRateSettings Apply(PvpArtifactRateSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(Seconds));
            return Reset ? default : new PvpArtifactRateSettings(Enabled ?? settings.Enabled, Seconds ?? settings.Seconds);
        }
    }
}
