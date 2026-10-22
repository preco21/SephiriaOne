using System;
using System.Globalization;

namespace SephiriaOne
{
    // Integer offsets preserve exact hundredths and make default(T) the shipped policy.
    internal readonly struct PvpRapidSettings
    {
        public const decimal DefaultWindowSeconds = 1m;
        public const decimal DefaultHitsPerSecond = 4m;
        public const decimal DefaultExponent = 0.5m;
        public const decimal DefaultBurstPercent = 75m;
        private const int DefaultBurstHundredths = (int)(DefaultBurstPercent * 100);
        private readonly int windowOffset, rateOffset, exponentOffset, burstOffset;
        public decimal WindowSeconds => (windowOffset + 100) / 100m;
        public decimal HitsPerSecond => (rateOffset + 400) / 100m;
        public decimal Exponent => (exponentOffset + 50) / 100m;
        public decimal BurstPercent => (burstOffset + DefaultBurstHundredths) / 100m;
        public double WindowScale => (windowOffset + 100) / 100d;
        public double RateScale => (rateOffset + 400) / 100d;
        public double ExponentScale => (exponentOffset + 50) / 100d;
        public double BurstScale => (burstOffset + DefaultBurstHundredths) / 10000d;
        public string WindowNumber => WindowSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        public string RateNumber => HitsPerSecond.ToString("0.##", CultureInfo.InvariantCulture);
        public string ExponentNumber => Exponent.ToString("0.##", CultureInfo.InvariantCulture);
        public string BurstNumber => BurstPercent.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => windowOffset != 0 || rateOffset != 0 || exponentOffset != 0 || burstOffset != 0;

        public PvpRapidSettings(decimal windowSeconds = DefaultWindowSeconds, decimal hitsPerSecond = DefaultHitsPerSecond,
            decimal exponent = DefaultExponent, decimal burstPercent = DefaultBurstPercent)
        {
            if (!ValidWindow(windowSeconds)) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (!ValidRate(hitsPerSecond)) throw new ArgumentOutOfRangeException(nameof(hitsPerSecond));
            if (!ValidExponent(exponent)) throw new ArgumentOutOfRangeException(nameof(exponent));
            if (!ValidBurst(burstPercent)) throw new ArgumentOutOfRangeException(nameof(burstPercent));
            windowOffset = (int)(windowSeconds * 100) - 100;
            rateOffset = (int)(hitsPerSecond * 100) - 400;
            exponentOffset = (int)(exponent * 100) - 50;
            burstOffset = (int)(burstPercent * 100) - DefaultBurstHundredths;
        }

        public static bool ValidWindow(decimal value) => Valid(value, 0.1m, 10m);
        public static bool ValidRate(decimal value) => Valid(value, 0.1m, 100m);
        public static bool ValidExponent(decimal value) => Valid(value, 0m, 2m);
        public static bool ValidBurst(decimal value) => Valid(value, 1m, 100m);
        private static bool Valid(decimal value, decimal minimum, decimal maximum) =>
            value >= minimum && value <= maximum && value * 100 == decimal.Truncate(value * 100);
        public bool SameFrequency(PvpRapidSettings other) => windowOffset == other.windowOffset &&
            rateOffset == other.rateOffset && exponentOffset == other.exponentOffset;
    }

    internal readonly struct PvpRapidChange
    {
        public decimal? WindowSeconds { get; }
        public decimal? HitsPerSecond { get; }
        public decimal? Exponent { get; }
        public decimal? BurstPercent { get; }
        public bool Reset { get; }
        public bool IsValid => (!WindowSeconds.HasValue || PvpRapidSettings.ValidWindow(WindowSeconds.Value)) &&
            (!HitsPerSecond.HasValue || PvpRapidSettings.ValidRate(HitsPerSecond.Value)) &&
            (!Exponent.HasValue || PvpRapidSettings.ValidExponent(Exponent.Value)) &&
            (!BurstPercent.HasValue || PvpRapidSettings.ValidBurst(BurstPercent.Value));
        public bool IsReset => Reset || (!WindowSeconds.HasValue || WindowSeconds.Value == PvpRapidSettings.DefaultWindowSeconds) &&
            (!HitsPerSecond.HasValue || HitsPerSecond.Value == PvpRapidSettings.DefaultHitsPerSecond) &&
            (!Exponent.HasValue || Exponent.Value == PvpRapidSettings.DefaultExponent) &&
            (!BurstPercent.HasValue || BurstPercent.Value == PvpRapidSettings.DefaultBurstPercent);
        public PvpRapidChange(decimal? windowSeconds = null, decimal? hitsPerSecond = null, decimal? exponent = null,
            decimal? burstPercent = null, bool reset = false)
        { WindowSeconds = windowSeconds; HitsPerSecond = hitsPerSecond; Exponent = exponent; BurstPercent = burstPercent; Reset = reset; }
        public PvpRapidSettings Apply(PvpRapidSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(PvpRapidChange));
            return Reset ? default : new PvpRapidSettings(WindowSeconds ?? settings.WindowSeconds,
                HitsPerSecond ?? settings.HitsPerSecond, Exponent ?? settings.Exponent, BurstPercent ?? settings.BurstPercent);
        }

        internal static bool TryParse(string key, string text, out PvpRapidChange change)
        {
            change = default;
            bool reset = string.Equals(text, "reset", StringComparison.OrdinalIgnoreCase);
            decimal value = 0;
            if (!reset)
            {
                int point = text.IndexOf('.');
                if (point >= 0 && text.Length - point - 1 > 2 ||
                    !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;
            }
            switch (key.ToLowerInvariant())
            {
                case "window": change = new PvpRapidChange(windowSeconds: reset ? PvpRapidSettings.DefaultWindowSeconds : value); break;
                case "rate": change = new PvpRapidChange(hitsPerSecond: reset ? PvpRapidSettings.DefaultHitsPerSecond : value); break;
                case "exponent": change = new PvpRapidChange(exponent: reset ? PvpRapidSettings.DefaultExponent : value); break;
                case "burst": change = new PvpRapidChange(burstPercent: reset ? PvpRapidSettings.DefaultBurstPercent : value); break;
                default: return false;
            }
            return change.IsValid;
        }
    }
}
