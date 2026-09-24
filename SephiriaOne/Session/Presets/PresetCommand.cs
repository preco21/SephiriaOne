#nullable enable
using System;

namespace SephiriaOne
{
    internal enum PresetAction { NotCommand, Help, Status, Save, Forget, Invalid }

    internal static class PresetCommand
    {
        public const string Usage = "Host only: /mod status shows active settings and player values; /mod save saves them for future hosted sessions; /mod forget removes the saved preset without changing this session.";

        public static PresetAction Parse(string? text, out string error)
        {
            error = Usage;
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("/mod", StringComparison.OrdinalIgnoreCase)) return PresetAction.NotCommand;
            if (parts.Length == 1) return PresetAction.Help;
            if (parts.Length != 2) return PresetAction.Invalid;
            switch (parts[1].ToLowerInvariant())
            {
                case "help": return PresetAction.Help;
                case "status": return PresetAction.Status;
                case "save": return PresetAction.Save;
                case "forget": return PresetAction.Forget;
                default: return PresetAction.Invalid;
            }
        }
    }
}
