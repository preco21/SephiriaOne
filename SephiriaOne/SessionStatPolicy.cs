#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed partial class SessionPolicy
    {
        public bool IsRelativeStat(StatDefinition stat) =>
            stats.TryGetValue(stat, out Setting setting) && !setting.Absolute;

        public bool TryPlanRelativeStat(StatSnapshot value, out StatUpdate update, out string error)
        {
            update = default;
            error = "No relative setting is active for this stat.";
            return stats.TryGetValue(value.Stat, out Setting setting) && !setting.Absolute &&
                TryPlanStatSetting(setting, value, out update, out error);
        }

        private Setting NextStatSetting(StatCommand command)
        {
            if (command.Stat == null) throw new ArgumentException("A stat is required.", nameof(command));
            if (command.Operation == StatOperation.Set) return new Setting(true, command.Amount);
            stats.TryGetValue(command.Stat, out Setting current);
            // A relative command starts a new offset when leaving absolute mode.
            decimal offset = current.Absolute ? 0 : current.Value;
            return new Setting(false, offset + (command.Operation == StatOperation.Add ? command.Amount : -command.Amount));
        }

        public bool TryPlanStatCommand(StatCommand command, IReadOnlyList<StatSnapshot> values,
            out StatUpdate[] updates, out string error)
        {
            if (command.Operation == StatOperation.Reset) return StatPlanner.TryPlan(command, values, out updates, out error);
            updates = Array.Empty<StatUpdate>();
            error = "No ready players found. Enter town or a run first.";
            if (values.Count == 0) return false;
            error = "A supported stat is required. Nobody was changed.";
            if (command.Stat == null) return false;
            Setting setting = NextStatSetting(command);
            var pending = new StatUpdate[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                error = "Stat selection mismatch. Nobody was changed.";
                if (values[i].Stat != command.Stat || !TryPlanStatSetting(setting, values[i], out pending[i], out error)) return false;
            }
            updates = pending;
            error = "";
            return true;
        }

        private static bool TryPlanStatSetting(Setting setting, StatSnapshot value, out StatUpdate update, out string error)
        {
            update = default;
            error = "A player's native stat baseline would overflow. Nobody was changed.";
            long baseline = (long)value.Raw - value.Contribution;
            if (baseline < int.MinValue || baseline > int.MaxValue) return false;
            if (setting.Empty)
            {
                // Net zero restores the exact raw baseline, including values that
                // have multiple rounded representations or exceed command bounds.
                update = new StatUpdate((int)baseline, 0);
                error = "";
                return true;
            }
            var command = new StatCommand(value.Stat, setting.Absolute ? StatOperation.Set :
                setting.Value < 0 ? StatOperation.Subtract : StatOperation.Add,
                setting.Absolute ? setting.Value : Math.Abs(setting.Value));
            var native = new StatSnapshot(value.Stat, (int)baseline, 0, value.Bonus, value.Amplifier);
            if (!StatPlanner.TryPlan(command, new[] { native }, out StatUpdate[] planned, out error)) return false;
            update = planned[0];
            return true;
        }
    }
}
