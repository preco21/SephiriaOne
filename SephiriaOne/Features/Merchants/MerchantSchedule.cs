using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    // Run state, deliberately separate from the toggle/chance preset. Chance-based
    // encounters never fulfill this independently scheduled guarantee.
    internal sealed class MerchantSchedule
    {
        private readonly SaveData run;
        private readonly MerchantRoute route;
        private IReadOnlyList<int> Opportunities => route.Opportunities;
        private readonly int seed;
        private readonly bool hasGuarantee;
        internal MerchantRunState State { get; }
        private string Prefix => State.SchedulePrefix;

        internal MerchantSchedule(SaveData run, MerchantRoute route, int seed, MerchantDefinition definition = null)
        {
            definition = definition ?? MerchantCatalog.Find(MerchantCatalog.DefaultId);
            this.run = run; this.route = route; this.seed = seed ^ definition.SeedSalt;
            hasGuarantee = definition.HasGuarantee; State = new MerchantRunState(run, definition.Id);
        }

        internal int Progress => run.GetInt(Prefix + "Progress", -1);
        internal int Target => run.GetInt(Prefix + "Target", -1);

        internal void Advance(int position, int firstFloor = 1, bool guarantee = true)
        {
            if (position < 0) return;
            int progress = Math.Max(Progress, position);
            if (progress != Progress) run.SetInt(Prefix + "Progress", progress);
            // Old saves with an encounter retain their fulfilled guarantee. A pending
            // schedule stays fixed across option changes, reloads and guest re-entry.
            if (!guarantee || !hasGuarantee || State.Completed || Target >= 0) return;
            int remaining = 0;
            foreach (int candidate in Opportunities)
                if (candidate >= progress && route.FloorNumber(candidate) >= firstFloor) remaining++;
            if (remaining == 0) return;
            int selected = new Random(seed ^ 0x4D475541).Next(remaining);
            foreach (int candidate in Opportunities)
            {
                if (candidate < progress || route.FloorNumber(candidate) < firstFloor || selected-- != 0) continue;
                run.SetInt(Prefix + "Target", candidate);
                return;
            }
        }

        internal bool IsDue(int position, bool guarantee = true)
        {
            int target = Target;
            return guarantee && hasGuarantee && target >= 0 && position >= target && position >= Progress && !State.Completed;
        }

        internal bool HasPendingOpportunity(int firstFloor, bool guarantee = true)
        {
            if (!guarantee || !hasGuarantee || State.Completed) return false;
            int earliest = Math.Max(Progress, Target);
            foreach (int candidate in Opportunities)
                if (candidate >= earliest && route.FloorNumber(candidate) >= firstFloor) return true;
            return false;
        }

        internal void Complete() => State.Complete();
    }
}
