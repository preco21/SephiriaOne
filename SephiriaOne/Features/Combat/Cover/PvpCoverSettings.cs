using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct PvpCoverSettings
    {
        public const int DefaultCount = 20, MaximumCount = 32;
        private readonly int countOffset;
        public bool Enabled { get; }
        public int Count => DefaultCount + countOffset;
        public bool HasChanges => Enabled || countOffset != 0;
        public PvpCoverSettings(bool enabled, int count = DefaultCount)
        {
            if (!ValidCount(count)) throw new ArgumentOutOfRangeException(nameof(count));
            Enabled = enabled; countOffset = count - DefaultCount;
        }
        internal static bool ValidCount(int count) => count >= 1 && count <= MaximumCount;
    }
    internal readonly struct PvpCoverChange
    {
        public bool? Enabled { get; }
        public int? Count { get; }
        public bool Reset { get; }
        public bool IsValid => !Count.HasValue || PvpCoverSettings.ValidCount(Count.Value);
        public bool IsReset => Reset || Enabled == false && !Count.HasValue;
        public PvpCoverChange(bool? enabled = null, int? count = null, bool reset = false)
        { Enabled = enabled; Count = count; Reset = reset; }
        public PvpCoverSettings Apply(PvpCoverSettings current) => Reset ? default :
            new PvpCoverSettings(Enabled ?? current.Enabled, Count ?? current.Count);
        internal static bool TryCount(string text, out int count) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count) && PvpCoverSettings.ValidCount(count);
    }
}
