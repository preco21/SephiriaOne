#nullable disable
using System;

namespace SephiriaOne
{
    // Matches the native converted-element index.
    internal enum PvpElement { Physical = 0, Flame = 1, Ice = 2, Lightning = 3 }

    internal readonly struct PvpElementalSettings
    {
        // Invert the toggle so old presets and default(T) enable the limits.
        private readonly bool disabled;
        private readonly PvpStatLimit physical, flame, ice, lightning;
        public bool Enabled => !disabled;
        public bool HasChanges => disabled || physical.HasChanges || flame.HasChanges || ice.HasChanges || lightning.HasChanges;

        public PvpElementalSettings(bool enabled, PvpStatLimit physical = default, PvpStatLimit flame = default,
            PvpStatLimit ice = default, PvpStatLimit lightning = default)
        {
            disabled = !enabled;
            this.physical = physical; this.flame = flame; this.ice = ice; this.lightning = lightning;
        }

        public PvpStatLimit GetLimit(PvpElement element)
        {
            switch (element)
            {
                case PvpElement.Physical: return physical;
                case PvpElement.Flame: return flame;
                case PvpElement.Ice: return ice;
                case PvpElement.Lightning: return lightning;
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }
        }

        public static bool TryElement(string text, out PvpElement element)
        {
            switch (text.ToLowerInvariant())
            {
                case "physical": element = PvpElement.Physical; return true;
                case "flame": element = PvpElement.Flame; return true;
                case "ice": element = PvpElement.Ice; return true;
                case "lightning": element = PvpElement.Lightning; return true;
                default: element = default; return false;
            }
        }
    }

    internal readonly struct PvpElementalChange
    {
        public bool? Enabled { get; }
        public PvpElement? Element { get; }
        public decimal? Soft { get; }
        public decimal? Hard { get; }
        public bool Reset { get; }
        public bool IsValid => (!Element.HasValue || (uint)Element.Value <= 3) && Soft.HasValue == Hard.HasValue &&
            (!Soft.HasValue || Element.HasValue && PvpStatLimit.ValidLimits(Soft.Value, Hard.Value));
        public bool IsReset => Reset || Enabled == false;

        public PvpElementalChange(bool? enabled = null, PvpElement? element = null, decimal? soft = null,
            decimal? hard = null, bool reset = false)
        { Enabled = enabled; Element = element; Soft = soft; Hard = hard; Reset = reset; }

        public PvpElementalSettings Apply(PvpElementalSettings source)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(Soft));
            if (Reset && !Element.HasValue) return default;
            var replacement = Reset ? default : Soft.HasValue ? new PvpStatLimit(Soft.Value, Hard.Value) : default;
            return new PvpElementalSettings(Enabled ?? source.Enabled,
                Element == PvpElement.Physical ? replacement : source.GetLimit(PvpElement.Physical),
                Element == PvpElement.Flame ? replacement : source.GetLimit(PvpElement.Flame),
                Element == PvpElement.Ice ? replacement : source.GetLimit(PvpElement.Ice),
                Element == PvpElement.Lightning ? replacement : source.GetLimit(PvpElement.Lightning));
        }
    }
}
