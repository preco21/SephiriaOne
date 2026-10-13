using System;

namespace SephiriaOne
{
    // Per-hit diminishing returns. No player stat writes, retained baseline,
    // damage budget or timing state; shields and later native effects stay native.
    internal static class FriendlyFireBalance
    {
        internal static double Soften(double damage, double maxHp)
        {
            if (double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0 ||
                double.IsNaN(maxHp) || double.IsInfinity(maxHp) || maxHp <= 0) return 0;
            double knee = maxHp * 0.05d;
            if (damage <= knee) return damage;
            double span = maxHp * 0.15d;
            double excess = damage - knee;
            // Divide first: even enormous finite hits must not overflow the
            // multiplication or manufacture an invalid native HP/MP value.
            return knee + span / (1d + span / excess);
        }
    }
}
