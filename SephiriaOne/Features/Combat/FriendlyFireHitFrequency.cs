using System;

namespace SephiriaOne
{
    // Eight fixed buckets approximate the configured rolling window. Storage
    // is allocated once per active pair/category/lifetime, never once per hit. No polling.
    internal struct FriendlyFireHitFrequency
    {
        private const int BucketCount = 8;
        private int[] buckets;
        private int index;
        private long total;
        private double lastSlot, lastTime, lastBucketSeconds;

        internal double Next(double now, PvpRapidSettings settings = default)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return 0;
            double bucketSeconds = settings.WindowScale / BucketCount;
            double slot = Math.Floor(now / bucketSeconds);
            if (double.IsInfinity(slot)) return 0;
            if (buckets == null) { buckets = new int[BucketCount]; lastSlot = slot; }
            if (lastBucketSeconds != bucketSeconds || now < lastTime || slot - lastSlot >= BucketCount)
            { Array.Clear(buckets, 0, BucketCount); total = 0; index = 0; }
            else
            {
                int steps = (int)(slot - lastSlot);
                for (int i = 0; i < steps; i++)
                {
                    index = (index + 1) % BucketCount;
                    total -= buckets[index]; buckets[index] = 0;
                }
            }
            lastSlot = slot; lastTime = now; lastBucketSeconds = bucketSeconds;
            // Saturation protects arithmetic without unbounded hit storage.
            if (buckets[index] < int.MaxValue) { buckets[index]++; total++; }
            double threshold = Math.Max(1, settings.RateScale * settings.WindowScale);
            double exponent = settings.ExponentScale;
            if (total <= threshold || exponent == 0) return 1;
            double ratio = threshold / total;
            // Preserve the inexpensive default path; arbitrary strength is
            // evaluated only above the configured rate, never every frame.
            if (exponent == 0.5) return Math.Sqrt(ratio);
            if (exponent == 1) return ratio;
            if (exponent == 2) return ratio * ratio;
            return Math.Pow(ratio, exponent);
        }
    }
}
