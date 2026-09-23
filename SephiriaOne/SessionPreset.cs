#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal sealed partial class SessionPolicy
    {
        internal const int MaximumPresetLength = 4096;
        private const string PresetHeader = "SephiriaOne preset v1";
        private static readonly string[] ChoiceNames = { "item", "weapon", "miracle" };

        public IReadOnlyList<string> DescribeSettings()
        {
            var lines = new List<string>();
            string Describe(Setting setting) => (setting.Absolute ? "set " : "offset ") +
                setting.Value.ToString("0.##", CultureInfo.InvariantCulture);
            if (fountain.HasValue) lines.Add("fountain " + Describe(fountain.Value));
            for (int i = 0; i < ChoiceCommand.Keys.Length; i++)
                if (choices.TryGetValue(ChoiceCommand.Keys[i], out int extra))
                    lines.Add("choices " + ChoiceNames[i] + " " + extra.ToString(CultureInfo.InvariantCulture));
            foreach (StatDefinition stat in StatCatalog.All)
                if (stats.TryGetValue(stat, out Setting setting)) lines.Add("stats " + stat.Name + " " + Describe(setting));
            return lines;
        }

        public string ToPresetText() => PresetHeader + "\n" +
            (HasChanges ? string.Join("\n", DescribeSettings()) + "\n" : "");

        public static bool TryReadPreset(string text, out SessionPolicy policy, out string error)
        {
            policy = new SessionPolicy();
            error = "Invalid saved preset; no saved settings were applied.";
            if (text == null || text.Length > MaximumPresetLength) return false;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0] != PresetHeader) return false;
            var pending = new SessionPolicy();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                if (!decimal.TryParse(parts[parts.Length - 1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal value) || value < -int.MaxValue || value > int.MaxValue) return false;
                if (parts[0] == "choices")
                {
                    if (parts.Length != 3 || value < 0 || value > ChoiceCommand.MaximumExtra || value != decimal.Truncate(value)) return false;
                    int index = Array.IndexOf(ChoiceNames, parts[1]);
                    if (index < 0 || !seen.Add("choices " + parts[1])) return false;
                    pending.RecordChoice(ChoiceCommand.Keys[index], (int)value);
                    continue;
                }
                bool isFountain = parts[0] == "fountain";
                if (!(isFountain && parts.Length == 3) && !(parts[0] == "stats" && parts.Length == 4)) return false;
                string mode = parts[parts.Length - 2];
                if (mode != "set" && mode != "offset") return false;
                var setting = new Setting(mode == "set", value);
                if (isFountain)
                {
                    if (!seen.Add("fountain") || value != decimal.Truncate(value) || (setting.Absolute && value < 0)) return false;
                    if (!setting.Empty) pending.fountain = setting;
                }
                else
                {
                    StatDefinition? stat = StatCatalog.Find(parts[1]);
                    if (stat == null || stat.Name != parts[1] || !seen.Add("stats " + stat.Name) ||
                        value * stat.Scale != decimal.Truncate(value * stat.Scale) ||
                        (setting.Absolute && (value < stat.Minimum || value > stat.Maximum))) return false;
                    if (!setting.Empty) pending.stats.Add(stat, setting);
                }
            }
            policy = pending;
            error = "";
            return true;
        }
    }
}
