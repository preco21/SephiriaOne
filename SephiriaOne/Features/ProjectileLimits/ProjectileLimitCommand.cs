#nullable enable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal enum ProjectileLimitOperation { Reset, Toggle, Total, DefaultSource, Source, ResetSource }
    internal enum ProjectileLimitParseResult { NotCommand, Help, Status, Invalid, Valid }
    internal readonly struct ProjectileLimitCommand
    {
        public ProjectileLimitOperation Operation { get; }
        public bool Enabled { get; }
        public int Limit { get; }
        public string? Source { get; }
        private ProjectileLimitCommand(ProjectileLimitOperation operation, bool enabled = false, int limit = 0, string? source = null)
        { Operation = operation; Enabled = enabled; Limit = limit; Source = source; }

        public ProjectileLimitSettings Apply(ProjectileLimitSettings settings) => Operation switch
        {
            ProjectileLimitOperation.Toggle => settings.WithEnabled(Enabled),
            ProjectileLimitOperation.Total => settings.WithPlayerLimit(Limit),
            ProjectileLimitOperation.DefaultSource => settings.WithDefaultSourceLimit(Limit),
            ProjectileLimitOperation.Source => settings.WithSourceLimit(Source!, Limit),
            ProjectileLimitOperation.ResetSource => settings.WithoutSourceLimit(Source!),
            _ => default
        };
        public static string Usage => L.T("Host only: /one projectiles on|off|reset|status; total N; source default N; source <id> N|reset. Limits: 1–4096; default off, 64 total/player, 12/source. /one save persists settings.");
        public static ProjectileLimitParseResult Parse(string? text, out ProjectileLimitCommand command)
        {
            command = default;
            var parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            bool Is(int i, string value) => parts.Length > i && parts[i].Equals(value, StringComparison.OrdinalIgnoreCase);
            if (!Is(0, "/one") || !Is(1, "projectiles")) return ProjectileLimitParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && Is(2, "help"))) return ProjectileLimitParseResult.Help;
            if (parts.Length == 3)
            {
                if (Is(2, "status")) return ProjectileLimitParseResult.Status;
                if (Is(2, "reset")) return ProjectileLimitParseResult.Valid;
                if (!Is(2, "on") && !Is(2, "off")) return ProjectileLimitParseResult.Invalid;
                command = new ProjectileLimitCommand(ProjectileLimitOperation.Toggle, Is(2, "on"));
                return ProjectileLimitParseResult.Valid;
            }
            if (parts.Length == 4 && Is(2, "total") && TryLimit(parts[3], out int total))
            {
                command = new ProjectileLimitCommand(ProjectileLimitOperation.Total, limit: total);
                return ProjectileLimitParseResult.Valid;
            }
            if (parts.Length != 5 || !Is(2, "source")) return ProjectileLimitParseResult.Invalid;
            if (Is(3, "default"))
            {
                if (!TryLimit(parts[4], out int limit)) return ProjectileLimitParseResult.Invalid;
                command = new ProjectileLimitCommand(ProjectileLimitOperation.DefaultSource, limit: limit);
                return ProjectileLimitParseResult.Valid;
            }
            var source = ProjectileLimitCatalog.Find(parts[3]);
            if (source == null) return ProjectileLimitParseResult.Invalid;
            if (Is(4, "reset"))
                command = new ProjectileLimitCommand(ProjectileLimitOperation.ResetSource, source: source.Id);
            else if (TryLimit(parts[4], out int limit))
                command = new ProjectileLimitCommand(ProjectileLimitOperation.Source, limit: limit, source: source.Id);
            else return ProjectileLimitParseResult.Invalid;
            return ProjectileLimitParseResult.Valid;
        }
        private static bool TryLimit(string text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && ProjectileLimitSettings.ValidLimit(value);
    }
}
