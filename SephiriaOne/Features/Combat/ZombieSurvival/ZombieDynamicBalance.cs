using System;

namespace SephiriaOne
{
    // Match-owned, allocation-free policy. Membership includes pending respawns;
    // temporary death/readiness never changes a team's numerical advantage.
    internal struct ZombieDynamicBalance
    {
        internal const double TimeConstant = 1d;
        private bool started;
        private double share, target, time;

        internal void Retarget(int zombies, int humans, double now)
        {
            if (zombies < 1 || humans < 0 || !Finite(now)) return;
            double next = humans / ((double)humans + zombies);
            if (!started) { started = true; share = next; time = now; }
            else Advance(now); // Settle old composition before changing the target.
            target = next;
        }

        private void Advance(double now)
        {
            if (!started || !Finite(now) || now <= time) return;
            if (share != target)
            {
                share = target + (share - target) * Math.Exp(-(now - time) / TimeConstant);
                if (Math.Abs(share - target) < 1e-10d) share = target;
            }
            time = now;
        }
        internal double RefillScale(double now) { Advance(now); return started ? 1d + 2d * share : 1d; }
        internal double IncomingScale(double now) { Advance(now); return started ? 1d - .7d * share : 1d; }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
