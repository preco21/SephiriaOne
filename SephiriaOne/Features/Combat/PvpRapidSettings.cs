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
        public const decimal DefaultBurstPercent = 70m;
        public const decimal DefaultResetSeconds = 1m;
        private const int DefaultBurstHundredths = (int)(DefaultBurstPercent * 100);
        private const int DefaultResetHundredths = (int)(DefaultResetSeconds * 100);
        private readonly int windowOffset, rateOffset, exponentOffset, burstOffset;
        private readonly int resetOffset;
        private readonly bool periodicResetDisabled;
        public bool PeriodicReset => !periodicResetDisabled;
        public decimal ResetSeconds => (resetOffset + DefaultResetHundredths) / 100m;
        public double ResetScale => (resetOffset + DefaultResetHundredths) / 100d;
        public string ResetNumber => ResetSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasResetChanges => periodicResetDisabled || resetOffset != 0;
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
        public bool HasChanges => windowOffset != 0 || rateOffset != 0 || exponentOffset != 0 || burstOffset != 0 || HasResetChanges;

        public PvpRapidSettings(decimal windowSeconds = DefaultWindowSeconds, decimal hitsPerSecond = DefaultHitsPerSecond,
            decimal exponent = DefaultExponent, decimal burstPercent = DefaultBurstPercent,
            bool periodicReset = true, decimal resetSeconds = DefaultResetSeconds)
        {
            if (!ValidWindow(windowSeconds)) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (!ValidRate(hitsPerSecond)) throw new ArgumentOutOfRangeException(nameof(hitsPerSecond));
            if (!ValidExponent(exponent)) throw new ArgumentOutOfRangeException(nameof(exponent));
            if (!ValidBurst(burstPercent)) throw new ArgumentOutOfRangeException(nameof(burstPercent));
            if (!ValidReset(resetSeconds)) throw new ArgumentOutOfRangeException(nameof(resetSeconds));
            periodicResetDisabled = !periodicReset;
            resetOffset = (int)(resetSeconds * 100) - DefaultResetHundredths;
            windowOffset = (int)(windowSeconds * 100) - 100;
            rateOffset = (int)(hitsPerSecond * 100) - 400;
            exponentOffset = (int)(exponent * 100) - 50;
            burstOffset = (int)(burstPercent * 100) - DefaultBurstHundredths;
        }

        public static bool ValidWindow(decimal value) => Valid(value, 0.1m, 10m);
        public static bool ValidRate(decimal value) => Valid(value, 0.1m, 100m);
        public static bool ValidExponent(decimal value) => Valid(value, 0m, 2m);
        public static bool ValidBurst(decimal value) => Valid(value, 1m, 100m);
        public static bool ValidReset(decimal value) => Valid(value, .1m, 60m);
        private static bool Valid(decimal value, decimal minimum, decimal maximum) =>
            value >= minimum && value <= maximum && value * 100 == decimal.Truncate(value * 100);
        public bool SameFrequency(PvpRapidSettings other) => windowOffset == other.windowOffset &&
            rateOffset == other.rateOffset && exponentOffset == other.exponentOffset;
        public bool SameRecovery(PvpRapidSettings other) => resetOffset == other.resetOffset && periodicResetDisabled == other.periodicResetDisabled;
    }

    internal readonly struct PvpRapidChange
    {
        public decimal? WindowSeconds { get; }
        public decimal? HitsPerSecond { get; }
        public decimal? Exponent { get; }
        public decimal? BurstPercent { get; }
        public bool? PeriodicReset { get; }
        public decimal? ResetSeconds { get; }
        public bool Reset { get; }
        private readonly bool recoveryReset;
        public bool IsValid => (!WindowSeconds.HasValue || PvpRapidSettings.ValidWindow(WindowSeconds.Value)) &&
            (!HitsPerSecond.HasValue || PvpRapidSettings.ValidRate(HitsPerSecond.Value)) &&
            (!Exponent.HasValue || PvpRapidSettings.ValidExponent(Exponent.Value)) &&
            (!BurstPercent.HasValue || PvpRapidSettings.ValidBurst(BurstPercent.Value)) &&
            (!ResetSeconds.HasValue || PvpRapidSettings.ValidReset(ResetSeconds.Value));
        public bool IsReset => Reset || (!WindowSeconds.HasValue || WindowSeconds.Value == PvpRapidSettings.DefaultWindowSeconds) &&
            (!HitsPerSecond.HasValue || HitsPerSecond.Value == PvpRapidSettings.DefaultHitsPerSecond) &&
            (!Exponent.HasValue || Exponent.Value == PvpRapidSettings.DefaultExponent) &&
            (!BurstPercent.HasValue || BurstPercent.Value == PvpRapidSettings.DefaultBurstPercent) &&
            (!PeriodicReset.HasValue || !PeriodicReset.Value || recoveryReset) &&
            (!ResetSeconds.HasValue || ResetSeconds.Value == PvpRapidSettings.DefaultResetSeconds);
        public PvpRapidChange(decimal? windowSeconds = null, decimal? hitsPerSecond = null, decimal? exponent = null,
            decimal? burstPercent = null, bool reset = false, bool? periodicReset = null, decimal? resetSeconds = null, bool recoveryReset = false)
        { WindowSeconds = windowSeconds; HitsPerSecond = hitsPerSecond; Exponent = exponent; BurstPercent = burstPercent; Reset = reset;
            PeriodicReset = recoveryReset ? true : periodicReset; ResetSeconds = resetSeconds; this.recoveryReset = recoveryReset; }
        public PvpRapidSettings Apply(PvpRapidSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(PvpRapidChange));
            return Reset ? default : new PvpRapidSettings(WindowSeconds ?? settings.WindowSeconds,
                HitsPerSecond ?? settings.HitsPerSecond, Exponent ?? settings.Exponent, BurstPercent ?? settings.BurstPercent,
                PeriodicReset ?? settings.PeriodicReset, ResetSeconds ?? settings.ResetSeconds);
        }

        internal static bool TryParse(string key, string text, out PvpRapidChange change)
        {
            change = default;
            bool reset = string.Equals(text, "reset", StringComparison.OrdinalIgnoreCase);
            if (key.Equals("recovery", StringComparison.OrdinalIgnoreCase))
            {
                if (!reset && !text.Equals("on", StringComparison.OrdinalIgnoreCase) && !text.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
                change = new PvpRapidChange(periodicReset: text.Equals("on", StringComparison.OrdinalIgnoreCase), recoveryReset: reset);
                return true;
            }
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
                case "interval": change = new PvpRapidChange(resetSeconds: reset ? PvpRapidSettings.DefaultResetSeconds : value); break;
                default: return false;
            }
            return change.IsValid;
        }
    }
}
