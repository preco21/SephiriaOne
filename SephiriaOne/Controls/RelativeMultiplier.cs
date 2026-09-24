#nullable enable
using System.Globalization;

namespace SephiriaOne
{
    // Shared syntax and range; each feature still validates its resulting units.
    internal static class RelativeMultiplier
    {
        public const string Usage = "Use xN for native baseline times N (0..10000, at most 2 decimals); x1 restores native values. Use xN alone or set xN, not add/sub xN.";
        public static bool HasPrefix(string text) => text.Length > 0 && (text[0] == 'x' || text[0] == 'X');
        public static bool IsValid(decimal factor) => factor >= 0 && factor <= 10000 && factor * 100 == decimal.Truncate(factor * 100);
        public static bool TryParse(string text, out decimal factor)
        {
            factor = 0;
            if (!HasPrefix(text)) return false;
            string number = text.Substring(1);
            int point = number.IndexOf('.');
            return (point < 0 || number.Length - point - 1 <= 2) &&
                decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out factor) && IsValid(factor);
        }
    }
}
