using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class MerchantRunState
    {
        private readonly SaveData run;
        private readonly string floorPrefix, encounterKey, countKey;
        private readonly bool legacy;
        internal string SchedulePrefix { get; }
        internal bool Completed => run.GetBool(encounterKey, false);
        internal int Count => Math.Max(0, run.GetInt(countKey, 0));

        internal MerchantRunState(SaveData run, string id)
        {
            this.run = run;
            legacy = id == MerchantCatalog.DefaultId;
            string prefix = "SephiriaOne.MerchantVariant.v1." + id + ".";
            floorPrefix = legacy ? "SephiriaOne.MerchantFloor." : prefix + "Floor.";
            encounterKey = legacy ? "SephiriaOne.MerchantEncounter" : prefix + "Encounter";
            SchedulePrefix = legacy ? "SephiriaOne.MerchantSchedule.v1." : prefix + "Schedule.";
            countKey = prefix + "Count";
        }

        internal void InitializeCount(IEnumerable<string> generatedFloors)
        {
            if (run.GetInt(countKey, -1) >= 0) return;
            // Old saves know only that rolls were consumed, not which ones spawned.
            // Count those conservatively once, so a new cap cannot overfill an old run.
            int count = 0;
            if (legacy)
            {
                foreach (string guid in generatedFloors) if (Consumed(guid) && count < int.MaxValue) count++;
                if (Completed) count = Math.Max(1, count);
            }
            run.SetInt(countKey, count);
        }

        internal bool Consumed(string guid) => run.GetBool(floorPrefix + guid, false);
        internal void Reserve(string guid) => run.SetBool(floorPrefix + guid, true);
        internal void Complete() => run.SetBool(encounterKey, true);
        internal void RecordSpawn(bool guaranteed)
        {
            run.SetInt(countKey, Count < int.MaxValue ? Count + 1 : int.MaxValue);
            if (guaranteed) Complete();
        }
    }
}
