#nullable disable
using System;
using System.Globalization;

namespace SephiriaOne
{
    internal readonly struct FriendlyFireSettings
    {
        // Hundredths of a percent keep equality/presets exact and default(T)
        // at the native damage scale and configured default refill/guard cost.
        private readonly int damageOffset;
        private readonly int refillOffset;
        private readonly int guardMpOffset;
        private readonly int dotDurationOffset;
        public const decimal DefaultDotMaxDurationSeconds = 1.2m;
        private const int DefaultDotDurationHundredths = (int)(DefaultDotMaxDurationSeconds * 100);
        public const decimal DefaultGuardMpPercent = 5m;
        private const int DefaultGuardMpHundredths = (int)(DefaultGuardMpPercent * 100);
        public const decimal DefaultRefillPercent = 70m;
        private const int DefaultRefillHundredths = (int)(DefaultRefillPercent * 100);
        public const double DefaultRefillScale = DefaultRefillHundredths / 10000d;
        public bool Enabled { get; }
        public bool Balance { get; }
        public bool HitFrequency { get; }
        public bool AutoPotion { get; }
        private readonly bool finiteHpPotions;
        public bool InfiniteHpPotions => !finiteHpPotions;
        public bool BlockLobbyRegen { get; }
        public PvpDotSettings DotIntervals { get; }
        public PvpAmplificationSettings Amplification { get; }
        public PvpRapidSettings Rapid { get; }
        public PvpPerfectGuardSettings PerfectGuard { get; }
        public PvpArtifactRateSettings ArtifactRate { get; }
        public decimal DamagePercent => (damageOffset + 10000) / 100m;
        public double DamageScale => (damageOffset + 10000) / 10000d;
        public bool HasDamage => damageOffset > -10000;
        public bool HasFractionalPercent => damageOffset % 100 != 0;
        public string Number => DamagePercent.ToString("0.##", CultureInfo.InvariantCulture);
        public decimal RefillPercent => (refillOffset + DefaultRefillHundredths) / 100m;
        public double RefillScale => (refillOffset + DefaultRefillHundredths) / 10000d;
        public string RefillNumber => RefillPercent.ToString("0.##", CultureInfo.InvariantCulture);
        public decimal GuardMpPercent => (guardMpOffset + DefaultGuardMpHundredths) / 100m;
        internal int GuardMpHundredths => guardMpOffset + DefaultGuardMpHundredths;
        public string GuardMpNumber => GuardMpPercent.ToString("0.##", CultureInfo.InvariantCulture);
        public decimal DotMaxDurationSeconds => (dotDurationOffset + DefaultDotDurationHundredths) / 100m;
        public float DotMaxDuration => (dotDurationOffset + DefaultDotDurationHundredths) / 100f;
        public string DotMaxDurationNumber => DotMaxDurationSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        public bool HasDotDurationChanges => dotDurationOffset != 0;
        public bool HasChanges => Enabled || Balance || HitFrequency || AutoPotion || !InfiniteHpPotions || BlockLobbyRegen || damageOffset != 0 || refillOffset != 0 || guardMpOffset != 0 || DotIntervals.HasChanges || Amplification.HasChanges || Rapid.HasChanges || PerfectGuard.HasChanges || HasDotDurationChanges || ArtifactRate.HasChanges;
        public FriendlyFireSettings(bool enabled, decimal damagePercent, bool balance = false, bool blockLobbyRegen = false,
            decimal refillPercent = DefaultRefillPercent, decimal guardMpPercent = DefaultGuardMpPercent, PvpDotSettings dotIntervals = default,
            bool hitFrequency = false, bool autoPotion = false, bool infiniteHpPotions = true, PvpAmplificationSettings amplification = default,
            PvpRapidSettings rapid = default, PvpPerfectGuardSettings perfectGuard = default,
            decimal dotMaxDurationSeconds = DefaultDotMaxDurationSeconds, PvpArtifactRateSettings artifactRate = default)
        {
            if (!ValidPercent(damagePercent)) throw new ArgumentOutOfRangeException(nameof(damagePercent));
            if (!ValidRefillPercent(refillPercent)) throw new ArgumentOutOfRangeException(nameof(refillPercent));
            if (!ValidGuardMpPercent(guardMpPercent)) throw new ArgumentOutOfRangeException(nameof(guardMpPercent));
            if (!PvpDotSettings.ValidSeconds(dotMaxDurationSeconds)) throw new ArgumentOutOfRangeException(nameof(dotMaxDurationSeconds));
            Enabled = enabled; Balance = balance; BlockLobbyRegen = blockLobbyRegen; damageOffset = (int)(damagePercent * 100) - 10000;
            refillOffset = (int)(refillPercent * 100) - DefaultRefillHundredths;
            guardMpOffset = (int)(guardMpPercent * 100) - DefaultGuardMpHundredths;
            dotDurationOffset = (int)(dotMaxDurationSeconds * 100) - DefaultDotDurationHundredths;
            DotIntervals = dotIntervals;
            Amplification = amplification;
            Rapid = rapid;
            PerfectGuard = perfectGuard;
            ArtifactRate = artifactRate;
            HitFrequency = hitFrequency; AutoPotion = autoPotion; finiteHpPotions = !infiniteHpPotions;
        }
        public static bool ValidPercent(decimal percent) => percent >= 0 && percent <= 300 && percent * 100 == decimal.Truncate(percent * 100);
        public static bool ValidRefillPercent(decimal percent) => percent >= 1 && ValidPercent(percent);
        public static bool ValidGuardMpPercent(decimal percent) => percent <= 100 && ValidPercent(percent);
        private FriendlyFireSettings(FriendlyFireSettings source, bool enabled)
        { Enabled = enabled; Balance = source.Balance; HitFrequency = source.HitFrequency; AutoPotion = source.AutoPotion; finiteHpPotions = source.finiteHpPotions; BlockLobbyRegen = source.BlockLobbyRegen; damageOffset = source.damageOffset; refillOffset = source.refillOffset; guardMpOffset = source.guardMpOffset; DotIntervals = source.DotIntervals; Amplification = source.Amplification; Rapid = source.Rapid; PerfectGuard = source.PerfectGuard; dotDurationOffset = source.dotDurationOffset; ArtifactRate = source.ArtifactRate; }
        // Deathmatch reads this at hit time; keep toggle overlays in integer space.
        public FriendlyFireSettings WithEnabled(bool enabled) => new FriendlyFireSettings(this, enabled);
        public FriendlyFireSettings Apply(FriendlyFireCommand command) => command.Reset ? default :
            new FriendlyFireSettings(command.TouchesMasterToggle ? command.Enabled : Enabled, command.Percent ?? DamagePercent,
                command.Balance ?? Balance, command.BlockLobbyRegen ?? BlockLobbyRegen, command.RefillPercent ?? RefillPercent,
                command.GuardMpPercent ?? GuardMpPercent, command.Dot.HasValue ? DotIntervals.Apply(command.Dot.Value) :
                    command.DotSchedule.HasValue ? DotIntervals.ApplySchedule(command.DotSchedule.Value) : DotIntervals,
                command.HitFrequency ?? HitFrequency, command.AutoPotion ?? AutoPotion, command.InfiniteHpPotions ?? InfiniteHpPotions,
                command.Amplification.HasValue ? command.Amplification.Value.Apply(Amplification) : Amplification,
                command.Rapid.HasValue ? command.Rapid.Value.Apply(Rapid) : Rapid,
                command.PerfectGuard.HasValue ? command.PerfectGuard.Value.Apply(PerfectGuard) : PerfectGuard,
                command.DotMaxDurationSeconds ?? DotMaxDurationSeconds,
                command.ArtifactRate.HasValue ? command.ArtifactRate.Value.Apply(ArtifactRate) : ArtifactRate);
    }

