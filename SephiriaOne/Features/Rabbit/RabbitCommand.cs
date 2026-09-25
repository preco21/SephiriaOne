#nullable enable
using System;

namespace SephiriaOne
{
    internal enum RabbitOption { Infinite, Share, Reset }
    internal enum RabbitParseResult { NotCommand, Help, Status, Invalid, Valid }

    internal readonly struct RabbitCommand
    {
        public RabbitOption Option { get; }
        public bool Enabled { get; }
        public bool IsReset => Option == RabbitOption.Reset || !Enabled;
        public const string Usage = "Host only: /one rabbit infinite on|off, /one rabbit share on|off, /one rabbit reset, /one rabbit status. Applies to Wing-Eared Rabbit healing potions. Save for future sessions: /one save.";

        public RabbitCommand(RabbitOption option, bool enabled)
        { Option = option; Enabled = enabled; }

        public static RabbitParseResult Parse(string? text, out RabbitCommand command, out string error)
        {
            command = default; error = "";
            string[] parts = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("rabbit", StringComparison.OrdinalIgnoreCase)) return RabbitParseResult.NotCommand;
            if (parts.Length == 2 || (parts.Length == 3 && parts[2].Equals("help", StringComparison.OrdinalIgnoreCase)))
                return RabbitParseResult.Help;
            if (parts.Length == 3 && parts[2].Equals("status", StringComparison.OrdinalIgnoreCase)) return RabbitParseResult.Status;
            if (parts.Length == 3 && parts[2].Equals("reset", StringComparison.OrdinalIgnoreCase))
            { command = new RabbitCommand(RabbitOption.Reset, false); return RabbitParseResult.Valid; }
            error = Usage;
            if (parts.Length != 4) return RabbitParseResult.Invalid;
            RabbitOption option;
            if (parts[2].Equals("infinite", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.Infinite;
            else if (parts[2].Equals("share", StringComparison.OrdinalIgnoreCase)) option = RabbitOption.Share;
            else return RabbitParseResult.Invalid;
            bool enabled;
            if (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
            else if (parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
            else return RabbitParseResult.Invalid;
            command = new RabbitCommand(option, enabled); error = "";
            return RabbitParseResult.Valid;
        }
    }
}
