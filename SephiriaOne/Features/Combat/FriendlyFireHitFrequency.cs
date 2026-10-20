using System;

namespace SephiriaOne
{
    // Eight fixed time buckets approximate the last second to 125 ms. Storage
    // is allocated once per active pair/category/lifetime, never once per hit. No polling.
    internal struct FriendlyFireHitFrequency
    {
        private const int BucketCount = 8;
        private const double BucketSeconds = 1d / BucketCount;
        private const double FullDamageHitsPerSecond = 4;
        private int[] buckets;
        private int index;
        private long total;
        private double lastSlot, lastTime;

        internal double Next(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return 0;
            double slot = Math.Floor(now / BucketSeconds);
            if (double.IsInfinity(slot)) return 0;
            if (buckets == null) { buckets = new int[BucketCount]; lastSlot = slot; }
            if (now < lastTime || slot - lastSlot >= BucketCount)
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
            lastSlot = slot; lastTime = now;
            // Saturation protects arithmetic without unbounded hit storage.
            if (buckets[index] < int.MaxValue) { buckets[index]++; total++; }
            return total <= FullDamageHitsPerSecond ? 1 : Math.Sqrt(FullDamageHitsPerSecond / total);
        }
    }
}
