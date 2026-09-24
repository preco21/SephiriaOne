#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal enum ResourceMode { Set, Offset, Multiplier }
    internal readonly struct ResourceSetting
    {
        public ResourceMode Mode { get; }
        public decimal Amount { get; }
        public bool Empty => Mode == ResourceMode.Offset ? Amount == 0 : Mode == ResourceMode.Multiplier && Amount == 1;
        public ResourceSetting(ResourceMode mode, decimal amount) { Mode = mode; Amount = amount; }
        public bool TryTarget(int baseline, int minimum, int maximum, out int target, out string error)
        {
            target = baseline; error = "Resource baseline or setting is invalid.";
            if (baseline < 0 || (Mode == ResourceMode.Multiplier && !RelativeMultiplier.IsValid(Amount))) return false;
            decimal value;
            try { value = Mode == ResourceMode.Set ? Amount : Mode == ResourceMode.Offset ? baseline + Amount : baseline * Amount; }
            catch (OverflowException) { return false; }
            error = $"Every resulting resource value must be an exact whole number {minimum}..{maximum}. Nobody was changed.";
            if (value < minimum || value > maximum || value != decimal.Truncate(value)) return false;
            target = (int)value; error = ""; return true;
        }
        public string Describe() => Mode.ToString().ToLowerInvariant() + " " + Amount.ToString("0.##", CultureInfo.InvariantCulture);
    }
    internal sealed class ResourcePolicy
    {
        private readonly Dictionary<ResourceKind, ResourceSetting> settings = new Dictionary<ResourceKind, ResourceSetting>();
        public bool HasChanges => settings.Count != 0;
        public bool TryGet(ResourceKind kind, out ResourceSetting setting) => settings.TryGetValue(kind, out setting);
        public ResourceSetting Next(ResourceCommand command)
        {
            if (command.IsReset) return new ResourceSetting(ResourceMode.Offset, 0);
            if (command.Operation == ResourceOperation.Set) return new ResourceSetting(ResourceMode.Set, command.Amount);
            if (command.Operation == ResourceOperation.Multiply) return new ResourceSetting(ResourceMode.Multiplier, command.Amount);
            settings.TryGetValue(command.Definition!.Kind, out ResourceSetting current);
            decimal offset = current.Mode == ResourceMode.Offset ? current.Amount : 0;
            return new ResourceSetting(ResourceMode.Offset, offset + (command.Operation == ResourceOperation.Add ? command.Amount : -command.Amount));
        }
        public void Record(ResourceCommand command)
        {
            if (command.Definition == null) { if (command.IsReset) Clear(); else throw new ArgumentException("Resource required."); return; }
            Set(command.Definition.Kind, Next(command));
        }
        public void Set(ResourceKind kind, ResourceSetting setting)
        { if (setting.Empty) settings.Remove(kind); else settings[kind] = setting; }
        public void Clear() => settings.Clear();
        public IEnumerable<string> Describe()
        {
            foreach (var definition in ResourceCatalog.All)
                if (settings.TryGetValue(definition.Kind, out ResourceSetting setting))
                    yield return "resources " + definition.Name + " " + setting.Describe();
        }
    }
}
