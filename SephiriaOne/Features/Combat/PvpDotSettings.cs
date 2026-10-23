using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum PvpDotEffect { Burn, PlasmaBurn, Frostbite, Poison, Wound, Lightning, Freeze, Chim, DarkCloud, PlasmaShock }

    internal readonly struct PvpDotScheduleChange
    {
        public PvpDotEffect Effect { get; }
        public bool Enabled { get; }
        public PvpDotScheduleChange(PvpDotEffect effect, bool enabled)
        {
            if (!PvpDotSettings.SupportsSchedule(effect)) throw new ArgumentOutOfRangeException(nameof(effect));
            Effect = effect; Enabled = enabled;
        }
    }

    internal readonly struct PvpDotChange
    {
        public PvpDotEffect Effect { get; }
        public decimal Seconds { get; }
        public PvpDotChange(PvpDotEffect effect, decimal seconds)
        {
            if ((uint)effect >= PvpDotSettings.Count) throw new ArgumentOutOfRangeException(nameof(effect));
            if (!PvpDotSettings.ValidSeconds(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Effect = effect; Seconds = seconds;
        }
    }

    // Default(T) inherits all defaults; integer offsets keep policy equality and
    // preset comparisons exact without dictionaries or per-tick allocations.
    internal readonly struct PvpDotSettings
    {
        internal const int Count = 10;
        internal static decimal DefaultSeconds(PvpDotEffect effect) => DefaultHundredths(effect) / 100m;
        private static int DefaultHundredths(PvpDotEffect effect) => effect switch
        {
            PvpDotEffect.Burn => 90,
            PvpDotEffect.PlasmaBurn => 75, PvpDotEffect.Frostbite => 75, PvpDotEffect.Poison => 75,
            PvpDotEffect.Wound => 133, PvpDotEffect.Lightning => 50, PvpDotEffect.Freeze => 25,
            PvpDotEffect.Chim => 25, PvpDotEffect.DarkCloud => 200, PvpDotEffect.PlasmaShock => 90,
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        private readonly int burn, plasma, frostbite, poison, wound, lightning, freeze, chim, darkCloud, plasmaShock;
        // Zero-initialized settings enable protection, including old presets.
        private readonly int nativeSchedules;
        public bool HasScheduleChanges => nativeSchedules != 0;
        internal static bool SupportsSchedule(PvpDotEffect effect) => (uint)effect < Count && effect != PvpDotEffect.DarkCloud;
        public bool PreserveSchedule(PvpDotEffect effect) => SupportsSchedule(effect) && (nativeSchedules & (1 << (int)effect)) == 0;
        public bool HasSeparatedShockChanges => lightning != 0 || plasmaShock != 0 ||
            !PreserveSchedule(PvpDotEffect.Lightning) || !PreserveSchedule(PvpDotEffect.PlasmaShock);
        public bool HasExtendedChanges => HasSeparatedShockChanges || wound != 0 || lightning != 0 || freeze != 0 || chim != 0 || darkCloud != 0;
        public bool HasChanges => burn != 0 || plasma != 0 || frostbite != 0 || poison != 0 || HasExtendedChanges || HasScheduleChanges;
        public int Hundredths(PvpDotEffect effect) => DefaultHundredths(effect) + (effect switch
        {
            PvpDotEffect.Burn => burn, PvpDotEffect.PlasmaBurn => plasma,
            PvpDotEffect.Frostbite => frostbite, PvpDotEffect.Poison => poison,
            PvpDotEffect.Wound => wound, PvpDotEffect.Lightning => lightning, PvpDotEffect.Freeze => freeze,
            PvpDotEffect.Chim => chim, PvpDotEffect.DarkCloud => darkCloud, PvpDotEffect.PlasmaShock => plasmaShock,
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        });
        public decimal Seconds(PvpDotEffect effect) => Hundredths(effect) / 100m;
        public float Interval(PvpDotEffect effect) => Hundredths(effect) / 100f;
        public string Number(PvpDotEffect effect) => Seconds(effect).ToString("0.##", CultureInfo.InvariantCulture);
        public static bool ValidSeconds(decimal value) => value >= 0.05m && value <= 10m && value * 100 == decimal.Truncate(value * 100);
        public PvpDotSettings Apply(PvpDotChange change) => new PvpDotSettings(this, change);
        public PvpDotSettings ApplySchedule(PvpDotScheduleChange change) => new PvpDotSettings(this, change);
        private PvpDotSettings(PvpDotSettings source, PvpDotScheduleChange change)
        {
            this = source;
            int bit = 1 << (int)change.Effect;
            nativeSchedules = change.Enabled ? source.nativeSchedules & ~bit : source.nativeSchedules | bit;
        }
        private PvpDotSettings(PvpDotSettings source, PvpDotChange change)
        {
            // Validate default(PvpDotChange), too.
            if (!ValidSeconds(change.Seconds)) throw new ArgumentOutOfRangeException(nameof(change));
            int offset = (int)(change.Seconds * 100) - DefaultHundredths(change.Effect);
            burn = change.Effect == PvpDotEffect.Burn ? offset : source.burn;
            plasma = change.Effect == PvpDotEffect.PlasmaBurn ? offset : source.plasma;
            frostbite = change.Effect == PvpDotEffect.Frostbite ? offset : source.frostbite;
            poison = change.Effect == PvpDotEffect.Poison ? offset : source.poison;
            wound = change.Effect == PvpDotEffect.Wound ? offset : source.wound;
            lightning = change.Effect == PvpDotEffect.Lightning ? offset : source.lightning;
            freeze = change.Effect == PvpDotEffect.Freeze ? offset : source.freeze;
            chim = change.Effect == PvpDotEffect.Chim ? offset : source.chim;
            darkCloud = change.Effect == PvpDotEffect.DarkCloud ? offset : source.darkCloud;
            plasmaShock = change.Effect == PvpDotEffect.PlasmaShock ? offset : source.plasmaShock;
            nativeSchedules = source.nativeSchedules;
        }
        internal static string Key(PvpDotEffect effect) => effect switch
        {
            PvpDotEffect.Burn => "burn", PvpDotEffect.PlasmaBurn => "plasma-burn",
            PvpDotEffect.Frostbite => "frostbite", PvpDotEffect.Poison => "poison",
            PvpDotEffect.Wound => "wound", PvpDotEffect.Lightning => "lightning", PvpDotEffect.Freeze => "freeze",
            PvpDotEffect.Chim => "chim", PvpDotEffect.DarkCloud => "dark-cloud", PvpDotEffect.PlasmaShock => "plasma-shock",
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        internal static string Label(PvpDotEffect effect) => effect switch
        {
            PvpDotEffect.Burn => "Burn (s)", PvpDotEffect.PlasmaBurn => "Plasma Burn (s)",
            PvpDotEffect.Frostbite => "Frostbite (s)", PvpDotEffect.Poison => "Poison (s)",
            PvpDotEffect.Wound => "Wound (s)", PvpDotEffect.Lightning => "Lightning / Shock (s)",
            PvpDotEffect.Freeze => "Freeze damage (s)", PvpDotEffect.Chim => "Chim damage (s)",
            PvpDotEffect.DarkCloud => "Dark Cloud (s)", PvpDotEffect.PlasmaShock => "Plasma shock (s)",
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        internal static bool TryEffect(string key, out PvpDotEffect effect)
        {
            if (string.Equals(key, "shock", StringComparison.OrdinalIgnoreCase))
            { effect = PvpDotEffect.Lightning; return true; }
            for (int i = 0; i < Count; i++)
                if (string.Equals(key, Key((PvpDotEffect)i), StringComparison.OrdinalIgnoreCase))
                { effect = (PvpDotEffect)i; return true; }
            effect = default; return false;
        }
        internal static bool TrySeconds(string text, out decimal seconds)
        {
            seconds = 0;
            int point = text.IndexOf('.');
            return (point < 0 || text.Length - point - 1 <= 2) &&
                decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out seconds) && ValidSeconds(seconds);
        }
    }
}
