using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    // Run state, deliberately separate from the toggle/chance preset. Chance-based
    // encounters never fulfill this independently scheduled guarantee.
    internal sealed class MerchantSchedule
    {
        private const string Prefix = "SephiriaOne.MerchantSchedule.v1.";
        private const string EncounterKey = "SephiriaOne.MerchantEncounter";
        private readonly SaveData run;
        private readonly IReadOnlyList<int> opportunities;
        private readonly int seed;

        internal MerchantSchedule(SaveData run, IReadOnlyList<int> opportunities, int seed)
        { this.run = run; this.opportunities = opportunities; this.seed = seed; }

        internal int Progress => run.GetInt(Prefix + "Progress", -1);

        internal void Advance(int position)
        {
            if (position < 0) return;
            int progress = Math.Max(Progress, position);
            if (progress != Progress) run.SetInt(Prefix + "Progress", progress);
            // Old saves with an encounter retain their fulfilled guarantee. A pending
            // schedule stays fixed across option changes, reloads and guest re-entry.
            if (run.GetBool(EncounterKey, false) || run.GetInt(Prefix + "Target", -1) >= 0) return;
            int remaining = 0;
            foreach (int candidate in opportunities) if (candidate >= progress) remaining++;
            if (remaining == 0) return;
            int selected = new Random(seed ^ 0x4D475541).Next(remaining);
            foreach (int candidate in opportunities)
            {
                if (candidate < progress || selected-- != 0) continue;
                run.SetInt(Prefix + "Target", candidate);
                return;
            }
        }

        internal bool IsDue(int position)
        {
            int target = run.GetInt(Prefix + "Target", -1);
            return target >= 0 && position >= target && position >= Progress &&
                !run.GetBool(EncounterKey, false);
        }

        internal void Complete() => run.SetBool(EncounterKey, true);
    }
}
