using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct EventSpawnSettings
    {
        private readonly decimal offset;
        public decimal Multiplier => offset + 1;
        public bool HasChanges => offset != 0;
        public string Number => Multiplier.ToString("0.##", CultureInfo.InvariantCulture);
        public EventSpawnSettings(decimal multiplier)
        {
            if (!RelativeMultiplier.IsValid(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
            offset = multiplier - 1;
        }
        public double Probability(double native) => !HasChanges ? native : Math.Max(0, Math.Min(1, native * (double)Multiplier));
    }
}
