using System;
using System.Globalization;

namespace SephiriaOne
{
    // Shared diminishing-return curve in the caller's native stat units.
    // Offset storage keeps default(T) equivalent to the explicit default policy.
    internal readonly struct PvpStatLimit
    {
        public const decimal DefaultSoft = 50m;
        public const decimal DefaultHard = 100m;
        private readonly int softOffset, hardOffset;
        private readonly float reciprocalSpanOffset;

        public decimal Soft => (softOffset + 5000) / 100m;
        public decimal Hard => (hardOffset + 10000) / 100m;
        public string SoftNumber => Soft.ToString("0.##", CultureInfo.InvariantCulture);
        public string HardNumber => Hard.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasChanges => softOffset != 0 || hardOffset != 0;

        public PvpStatLimit(decimal soft = DefaultSoft, decimal hard = DefaultHard)
        {
            if (!ValidLimits(soft, hard)) throw new ArgumentOutOfRangeException(nameof(hard));

            softOffset = (int)(soft * 100) - 5000;
            hardOffset = (int)(hard * 100) - 10000;
            // Cache the only division at policy-edit time, never per hit.
            float width = (float)(hard - soft);
            reciprocalSpanOffset = (width > 0 ? 1f / (2f * width) : 0f) - 0.01f;
        }

        public static bool ValidLimits(decimal soft, decimal hard) =>
            soft >= 0 && soft <= hard && hard <= 10000 &&
            soft * 100 == decimal.Truncate(soft * 100) && hard * 100 == decimal.Truncate(hard * 100);

        public float Limit(float nativeBonus)
        {
            float soft = (softOffset + 5000) * 0.01f;
            if (nativeBonus <= soft) return nativeBonus;
            float hard = (hardOffset + 10000) * 0.01f, width = hard - soft;
            float delta = nativeBonus - soft;
            if (delta >= 2f * width) return hard;
            // C1 join: slope 1 at soft, slope 0 at hard. An extra 2*(hard-soft)
            // native points exhausts the interval without a discontinuous clamp.
            float t = delta * (reciprocalSpanOffset + 0.01f);
            return Math.Min(hard, soft + width * t * (2f - t));
        }
    }
}
