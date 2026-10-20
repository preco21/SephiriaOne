#nullable disable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct RequitalSettings : IEquatable<RequitalSettings>
    {
        public const int DefaultCount = 1;
        public const int MaximumCount = 20;
        private readonly int countOffset;
        public bool Automatic { get; }
        public int Count => DefaultCount + countOffset;
        public bool HasChanges => Automatic || countOffset != 0;
        public RequitalSettings(bool automatic, int count = DefaultCount)
        {
            if (!ValidCount(count)) throw new ArgumentOutOfRangeException(nameof(count));
            Automatic = automatic;
            countOffset = count - DefaultCount;
        }
        public static bool ValidCount(int count) => count >= 0 && count <= MaximumCount;
        public bool Equals(RequitalSettings other) => Automatic == other.Automatic && countOffset == other.countOffset;
        public override bool Equals(object obj) => obj is RequitalSettings other && Equals(other);
        public override int GetHashCode() => countOffset * 2 + (Automatic ? 1 : 0);
        public RequitalSettings Apply(RequitalCommand command) => command.Reset ? default :
            new RequitalSettings(command.Automatic ?? Automatic, command.Count ?? Count);
    }

    internal enum RequitalParseResult { NotCommand, Help, Status, Give, Invalid, Valid }
    internal readonly struct RequitalCommand
    {
        public bool? Automatic { get; }
        public int? Count { get; }
        public bool Reset { get; }
        public RequitalCommand(bool? automatic = null, int? count = null, bool reset = false)
        { Automatic = automatic; Count = count; Reset = reset; }
        public static string Usage => L.T("Requital Slates: /one slates auto on|off|reset; count 0..20|reset; give; status; reset. Auto adds a retractable starting loadout, not a grant per floor. Give adds ordinary one-time copies. Default: auto off, count 1. /one save persists settings.");
        public static RequitalParseResult Parse(string text, out RequitalCommand command)
        {
            command = default;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool Is(int index, string value) => parts.Length > index && parts[index].Equals(value, StringComparison.OrdinalIgnoreCase);
            if (!Is(0, "/one") || !Is(1, "slates")) return RequitalParseResult.NotCommand;
            if (parts.Length == 2 || parts.Length == 3 && Is(2, "help")) return RequitalParseResult.Help;
            if (parts.Length == 3)
            {
                if (Is(2, "status")) return RequitalParseResult.Status;
                if (Is(2, "give")) return RequitalParseResult.Give;
                if (Is(2, "reset")) { command = new RequitalCommand(reset: true); return RequitalParseResult.Valid; }
            }
            if (parts.Length == 4 && Is(2, "auto") && (Is(3, "on") || Is(3, "off") || Is(3, "reset")))
            { command = new RequitalCommand(automatic: Is(3, "on")); return RequitalParseResult.Valid; }
            if (parts.Length == 4 && Is(2, "count"))
            {
                int count = RequitalSettings.DefaultCount;
                if (Is(3, "reset") || int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out count) &&
                    RequitalSettings.ValidCount(count))
                { command = new RequitalCommand(count: count); return RequitalParseResult.Valid; }
            }
            return RequitalParseResult.Invalid;
        }
    }
}
