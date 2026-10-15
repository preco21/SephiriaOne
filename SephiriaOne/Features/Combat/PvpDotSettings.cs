using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum PvpDotEffect { Burn, PlasmaBurn, Frostbite, Poison }

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
        internal const int Count = 4;
        internal const decimal DefaultSeconds = 0.75m;
        private const int DefaultHundredths = 75;
        private readonly int burn, plasma, frostbite, poison;
        public bool HasChanges => burn != 0 || plasma != 0 || frostbite != 0 || poison != 0;
        public int Hundredths(PvpDotEffect effect) => DefaultHundredths + (effect switch
        {
            PvpDotEffect.Burn => burn, PvpDotEffect.PlasmaBurn => plasma,
            PvpDotEffect.Frostbite => frostbite, PvpDotEffect.Poison => poison,
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        });
        public decimal Seconds(PvpDotEffect effect) => Hundredths(effect) / 100m;
        public float Interval(PvpDotEffect effect) => Hundredths(effect) / 100f;
        public string Number(PvpDotEffect effect) => Seconds(effect).ToString("0.##", CultureInfo.InvariantCulture);
        public static bool ValidSeconds(decimal value) => value >= 0.05m && value <= 10m && value * 100 == decimal.Truncate(value * 100);
        public PvpDotSettings Apply(PvpDotChange change) => new PvpDotSettings(this, change);
        private PvpDotSettings(PvpDotSettings source, PvpDotChange change)
        {
            // Validate default(PvpDotChange), too.
            if (!ValidSeconds(change.Seconds)) throw new ArgumentOutOfRangeException(nameof(change));
            int offset = (int)(change.Seconds * 100) - DefaultHundredths;
            burn = change.Effect == PvpDotEffect.Burn ? offset : source.burn;
            plasma = change.Effect == PvpDotEffect.PlasmaBurn ? offset : source.plasma;
            frostbite = change.Effect == PvpDotEffect.Frostbite ? offset : source.frostbite;
            poison = change.Effect == PvpDotEffect.Poison ? offset : source.poison;
        }
        internal static string Key(PvpDotEffect effect) => effect switch
        {
            PvpDotEffect.Burn => "burn", PvpDotEffect.PlasmaBurn => "plasma-burn",
            PvpDotEffect.Frostbite => "frostbite", PvpDotEffect.Poison => "poison",
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        internal static string Label(PvpDotEffect effect) => effect switch
        {
            PvpDotEffect.Burn => "Burn (s)", PvpDotEffect.PlasmaBurn => "Plasma Burn (s)",
            PvpDotEffect.Frostbite => "Frostbite (s)", PvpDotEffect.Poison => "Poison (s)",
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        internal static bool TryEffect(string key, out PvpDotEffect effect)
        {
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
