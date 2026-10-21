using System;

namespace SephiriaOne
{
    // Fractions of current maximum HP, not stored HP or a fixed-duration window.
    // All sources consume one pool. A requested positive minimum remains
    // responsive at exhaustion; only funded damage can later be refunded.
    internal struct FriendlyFireDamageBudget
    {
        internal const double Capacity = 0.20d;
        private bool started;
        private double remaining, lastTime, lastRefill, lastCapacity;

        internal double Take(double damage, double maxHp, double now, double refillPerSecond = FriendlyFireSettings.DefaultRefillScale,
            double elapsedRateCeiling = double.PositiveInfinity, double minimumDamage = 0, double capacity = Capacity) =>
            Take(damage, maxHp, now, out _, refillPerSecond, elapsedRateCeiling, minimumDamage, capacity);

        internal double Take(double damage, double maxHp, double now, out double charged,
            double refillPerSecond = FriendlyFireSettings.DefaultRefillScale,
            double elapsedRateCeiling = double.PositiveInfinity, double minimumDamage = 0, double capacity = Capacity)
        {
            charged = 0;
            if (!Finite(damage) || damage <= 0 || !Finite(maxHp) || maxHp <= 0 || !Finite(now) ||
                !Finite(refillPerSecond) || refillPerSecond <= 0 || double.IsNaN(elapsedRateCeiling) || elapsedRateCeiling <= 0 ||
                !Finite(minimumDamage) || minimumDamage < 0 || !Finite(capacity) || capacity <= 0) return 0;
            if (!started || now < lastTime)
            { remaining = capacity; started = true; }
            else
                // Rate edits preserve tokens. Never credit elapsed time retroactively
                // at a higher rate or into newly enlarged capacity. Decreases
                // take effect immediately; larger caps refill only after observation.
                remaining = Math.Min(Math.Min(capacity, lastCapacity), remaining +
                    (now - lastTime) * Math.Min(elapsedRateCeiling, Math.Min(lastRefill, refillPerSecond)));
            lastTime = now; lastRefill = refillPerSecond; lastCapacity = capacity;
            double available = Math.Max(0, remaining) * maxHp;
            double allowed = Math.Max(minimumDamage, Math.Min(damage, available));
            charged = Math.Min(available, allowed);
            // Continuous clipping stops at the caller's positive floor. Excess
            // floor damage is deliberately unbudgeted, never debt or free credit.
            if (charged > 0)
            {
                double remainder = available - charged;
                // Use original capacity, not the shrinking last allowance:
                // hundreds of fraction/HP conversions accumulate precision dust
                // that must not manufacture funded allowance at exact depletion.
                remaining = remainder > 0 && remainder <= maxHp * capacity * 1e-12d ? 0 : remainder / maxHp;
            }
            return allowed;
        }

        internal void Refund(double fraction, double capacity = Capacity)
        {
            if (started && Finite(fraction) && fraction > 0 && Finite(capacity) && capacity > 0)
                remaining = Math.Min(Math.Min(capacity, lastCapacity), remaining + fraction);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
