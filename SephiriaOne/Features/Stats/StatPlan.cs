#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal readonly struct StatSnapshot
    {
        public StatDefinition Stat { get; }
        public int Raw { get; }
        public int Contribution { get; }
        public int Bonus { get; }
        public int Amplifier { get; }

        public StatSnapshot(StatDefinition stat, int raw, int contribution, int bonus, int amplifier)
        {
            Stat = stat;
            Raw = raw;
            Contribution = contribution;
            Bonus = bonus;
            Amplifier = amplifier;
        }
    }

    internal readonly struct StatUpdate
    {
        public int Raw { get; }
        public int Contribution { get; }
        public StatUpdate(int raw, int contribution) { Raw = raw; Contribution = contribution; }
    }

    internal static class StatPlanner
    {
        public static bool TryPlan(StatCommand command, IReadOnlyList<StatSnapshot> values,
            out StatUpdate[] updates, out string error)
        {
            updates = Array.Empty<StatUpdate>();
            error = "No ready players found. Enter town or a run first.";
            if (values.Count == 0) return false;
            var pending = new StatUpdate[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                StatSnapshot value = values[i];
                error = "Stat selection mismatch. Nobody was changed.";
                if (value.Stat == null || (command.Stat != value.Stat &&
                    !(command.Stat == null && command.Operation == StatOperation.Reset))) return false;
                if (!TryPlanOne(command, value, out pending[i], out error)) return false;
            }
            updates = pending;
            error = "";
            return true;
        }

        private static bool TryPlanOne(StatCommand command, StatSnapshot value, out StatUpdate update, out string error)
        {
            update = default;
            error = "Stat or tracked adjustment would overflow. Nobody was changed.";
            if (command.Operation == StatOperation.Reset || (command.Operation == StatOperation.Multiply && command.Amount == 1))
            {
                long restored = (long)value.Raw - value.Contribution;
                if (!FitsInt(restored)) return false;
                update = new StatUpdate((int)restored, 0);
                return true;
            }

            if (command.Operation == StatOperation.Multiply && !RelativeMultiplier.IsValid(command.Amount))
            { error = RelativeMultiplier.Usage; return false; }

            // Multiply is always native-relative, even when this planner is used
            // directly rather than through the retained session policy.
            int source = value.Raw;
            if (command.Operation == StatOperation.Multiply)
            {
                long baseline = (long)value.Raw - value.Contribution;
                if (!FitsInt(baseline)) return false;
                source = (int)baseline;
            }
            // Match UnitAvatar: int sum/product, then float division and truncation.
            long factor = 100L + value.Amplifier;
            error = "A stat multiplier is non-positive or its arithmetic would overflow. Nobody was changed.";
            if (factor <= 0 || !FitsInt(factor) || !TryEffective(source, value.Bonus, factor, out int current)) return false;
            StatDefinition stat = value.Stat;
            decimal display = command.Operation == StatOperation.Set ? command.Amount :
                command.Operation == StatOperation.Multiply ? stat.Display(current) * command.Amount :
                stat.Display(current) + (command.Operation == StatOperation.Add ? command.Amount : -command.Amount);
            error = $"Every player's resulting {stat.Name} must be {stat.Minimum}..{stat.Maximum} {stat.Unit}. Nobody was changed.";
            if (display < stat.Minimum || display > stat.Maximum) return false;
            decimal scaled = (display - stat.Offset) * stat.Scale;
            if (scaled != decimal.Truncate(scaled) || scaled < int.MinValue || scaled > int.MaxValue) return false;
            int target = (int)scaled;
            int raw = source;
            if (current != target)
            {
                // Search all sums with representable base, sum, and native product.
                // The game formula is monotonic for the positive factors above.
                long lower = Math.Max(int.MinValue / factor, (long)int.MinValue + value.Bonus);
                long upper = Math.Min(int.MaxValue / factor, (long)int.MaxValue + value.Bonus);
                long end = upper;
                while (lower < upper)
                {
                    long middle = lower + (upper - lower) / 2;
                    if (Effective(middle, factor) < target) lower = middle + 1;
                    else upper = middle;
                }
                error = $"The exact {stat.Name} value is not representable with a player's current multiplier. Try another amount. Nobody was changed.";
                if (lower > end || Effective(lower, factor) != target) return false;
                raw = (int)(lower - value.Bonus);
            }
            long contribution = (long)value.Contribution + raw - value.Raw;
            error = "The tracked stat adjustment would overflow. Nobody was changed.";
            if (!FitsInt(contribution)) return false;
            update = new StatUpdate(raw, (int)contribution);
            return true;
        }

        private static bool TryEffective(int raw, int bonus, long factor, out int effective)
        {
            effective = 0;
            long sum = (long)raw + bonus;
            if (!FitsInt(sum) || !FitsInt(sum * factor)) return false;
            effective = Effective(sum, factor);
            return true;
        }

        private static int Effective(long sum, long factor) => (int)((float)(sum * factor) / 100f);
        private static bool FitsInt(long value) => value >= int.MinValue && value <= int.MaxValue;
    }
}
