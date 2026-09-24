#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SephiriaOne
{
    internal static class NameGradient
    {
        private const string LegacyPrefix = "<color=#0000FF>";
        private const string Suffix = "</color>";
        private static readonly HashSet<string> FormattingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "b", "i", "u", "s", "mark", "sub", "sup", "color", "alpha", "a", "size", "sprite", "nobr",
            "style", "font", "material", "link", "font-weight", "noparse", "pos", "voffset", "space", "page",
            "align", "width", "gradient", "cspace", "mspace", "indent", "line-indent", "margin", "margin-left",
            "margin-right", "line-height", "action", "scale", "rotate", "lowercase", "allcaps", "uppercase",
            "smallcaps", "liga", "frac", "br", "cr", "zwsp", "zwj", "nbsp", "shy"
        };

        public static string Format(string name) => FormatPlain(Plain(name));

        public static string Plain(string name)
        {
            while (true)
            {
                if (name.StartsWith(LegacyPrefix, StringComparison.Ordinal) &&
                    name.EndsWith(Suffix, StringComparison.Ordinal) && name.Length >= LegacyPrefix.Length + Suffix.Length)
                {
                    name = name.Substring(LegacyPrefix.Length, name.Length - LegacyPrefix.Length - Suffix.Length);
                    continue;
                }
                string candidate = RemoveElementColors(name);
                // Only remove a complete canonical gradient. Preserve unrelated
                // color/style tags, even when some colors happen to match ours.
                if (candidate != name && FormatPlain(candidate) == name)
                {
                    name = candidate;
                    continue;
                }
                return name;
            }
        }

        private static string RemoveElementColors(string name)
        {
            if (name.IndexOf("<color=#", StringComparison.Ordinal) < 0) return name;
            var result = new StringBuilder(name.Length);
            for (int offset = 0; offset < name.Length;)
            {
                if (TryNoParseBlock(name, offset, out int blockEnd))
                {
                    result.Append(name, offset, blockEnd - offset);
                    offset = blockEnd;
                    continue;
                }
                bool colorTag = StartsAt(name, offset, "<color=#") &&
                    name.Length > offset + 15 && name[offset + 14] == '>';
                for (int digit = offset + 8; colorTag && digit < offset + 14; digit++)
                    colorTag = (name[digit] >= '0' && name[digit] <= '9') || (name[digit] >= 'A' && name[digit] <= 'F');
                if (colorTag)
                {
                    string element = StringInfo.GetNextTextElement(name, offset + 15);
                    int end = offset + 15 + element.Length;
                    if (StartsAt(name, end, Suffix))
                    {
                        result.Append(element);
                        offset = end + Suffix.Length;
                        continue;
                    }
                }
                result.Append(name[offset++]);
            }
            return result.ToString();
        }

        private static bool StartsAt(string text, int offset, string value) =>
            text.Length - offset >= value.Length && string.CompareOrdinal(text, offset, value, 0, value.Length) == 0;

        private static bool TryNoParseBlock(string text, int offset, out int end)
        {
            end = offset;
            const string open = "<noparse>";
            if (text.Length - offset < open.Length ||
                string.Compare(text, offset, open, 0, open.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
            int close = text.IndexOf("</noparse>", offset + open.Length, StringComparison.OrdinalIgnoreCase);
            end = close < 0 ? text.Length : close + "</noparse>".Length;
            return true;
        }

        private static bool IsFormattingTag(string text, int start, int end)
        {
            string tag = text.Substring(start + 1, end - start - 1);
            if (tag.StartsWith("#", StringComparison.Ordinal))
            {
                if (tag.Length != 4 && tag.Length != 5 && tag.Length != 7 && tag.Length != 9) return false;
                for (int i = 1; i < tag.Length; i++) if (!Uri.IsHexDigit(tag[i])) return false;
                return true;
            }
            if (tag.StartsWith("/", StringComparison.Ordinal)) tag = tag.Substring(1);
            int delimiter = tag.IndexOfAny(new[] { '=', ' ' });
            if (delimiter >= 0) tag = tag.Substring(0, delimiter);
            return FormattingTags.Contains(tag);
        }

        private static string FormatPlain(string name)
        {
            var parts = new List<(string Text, bool Color)>();
            int letters = 0;
            for (int offset = 0; offset < name.Length;)
            {
                // Leave rich-text tags intact so existing bold/size/color markup
                // does not turn into letters or shift the gradient positions.
                if (name[offset] == '<')
                {
                    int end = name.IndexOf('>', offset + 1);
                    if (end >= 0 && IsFormattingTag(name, offset, end))
                    {
                        if (TryNoParseBlock(name, offset, out int blockEnd))
                        {
                            // Markup inside this native TMP escape block would be
                            // displayed literally. Keep the entire block unchanged.
                            end = blockEnd - 1;
                        }
                        parts.Add((name.Substring(offset, end - offset + 1), false));
                        offset = end + 1;
                        continue;
                    }
                }
                string element = StringInfo.GetNextTextElement(name, offset);
                bool color = !string.IsNullOrWhiteSpace(element);
                parts.Add((element, color));
                if (color) letters++;
                offset += element.Length;
            }
            if (letters == 0) return name;
            var result = new StringBuilder();
            int index = 0;
            foreach (var part in parts)
            {
                if (!part.Color) { result.Append(part.Text); continue; }
                result.Append("<color=#");
                result.Append(Channel(0x40, 0xa8, index, letters));
                result.Append(Channel(0x8a, 0xd7, index, letters));
                result.Append(Channel(0xf1, 0xfa, index, letters));
                result.Append('>').Append(part.Text).Append(Suffix);
                index++;
            }
            return result.ToString();
        }

        private static string Channel(int start, int end, int index, int count)
        {
            // Sample (index + 0.5) / count; integer arithmetic rounds halves up.
            long step = ((end - start) * (2L * index + 1) + count) / (2L * count);
            return (start + step).ToString("X2", CultureInfo.InvariantCulture);
        }
    }
}
