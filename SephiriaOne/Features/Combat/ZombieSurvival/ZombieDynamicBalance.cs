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
        private double refill, incoming, humanIncoming;

        internal void Retarget(int zombies, int humans, double now)
        {
            if (zombies < 1 || humans < 0 || !Finite(now)) return;
            double next = humans / ((double)humans + zombies);
            if (!started) { started = true; share = next; time = now; UpdateScales(); }
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
                UpdateScales();
            }
            time = now;
        }
        private void UpdateScales()
        {
            // Smoothstep of bounded team imbalance: neutral at parity, with no
            // slope jump when the favored side changes. Cache all three outputs.
            double imbalance = 2d * share - 1d;
            double magnitude = Math.Abs(imbalance);
            double strength = magnitude * magnitude * (3d - 2d * magnitude);
            bool minorityZombie = imbalance >= 0;
            // Majority Zombies lose a little more refill as their share grows;
            // minority bonuses and both incoming-damage curves stay independent.
            refill = 1d + (minorityZombie ? .5d : -.5d) * strength;
            incoming = 1d + (minorityZombie ? -.2d : .6d) * strength;
            humanIncoming = minorityZombie ? 1d : 1d - .4d * strength;
        }
        internal double RefillScale(double now) { Advance(now); return started ? refill : 1d; }
        internal double IncomingScale(double now) { Advance(now); return started ? incoming : 1d; }
        internal double HumanIncomingScale(double now) { Advance(now); return started ? humanIncoming : 1d; }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
