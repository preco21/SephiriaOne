using System;

namespace SephiriaOne
{
    // Fractions of current maximum HP, not stored HP or a fixed-duration window.
    // Slow attacks retain their softened damage; rapid attacks consume one pool.
    internal struct FriendlyFireDamageBudget
    {
        internal const double Capacity = 0.20d;
        private bool started;
        private double remaining, lastTime, lastRefill;

        internal double Take(double damage, double maxHp, double now, double refillPerSecond = FriendlyFireSettings.DefaultRefillScale)
        {
            if (!Finite(damage) || damage <= 0 || !Finite(maxHp) || maxHp <= 0 || !Finite(now) ||
                !Finite(refillPerSecond) || refillPerSecond <= 0) return 0;
            if (!started || now < lastTime)
            { remaining = Capacity; started = true; }
            else
                // Rate edits preserve tokens. Never credit elapsed time retroactively
                // at a higher rate; a decrease takes effect on the next hit as well.
                remaining = Math.Min(Capacity, remaining + (now - lastTime) * Math.Min(lastRefill, refillPerSecond));
            lastTime = now; lastRefill = refillPerSecond;
            double allowed = Math.Min(damage, remaining * maxHp);
            remaining = Math.Max(0, remaining - allowed / maxHp);
            return allowed;
        }

        internal void Refund(double fraction)
        {
            if (Finite(fraction) && fraction > 0) remaining = Math.Min(Capacity, remaining + fraction);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
