using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct PvpArtifactRangeSettings
    {
        public const decimal DefaultMeters = 5m;
        private readonly int offset;
        public decimal Meters => (offset + 500) / 100m;
        public float Squared { get { float radius = (offset + 500) / 100f; return radius * radius; } }
        public string Number => Meters.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => offset != 0;
        public PvpArtifactRangeSettings(decimal meters)
        {
            if (!ValidMeters(meters)) throw new ArgumentOutOfRangeException(nameof(meters));
            offset = (int)(meters * 100) - 500;
        }
        public static bool ValidMeters(decimal value) => value >= 0.5m && value <= 50m && value * 100 == decimal.Truncate(value * 100);
        internal static bool TryMeters(string text, out decimal value)
        {
            string number = (text ?? "").Trim();
            int point = number.IndexOf('.'); value = 0;
            return (point < 0 || number.Length - point - 1 <= 2) &&
                decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && ValidMeters(value);
        }
    }
}
