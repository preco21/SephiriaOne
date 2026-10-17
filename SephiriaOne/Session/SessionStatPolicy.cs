#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal sealed partial class SessionPolicy
    {
        internal bool TryDescribeAppliedCompanionStat(StatSnapshot value, out string text)
        {
            text = "";
            if (value.Stat == null || !value.Stat.IsCompanion || value.Contribution == 0 ||
                !stats.TryGetValue(value.Stat, out Setting setting) || setting.Empty) return false;
            // A retained intention is not proof of an applied bonus. Relative
            // fallback, failed inheritance and unreconciled inputs stay silent.
            if (!setting.Absolute && (!TryPlanStatSetting(setting, value, out StatUpdate update, out _) ||
                update.UsesNativeFallback || update.Raw != value.Raw || update.Contribution != value.Contribution)) return false;
            if (!StatPlanner.TryDisplay(value, out decimal actual)) return false;
            string label = L.T(value.Stat.Label);
            string amount = setting.Value.ToString("0.##", CultureInfo.InvariantCulture);
            string unit = value.Stat.Key == "FOLLOWERDEFENSE" ? L.T("points") : "%";
            text = setting.Multiplier ? L.F("{0} ×{1}", label, amount) : setting.Absolute ?
                L.F("{0}: {1} {2}", label, actual.ToString("0.##", CultureInfo.InvariantCulture), unit) :
                L.F("{0}: {1}{2} {3}", label, setting.Value > 0 ? "+" : "", amount, unit);
            return true;
        }

        public bool IsRelativeStat(StatDefinition stat) =>
            stats.TryGetValue(stat, out Setting setting) && !setting.Absolute;

        public bool TryPlanRelativeStat(StatSnapshot value, out StatUpdate update, out string error)
        {
            update = default;
            error = L.T("No relative setting is active for this stat.");
            return stats.TryGetValue(value.Stat, out Setting setting) && !setting.Absolute &&
                TryPlanStatSetting(setting, value, out update, out error);
        }

        private Setting NextStatSetting(StatCommand command)
        {
            if (command.Stat == null) throw new ArgumentException("A stat is required.", nameof(command));
            if (command.Operation == StatOperation.Set) return new Setting(true, command.Amount);
            if (command.Operation == StatOperation.Multiply) return new Setting(false, command.Amount, true);
            stats.TryGetValue(command.Stat, out Setting current);
            // A relative command starts a new offset when leaving absolute mode.
            decimal offset = current.Absolute || current.Multiplier ? 0 : current.Value;
            return new Setting(false, offset + (command.Operation == StatOperation.Add ? command.Amount : -command.Amount));
        }

        public bool TryPlanStatCommand(StatCommand command, IReadOnlyList<StatSnapshot> values,
            out StatUpdate[] updates, out string error)
        {
            if (command.Operation == StatOperation.Reset) return StatPlanner.TryPlan(command, values, out updates, out error);
            updates = Array.Empty<StatUpdate>();
            error = L.T("No ready players found. Enter town or a run first.");
            if (values.Count == 0) return false;
            error = L.T("A supported stat is required. Nobody was changed.");
            if (command.Stat == null) return false;
            Setting setting = NextStatSetting(command);
            var pending = new StatUpdate[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                error = L.T("Stat selection mismatch. Nobody was changed.");
                if (values[i].Stat != command.Stat || !TryPlanStatSetting(setting, values[i], out pending[i], out error)) return false;
            }
            updates = pending;
            error = "";
            return true;
        }

        private static bool TryPlanStatSetting(Setting setting, StatSnapshot value, out StatUpdate update, out string error)
        {
            update = default;
            error = RelativeMultiplier.Usage;
            if (setting.Multiplier && !RelativeMultiplier.IsValid(setting.Value)) return false;
            error = L.T("A player's native stat baseline would overflow. Nobody was changed.");
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
            var command = new StatCommand(value.Stat, setting.Multiplier ? StatOperation.Multiply : setting.Absolute ? StatOperation.Set :
                setting.Value < 0 ? StatOperation.Subtract : StatOperation.Add,
                setting.Absolute || setting.Multiplier ? setting.Value : Math.Abs(setting.Value));
            var native = new StatSnapshot(value.Stat, (int)baseline, 0, value.Bonus, value.Amplifier);
            if (!StatPlanner.TryPlan(command, native, out StatUpdate planned, out error))
            {
                if (!setting.Multiplier) return false;
                // A valid factor can be incompatible with one character's native
                // penalties, range, or rounding. Restore the exact raw baseline;
                // never clamp it or reject the other participants/families. Keep
                // the factor so observed native input changes can try it again.
                update = new StatUpdate((int)baseline, 0, L.TrimSuffix(error, " Nobody was changed."));
                error = "";
                return true;
            }
            update = planned;
            return true;
        }
    }
}
