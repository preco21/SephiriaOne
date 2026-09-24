#nullable enable
using System;

namespace SephiriaOne
{
    internal readonly struct ResourceSnapshot
    {
        public ResourceDefinition Definition { get; }
        public int Raw { get; }
        public int Owned { get; }
        public int Bonus { get; }
        public int Amplifier { get; }
        public int DisplayOffset { get; }
        public int MinimumSafe { get; }
        public bool Busy { get; }
        public ResourceSnapshot(ResourceDefinition definition, int raw, int owned, int bonus = 0, int amplifier = 0,
            int displayOffset = 0, int minimumSafe = 0, bool busy = false)
        { Definition = definition; Raw = raw; Owned = owned; Bonus = bonus; Amplifier = amplifier;
            DisplayOffset = displayOffset; MinimumSafe = minimumSafe; Busy = busy; }
    }
    internal readonly struct ResourceUpdate
    {
        public ResourceDefinition Definition { get; }
        public int Raw { get; }
        public int Owned { get; }
        public int Target { get; }
        public ResourceUpdate(ResourceDefinition definition, int raw, int owned, int target)
        { Definition = definition; Raw = raw; Owned = owned; Target = target; }
    }
    internal static class ResourcePlanner
    {
        public static bool TryValue(ResourceSnapshot value, bool native, out int total)
        {
            total = 0;
            long raw = (long)value.Raw - (native ? value.Owned : 0);
            if (value.Definition.Kind != ResourceKind.Fruit)
            {
                if (raw < int.MinValue || raw > int.MaxValue) return false;
                total = (int)raw; return true;
            }
            long factor = 100L + value.Amplifier;
            long sum = raw + value.Bonus;
            if (raw < int.MinValue || raw > int.MaxValue || sum < int.MinValue || sum > int.MaxValue || factor < int.MinValue || factor > int.MaxValue ||
                sum * factor < int.MinValue || sum * factor > int.MaxValue) return false;
            long display = (long)(int)((float)(sum * factor) / 100f) + value.DisplayOffset;
            if (display < int.MinValue || display > int.MaxValue) return false;
            total = (int)display; return true;
        }
        public static bool TryPlan(ResourceSetting setting, ResourceSnapshot value, bool reset, out ResourceUpdate update, out string error)
        {
            update = default; error = "Resource arithmetic or native baseline is invalid. Nobody was changed.";
            if (!TryValue(value, true, out int native) || !TryValue(value, false, out int current)) return false;
            var definition = value.Definition;
            int target = native;
            reset |= setting.Empty;
            if (!reset && !setting.TryTarget(native, definition.Minimum, definition.Maximum, out target, out error)) return false;
            error = "A resource decrease conflicts with occupied slots, allocated points, or a pending native menu. Nobody was changed.";
            if (target < value.MinimumSafe || (value.Busy && target < current)) return false;
            if (definition.Kind == ResourceKind.Fruit)
            {
                var stat = new StatDefinition("fruit", "FRUITCOUNT", "slots", offset: value.DisplayOffset,
                    minimum: definition.Minimum, maximum: definition.Maximum);
                var command = new StatCommand(stat, reset ? StatOperation.Reset : StatOperation.Set, target);
                if (!StatPlanner.TryPlan(command, new[] { new StatSnapshot(stat, value.Raw, value.Owned, value.Bonus, value.Amplifier) },
                    out StatUpdate[] result, out error)) return false;
                update = new ResourceUpdate(definition, result[0].Raw, result[0].Contribution, target);
            }
            else
            {
                long owned = (long)target - native;
                if (owned < int.MinValue || owned > int.MaxValue) return false;
                update = new ResourceUpdate(definition, target, (int)owned, target);
            }
            error = ""; return true;
        }
    }
}
