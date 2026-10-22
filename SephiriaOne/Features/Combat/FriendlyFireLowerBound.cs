using System;

namespace SephiriaOne
{
    internal static class FriendlyFireLowerBound
    {
        // Damage is already native-resolved and percentage-scaled. Below twice
        // the HP-relative reference, lift halfway toward that threshold. Keep
        // original ordering; never manufacture damage from an invalid/zero hit.
        // This is a request, not a guaranteed floor: both budgets still apply.
        internal static double RequestedBonus(double damage, double maxHp, double referenceScale)
        {
            if (!Finite(damage) || damage <= 0 || !Finite(maxHp) || maxHp <= 0 ||
                !Finite(referenceScale) || referenceScale <= 0) return 0;
            double reference = maxHp * referenceScale;
            return Finite(reference) ? Math.Max(0, reference - damage * .5d) : 0;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
