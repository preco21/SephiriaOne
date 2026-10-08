#nullable disable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum JarSpawnMode { Native, Chance, Multiplier }
    internal readonly struct JarSpawnSettings
    {
        public JarSpawnMode Mode { get; }
        public decimal Value { get; }
        public bool HasChanges => Mode != JarSpawnMode.Native;
        public JarSpawnSettings(JarSpawnMode mode, decimal value)
        {
            if (mode == JarSpawnMode.Chance ? value < 0 || value > 100 || value * 100 != decimal.Truncate(value * 100) :
                mode == JarSpawnMode.Multiplier ? !RelativeMultiplier.IsValid(value) : mode != JarSpawnMode.Native || value != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            Mode = mode == JarSpawnMode.Multiplier && value == 1 ? JarSpawnMode.Native : mode;
            Value = Mode == JarSpawnMode.Native ? 0 : value;
        }
        public float Probability(float native) => !HasChanges ? native : Mode == JarSpawnMode.Chance ? (float)(Value / 100m) :
            (float)Math.Max(0d, Math.Min(1d, native * (double)Value));
        public string Number => Value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    internal enum JarSpawnParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal static class JarSpawnCommand
    {
        public static string Usage => L.T("Mystic Jar: /one jars chance 0..100|xN; /one jars status|reset. Percentages allow 2 decimals; xN uses each native chance (capped at 100%). x1/reset restores native. /one save persists settings.");
        public static JarSpawnParseResult Parse(string text, out JarSpawnSettings settings)
        {
            settings = default;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("jars", StringComparison.OrdinalIgnoreCase)) return JarSpawnParseResult.NotCommand;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length <= 3)
            {
                if (op == "help") return JarSpawnParseResult.Help;
                if (op == "status") return JarSpawnParseResult.Status;
                if (op == "reset" || op == "off") return JarSpawnParseResult.Valid;
            }
            if (parts.Length == 4 && op == "chance")
            {
                if (RelativeMultiplier.TryParse(parts[3], out decimal factor))
                { settings = new JarSpawnSettings(JarSpawnMode.Multiplier, factor); return JarSpawnParseResult.Valid; }
                string number = parts[3]; int point = number.IndexOf('.');
                if ((point < 0 || number.Length - point - 1 <= 2) && decimal.TryParse(number, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal percent) && percent >= 0 && percent <= 100 && percent * 100 == decimal.Truncate(percent * 100))
                { settings = new JarSpawnSettings(JarSpawnMode.Chance, percent); return JarSpawnParseResult.Valid; }
            }
            return JarSpawnParseResult.Invalid;
        }
    }
}