    internal readonly struct PvpAmplificationChange
    {
        public bool? Enabled { get; }
        public decimal? SoftPercent { get; }
        public decimal? HardPercent { get; }
        public bool Reset { get; }
        public bool IsValid => SoftPercent.HasValue == HardPercent.HasValue &&
            (!SoftPercent.HasValue || PvpAmplificationSettings.ValidLimits(SoftPercent.Value, HardPercent.Value));
        public bool IsReset => Reset || Enabled == false && !SoftPercent.HasValue && !HardPercent.HasValue;
        public PvpAmplificationChange(bool? enabled = null, decimal? softPercent = null, decimal? hardPercent = null, bool reset = false)
        { Enabled = enabled; SoftPercent = softPercent; HardPercent = hardPercent; Reset = reset; }
        public PvpAmplificationSettings Apply(PvpAmplificationSettings settings)
        {
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(SoftPercent));
            return Reset ? default : new PvpAmplificationSettings(Enabled ?? settings.Enabled,
                SoftPercent ?? settings.SoftPercent, HardPercent ?? settings.HardPercent);
        }
    }

    internal enum FriendlyFireParseResult { NotCommand, Help, Status, ResetScores, GivePotions, Invalid, Valid }
    internal readonly struct FriendlyFireCommand
    {
        public bool Reset { get; }
        public bool Enabled { get; }
        public decimal? Percent { get; }
        public decimal? RefillPercent { get; }
        public decimal? GuardMpPercent { get; }
        public decimal? DotMaxDurationSeconds { get; }
        public PvpDotChange? Dot { get; }
        public PvpDotScheduleChange? DotSchedule { get; }
        public bool? Balance { get; }
        public bool? HitFrequency { get; }
        public bool? AutoPotion { get; }
        public bool? InfiniteHpPotions { get; }
        public bool? BlockLobbyRegen { get; }
        public PvpAmplificationChange? Amplification { get; }
        public PvpRapidChange? Rapid { get; }
        public PvpPerfectGuardChange? PerfectGuard { get; }
        public PvpArtifactRateChange? ArtifactRate { get; }
        public FriendlyFireCommand(bool enabled, decimal? percent = null, bool reset = false, bool? balance = null, bool? blockLobbyRegen = null,
            decimal? refillPercent = null, decimal? guardMpPercent = null, PvpDotChange? dot = null, bool? hitFrequency = null,
            PvpDotScheduleChange? dotSchedule = null, bool? autoPotion = null, bool? infiniteHpPotions = null,
            PvpAmplificationChange? amplification = null, PvpRapidChange? rapid = null, PvpPerfectGuardChange? perfectGuard = null,
            decimal? dotMaxDurationSeconds = null, PvpArtifactRateChange? artifactRate = null)
        { Enabled = enabled; Percent = percent; Reset = reset; Balance = balance; HitFrequency = hitFrequency; AutoPotion = autoPotion; InfiniteHpPotions = infiniteHpPotions; BlockLobbyRegen = blockLobbyRegen; RefillPercent = refillPercent; GuardMpPercent = guardMpPercent; Dot = dot; DotSchedule = dotSchedule; Amplification = amplification; Rapid = rapid; PerfectGuard = perfectGuard; DotMaxDurationSeconds = dotMaxDurationSeconds; ArtifactRate = artifactRate; }
        public bool IsReset => Reset || !Dot.HasValue && !GuardMpPercent.HasValue && !RefillPercent.HasValue &&
            (ArtifactRate.HasValue ? ArtifactRate.Value.IsReset : DotMaxDurationSeconds.HasValue ? DotMaxDurationSeconds.Value == FriendlyFireSettings.DefaultDotMaxDurationSeconds : PerfectGuard.HasValue ? PerfectGuard.Value.IsReset : Rapid.HasValue ? Rapid.Value.IsReset : Amplification.HasValue ? Amplification.Value.IsReset : InfiniteHpPotions.HasValue ? !InfiniteHpPotions.Value : AutoPotion.HasValue ? !AutoPotion.Value : DotSchedule.HasValue ? !DotSchedule.Value.Enabled : HitFrequency.HasValue ? !HitFrequency.Value : BlockLobbyRegen.HasValue ? !BlockLobbyRegen.Value : Balance.HasValue ? !Balance.Value : !Percent.HasValue && !Enabled);
        public bool TouchesMasterToggle => Reset || !ArtifactRate.HasValue && !DotMaxDurationSeconds.HasValue && !PerfectGuard.HasValue && !Rapid.HasValue && !Amplification.HasValue && !InfiniteHpPotions.HasValue && !AutoPotion.HasValue && !Percent.HasValue && !Balance.HasValue && !HitFrequency.HasValue && !BlockLobbyRegen.HasValue && !RefillPercent.HasValue && !GuardMpPercent.HasValue && !Dot.HasValue && !DotSchedule.HasValue;
        public static string Usage => L.T("Infinite PvP HP potions: /one friendlyfire infinite-hp on|off|reset. Default on; active participants keep Small/Large HP potions, pay no MP and gain no Survival random stat. Sample and other potions are excluded.") + " " + L.T("Large Potion: /one friendlyfire potion gives one to each player who lacks it. /one friendlyfire auto-potion on|off|reset grants one on PvP entry or match start, only to participants. Default off; /one save persists it.") + " " + L.T("Friendly fire: /one friendlyfire on|off|status|reset|resetkda; /one friendlyfire damage 0..300 (percent, up to 2 decimals; e.g. 0.1). Default 100. /one save persists settings.") + " " +
            L.T("PvP balancing: /one friendlyfire balance on|off. Default off; softens hits, limits rapid damage and Dark Cloud repeats, and uses configurable DoT intervals. Lightning/Shock, Freeze, Chim and Dark Cloud have separate configurable damage intervals; Freeze stun is preserved.") + " " +
            L.T("PvP Evasion: stat-based dodge chance is capped at 50% before anti-Evasion; guaranteed item dodges are unchanged.") + " " +
            L.T("Balanced PvP: Elastic Band restores dash charges at most once every 2s per defender; extended weapon-counter protection is capped at 0.35s. PvE stays native.") + " " +
            L.T("PvP recovery: HP Steal is disabled on friendly-fire hits.") + " " +
            L.T("Rabbit shared healing is blocked in PvP by default. /one rabbit block-pvp-share off permits it when Share HP healing is enabled.") + " " +
            L.T("Hit-frequency scaling: /one friendlyfire hit-frequency on|off|reset. Default off; direct and incidental hits have separate rate histories, with one shared damage allowance. Requires PvP balancing.") + " " +
            L.T("Rapid hits: /one friendlyfire rapid window 0.1..10; rate 0.1..100; exponent 0..2; burst 1..100 (up to 2 decimals). Each accepts reset; rapid reset restores only these four numbers to 1s, 4 hits/s, 0.5 and 20%.") + " " +
            L.T("PvP damage allowance: valid positive hits retain at least 1 damage before shields when credit runs out. Native defenses and debuff intervals still apply; extreme hit rates can exceed the refill limit.") + " " +
            L.T("PvP DoT: /one friendlyfire dot burn|plasma-burn|frostbite|poison|wound|lightning|plasma-shock|freeze|chim|dark-cloud 0.05..10|reset (seconds, up to 2 decimals). Lightning and Plasma shock have separate clocks; Freeze controls damage, not stun. Only with PvP balancing on.") + " " +
            L.T("Autonomous artifacts: /one friendlyfire artifact-rate on|off|reset; artifact-rate interval 0.05..60|reset (seconds, up to 2 decimals). Default off, 2s. Hard interval per attacker/artifact/opponent; requires PvP balancing. Dark Cloud uses the longer of this and its own interval. Weapons, books, companions, planets and other Ice Armaments are excluded.") + " " +
            L.T("PvP DoT duration: /one friendlyfire dot-duration 0.05..10|reset (seconds, up to 2 decimals). Default 1.2; caps Burn, Plasma Burn and blue/evolved variants while balancing is on. Native reapplications may refresh this duration; other debuffs and tick intervals stay unchanged.") + " " +
            L.T("Keep debuff schedule: /one friendlyfire dot <effect> preserve on|off|reset. Default on; keeps the first native hit, then requires one interval between hits, even on reapplication. Does not change stacks or duration refreshes. Dark Cloud always uses its separate strike clock. Only with PvP balancing on.") + " " +
            L.T("PvP refill: /one friendlyfire refill 1..300|reset (percent of max HP per second, up to 2 decimals). Default 70; applies only with PvP balancing on.") + " " +
            L.T("PvP shield minimum: /one friendlyfire guard-mp 0..100|reset (percent of max MP, up to 2 decimals). Default 5; guards, parries and automatic shields share the floor after reductions. Costs round up and spend available MP. Enabled Perfect Guard multiplier is exempt. Only with PvP balancing on.") + " " +
            L.T("Perfect Guard: /one friendlyfire perfect-guard on|off|reset; perfect-guard multiplier x1..x100|reset (up to 2 decimals). Default off, x2. Multiplies native Perfect Guard MP cost and rounds up, ignoring the PvP shield minimum. x1 keeps native cost; off keeps normal PvP guard costs. Only with PvP balancing on.") + " " +
            L.T("All Damage Amplified limit: /one friendlyfire amplification on|off|reset; amplification limits <soft> <hard>. Default off, soft 50%, hard 100%. Bonus percentages, not C-menu totals: 100% means +100%. Use 0..10000 with up to 2 decimals and soft <= hard. Only with PvP balancing on.") + " " +
            L.T("Lobby recovery: /one friendlyfire block-lobby-regen on|off. Default off; blocks automatic HP/MP recovery for active lobby participants. Potions and skill healing remain available.");
        internal static bool TryPercent(string text, out decimal percent)
        {
            string number = (text ?? "").Trim();
            if (number.EndsWith("%", StringComparison.Ordinal)) number = number.Substring(0, number.Length - 1);
            int point = number.IndexOf('.');
            percent = 0;
            return (point < 0 || number.Length - point - 1 <= 2) && decimal.TryParse(number, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out percent) && FriendlyFireSettings.ValidPercent(percent);
        }
        internal static bool TryAmplificationPercent(string text, out decimal percent)
        {
            string number = (text ?? "").Trim();
            if (number.EndsWith("%", StringComparison.Ordinal)) number = number.Substring(0, number.Length - 1);
            int point = number.IndexOf('.');
            percent = 0;
            return (point < 0 || number.Length - point - 1 <= 2) && decimal.TryParse(number, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out percent) && PvpAmplificationSettings.ValidLimits(percent, percent);
        }
        public static FriendlyFireParseResult Parse(string text, out FriendlyFireCommand command)
        {
            command = default;
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("friendlyfire", StringComparison.OrdinalIgnoreCase)) return FriendlyFireParseResult.NotCommand;
            string op = parts.Length > 2 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length <= 3)
            {
                if (op == "help") return FriendlyFireParseResult.Help;
                if (op == "status") return FriendlyFireParseResult.Status;
                if (op == "potion") return FriendlyFireParseResult.GivePotions;
                if (op == "resetkda") return FriendlyFireParseResult.ResetScores;
                if (op == "on" || op == "off" || op == "reset")
                { command = new FriendlyFireCommand(op == "on", reset: op == "reset"); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "infinite-hp" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("off", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, infiniteHpPotions: !parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "rapid" && parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase))
            { command = new FriendlyFireCommand(false, rapid: new PvpRapidChange(reset: true)); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "artifact-rate" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("off", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            {
                command = new FriendlyFireCommand(false, artifactRate: new PvpArtifactRateChange(
                    enabled: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase), reset: parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)));
                return FriendlyFireParseResult.Valid;
            }
            if (parts.Length == 5 && op == "artifact-rate" && parts[3].Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                decimal seconds = PvpArtifactRateSettings.DefaultSeconds;
                if (parts[4].Equals("reset", StringComparison.OrdinalIgnoreCase) || PvpArtifactRateSettings.TrySeconds(parts[4], out seconds))
                { command = new FriendlyFireCommand(false, artifactRate: new PvpArtifactRateChange(seconds: seconds)); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "dot-duration")
            {
                decimal seconds = FriendlyFireSettings.DefaultDotMaxDurationSeconds;
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase) || PvpDotSettings.TrySeconds(parts[3], out seconds))
                { command = new FriendlyFireCommand(false, dotMaxDurationSeconds: seconds); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 5 && op == "rapid" && PvpRapidChange.TryParse(parts[3], parts[4], out var rapid))
            { command = new FriendlyFireCommand(false, rapid: rapid); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "perfect-guard" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("off", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            {
                command = new FriendlyFireCommand(false, perfectGuard: new PvpPerfectGuardChange(
                    enabled: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase), reset: parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)));
                return FriendlyFireParseResult.Valid;
            }
            if (parts.Length == 5 && op == "perfect-guard" && parts[3].Equals("multiplier", StringComparison.OrdinalIgnoreCase))
            {
                decimal multiplier = PvpPerfectGuardSettings.DefaultMultiplier;
                if (parts[4].Equals("reset", StringComparison.OrdinalIgnoreCase) || PvpPerfectGuardSettings.TryMultiplier(parts[4], out multiplier))
                { command = new FriendlyFireCommand(false, perfectGuard: new PvpPerfectGuardChange(multiplier: multiplier)); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "amplification" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("off", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            {
                command = new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(
                    enabled: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase), reset: parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)));
                return FriendlyFireParseResult.Valid;
            }
            if (parts.Length == 6 && op == "amplification" && parts[3].Equals("limits", StringComparison.OrdinalIgnoreCase) &&
                TryAmplificationPercent(parts[4], out decimal soft) && TryAmplificationPercent(parts[5], out decimal hard) &&
                PvpAmplificationSettings.ValidLimits(soft, hard))
            {
                command = new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(softPercent: soft, hardPercent: hard));
                return FriendlyFireParseResult.Valid;
            }
            if (parts.Length == 4 && op == "auto-potion" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("off", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, autoPotion: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "damage" && TryPercent(parts[3], out decimal percent))
            { command = new FriendlyFireCommand(false, percent); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 5 && op == "dot" && PvpDotSettings.TryEffect(parts[3], out var effect))
            {
                decimal seconds = PvpDotSettings.DefaultSeconds(effect);
                if (parts[4].Equals("reset", StringComparison.OrdinalIgnoreCase) || PvpDotSettings.TrySeconds(parts[4], out seconds))
                { command = new FriendlyFireCommand(false, dot: new PvpDotChange(effect, seconds)); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 6 && op == "dot" && PvpDotSettings.TryEffect(parts[3], out var scheduleEffect) &&
                PvpDotSettings.SupportsSchedule(scheduleEffect) && parts[4].Equals("preserve", StringComparison.OrdinalIgnoreCase) &&
                (parts[5].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[5].Equals("off", StringComparison.OrdinalIgnoreCase) ||
                 parts[5].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            {
                command = new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(scheduleEffect,
                    !parts[5].Equals("off", StringComparison.OrdinalIgnoreCase)));
                return FriendlyFireParseResult.Valid;
            }
            if (parts.Length == 4 && op == "refill")
            {
                decimal refill = FriendlyFireSettings.DefaultRefillPercent;
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                    TryPercent(parts[3], out refill) && FriendlyFireSettings.ValidRefillPercent(refill))
                { command = new FriendlyFireCommand(false, refillPercent: refill); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "guard-mp")
            {
                decimal guard = FriendlyFireSettings.DefaultGuardMpPercent;
                if (parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                    TryPercent(parts[3], out guard) && FriendlyFireSettings.ValidGuardMpPercent(guard))
                { command = new FriendlyFireCommand(false, guardMpPercent: guard); return FriendlyFireParseResult.Valid; }
            }
            if (parts.Length == 4 && op == "balance" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, balance: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "hit-frequency" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("off", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("reset", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, hitFrequency: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            if (parts.Length == 4 && op == "block-lobby-regen" && (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)))
            { command = new FriendlyFireCommand(false, blockLobbyRegen: parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)); return FriendlyFireParseResult.Valid; }
            return FriendlyFireParseResult.Invalid;
        }
    }
}
