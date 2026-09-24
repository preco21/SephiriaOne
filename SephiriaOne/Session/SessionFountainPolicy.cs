#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed partial class SessionPolicy
    {
        private Setting NextFountainSetting(FountainCommand command)
        {
            if (command.Operation == FountainOperation.Set) return new Setting(true, command.Amount);
            if (command.Operation == FountainOperation.Multiply) return new Setting(false, command.Amount, true);
            Setting current = fountain.GetValueOrDefault();
            // Preserve legacy Set-then-Add behavior, but leave multiplier mode
            // with a fresh native-relative offset, as for character stats.
            if (current.Multiplier) current = new Setting(false, 0);
            return current.Add(command.Operation == FountainOperation.Add ? command.Amount : -command.Amount);
        }

        public bool TryPlanFountainCommand(FountainCommand command, IReadOnlyList<int> balances,
            IReadOnlyList<int> contributions, int limit, int? original, int? applied,
            out FountainPlan plan, out string error)
        {
            bool multiply = command.Operation == FountainOperation.Multiply;
            bool leaveMultiplier = HasFountainMultiplier &&
                (command.Operation == FountainOperation.Add || command.Operation == FountainOperation.Subtract);
            if ((!multiply && !leaveMultiplier) || (multiply && command.Amount == 1))
                return command.TryPlanTracked(balances, contributions, limit, original, applied, out plan, out error);
            return TryPlanFountainSetting(NextFountainSetting(command), balances, contributions,
                limit, original, applied, out plan, out error);
        }

        public bool TryPlanFountainMultiplier(SessionPlayerSnapshot player, out FountainPlan plan, out string error)
        {
            plan = new FountainPlan(Array.Empty<int>(), Array.Empty<int>(), 0, null, null);
            error = "Fountain multiplier or session limit is unavailable.";
            return HasFountainMultiplier && player.FountainLimit.HasValue &&
                TryPlanFountainSetting(fountain!.Value, new[] { player.FountainPoints }, new[] { player.FountainContribution },
                    player.FountainLimit.Value, player.OriginalLimit, player.AppliedLimit, out plan, out error);
        }

        private static bool TryPlanFountainSetting(Setting setting, IReadOnlyList<int> balances,
            IReadOnlyList<int> contributions, int limit, int? original, int? applied,
            out FountainPlan plan, out string error)
        {
            plan = new FountainPlan(Array.Empty<int>(), Array.Empty<int>(), limit, original, applied);
            error = "A player's native Fountain baseline is invalid. Nobody was changed.";
            if (balances.Count != contributions.Count) return false;
            var native = new int[balances.Count];
            for (int i = 0; i < balances.Count; i++)
            {
                long baseline = (long)balances[i] - contributions[i];
                if (baseline < 0 || baseline > int.MaxValue) return false;
                native[i] = (int)baseline;
            }
            var command = new FountainCommand(setting.Multiplier ? FountainOperation.Multiply : setting.Absolute ? FountainOperation.Set :
                setting.Value < 0 ? FountainOperation.Subtract : FountainOperation.Add,
                setting.Multiplier || setting.Absolute ? setting.Value : Math.Abs(setting.Value));
            return command.TryPlanTracked(native, new int[native.Length], limit, original, applied, out plan, out error);
        }
    }
}
