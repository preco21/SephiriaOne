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
        private const string MultiplierPresetHeader = "SephiriaOne preset v2";
        private const string ResourcePresetHeader = "SephiriaOne preset v3";
        private const string RabbitPresetHeader = "SephiriaOne preset v4";
        private const string RabbitBalancePresetHeader = "SephiriaOne preset v5";
        private const string RabbitCostPresetHeader = "SephiriaOne preset v6";
        private const string MerchantPresetHeader = "SephiriaOne preset v7";
        private const string MerchantTypesPresetHeader = "SephiriaOne preset v8";
        private const string MerchantGuaranteePresetHeader = "SephiriaOne preset v9";
        private const string RabbitLevelUpPresetHeader = "SephiriaOne preset v10";
        private const string ItemUnlockPresetHeader = "SephiriaOne preset v11";
        private const string CombatPresetHeader = "SephiriaOne preset v12";
        private const string ExpandedStatsPresetHeader = "SephiriaOne preset v17";
        private const string DeathmatchPresetHeader = "SephiriaOne preset v18";
        private const string FractionalCombatPresetHeader = "SephiriaOne preset v19";
        private const string TournamentPresetHeader = "SephiriaOne preset v20";
        private const string CompanionStatsPresetHeader = "SephiriaOne preset v21";
        private const string LobbyRegenPresetHeader = "SephiriaOne preset v23";
        private const string PlayTagPresetHeader = "SephiriaOne preset v25";
        private const string PlayTagChaserPresetHeader = "SephiriaOne preset v26";
        private const string PvpGuardPresetHeader = "SephiriaOne preset v27";
        private const string PvpDotPresetHeader = "SephiriaOne preset v28";
        private const string HitFrequencyPresetHeader = "SephiriaOne preset v29";
        private const string ExtendedPvpDotPresetHeader = "SephiriaOne preset v34";
        private const string ExperiencePresetHeader = "SephiriaOne preset v39";
        private const string ProjectilePresetHeader = "SephiriaOne preset v42";
        private const string AmplificationPresetHeader = "SephiriaOne preset v43";
        private const string RapidPresetHeader = "SephiriaOne preset v44";
        private const string PerfectGuardPresetHeader = "SephiriaOne preset v45";
        private const string ArtifactRatePresetHeader = "SephiriaOne preset v47";
        private const string ThornsPresetHeader = "SephiriaOne preset v48";
        private const string LowerBoundPresetHeader = "SephiriaOne preset v49";
        private const string ElementalPresetHeader = "SephiriaOne preset v52";
        private const string PeriodicRecoveryPresetHeader = "SephiriaOne preset v51";
        private const string ProjectileRatePresetHeader = "SephiriaOne preset v50";
        private const string DotDurationPresetHeader = "SephiriaOne preset v46";
        private const string ZombieDynamicPresetHeader = "SephiriaOne preset v41";
        private const string SlateSelectionPresetHeader = "SephiriaOne preset v40";
        private const string RequitalPresetHeader = "SephiriaOne preset v38";
        private const string PvpPotionPresetHeader = "SephiriaOne preset v37";
        private const string PotionPresetHeader = "SephiriaOne preset v36";
        private const string PvpDotSchedulePresetHeader = "SephiriaOne preset v35";
        private const string RabbitPvpSharePresetHeader = "SephiriaOne preset v33";
        private const string ZombieCountPresetHeader = "SephiriaOne preset v32";
        private const string ZombiePresetHeader = "SephiriaOne preset v31";
        private const string PlayTagRoundsPresetHeader = "SephiriaOne preset v30";
        private const string PvpRefillPresetHeader = "SephiriaOne preset v24";
        private const string PvpBalancePresetHeader = "SephiriaOne preset v22";
        private const string EventPresetHeader = "SephiriaOne preset v16";
        private const string CollinPresetHeader = "SephiriaOne preset v15";
        private const string BatPresetHeader = "SephiriaOne preset v14";
        private const string JarPresetHeader = "SephiriaOne preset v13";
        private static readonly string[] ChoiceNames = { "item", "weapon", "miracle" };

        private bool UsesMerchantGuaranteePreset
        {
            get
            {
                foreach (var definition in MerchantCatalog.All)
                    if (Merchants.Get(definition.Id).Guarantee != definition.HasGuarantee) return true;
                return false;
            }
        }

        private bool UsesMerchantTypesPreset
        {
            get
            {
                foreach (var definition in MerchantCatalog.All)
                {
                    var settings = Merchants.Get(definition.Id);
                    if (settings.FirstFloor != 1 || settings.MaxPerRun != 0 || settings.Guarantee != definition.HasGuarantee ||
                        (definition.Id != MerchantCatalog.DefaultId && !settings.Equals(MerchantSettings.Defaults(definition)))) return true;
                }
                return false;
            }
        }

        public IReadOnlyList<string> DescribeSettings()
        {
            var lines = new List<string>();
            DescribeZombieSettings(lines);
            if (Projectiles.Enabled) lines.Add("projectiles enabled 1");
            if (Projectiles.PlayerLimit != ProjectileLimitSettings.DefaultPlayerLimit)
                lines.Add("projectiles total " + Projectiles.PlayerLimit.ToString(CultureInfo.InvariantCulture));
            if (Projectiles.DefaultSourceLimit != ProjectileLimitSettings.DefaultPerSourceLimit)
                lines.Add("projectiles default " + Projectiles.DefaultSourceLimit.ToString(CultureInfo.InvariantCulture));
            foreach (var source in ProjectileLimitCatalog.All)
                if (Projectiles.SourceLimits.TryGetValue(source.Id, out int sourceLimit))
                    lines.Add("projectiles source " + source.Id + " " + sourceLimit.ToString(CultureInfo.InvariantCulture));
            if (Projectiles.RateEnabled) lines.Add("projectiles rate-enabled 1");
            if (Projectiles.PlayerRate != ProjectileLimitSettings.DefaultPlayerRate)
                lines.Add("projectiles rate-total " + Projectiles.PlayerRate.ToString(CultureInfo.InvariantCulture));
            if (Projectiles.SourceRate != ProjectileLimitSettings.DefaultSourceRate)
                lines.Add("projectiles rate-source " + Projectiles.SourceRate.ToString(CultureInfo.InvariantCulture));
            if (Experience.Block) lines.Add("xp block 1");
            if (Experience.RabbitExempt) lines.Add("xp rabbit-exempt 1");
            if (Requital.Kind != SlateKind.Requital) lines.Add("slates type " + ((int)Requital.Kind).ToString(CultureInfo.InvariantCulture));
            if (Requital.Automatic) lines.Add("slates auto 1");
            if (Requital.Count != RequitalSettings.DefaultCount) lines.Add("slates count " + Requital.Count.ToString(CultureInfo.InvariantCulture));
            if (DeathmatchDuration != DeathmatchSettings.DefaultDuration) lines.Add("deathmatch duration " + DeathmatchDuration.ToString(CultureInfo.InvariantCulture));
            if (TournamentDuration != TournamentSettings.DefaultDuration) lines.Add("tournament duration " + TournamentDuration.ToString(CultureInfo.InvariantCulture));
            if (PlayTagDuration != PlayTagSettings.DefaultDuration) lines.Add("tag duration " + PlayTagDuration.ToString(CultureInfo.InvariantCulture));
            if (PlayTagRounds != PlayTagSettings.DefaultRounds) lines.Add("tag rounds " + PlayTagRounds.ToString(CultureInfo.InvariantCulture));
            if (!PlayTagChaser.RefillEnabled) lines.Add("tag chaser-refill-enabled 0");
            if (PlayTagChaser.RefillMultiplier != CombatRoleSettings.DefaultRefillMultiplier) lines.Add("tag chaser-refill " + PlayTagChaser.RefillNumber);
            if (!PlayTagChaser.IncomingEnabled) lines.Add("tag chaser-damage-enabled 0");
            if (PlayTagChaser.IncomingPercent != CombatRoleSettings.DefaultIncomingPercent) lines.Add("tag chaser-damage " + PlayTagChaser.IncomingNumber);
            if (EventSpawns.HasChanges) lines.Add("events multiplier " + EventSpawns.Number);
            if (CollinStartingArtifact) lines.Add("collin starting 1");
            if (BatHpSteal) lines.Add("bat hp-steal 1");
            if (JarSpawns.HasChanges) lines.Add("jars " + (JarSpawns.Mode == JarSpawnMode.Chance ? "chance " : "multiplier ") + JarSpawns.Number);
            if (ItemUnlock) lines.Add("items unlock 1");
            if (!FriendlyFire.InfiniteHpPotions) lines.Add("friendlyfire infinite-hp 0");
            if (FriendlyFire.AutoPotion) lines.Add("friendlyfire auto-potion 1");
            if (FriendlyFire.Enabled) lines.Add("friendlyfire enabled 1");
            if (FriendlyFire.BlockLobbyRegen) lines.Add("friendlyfire block-lobby-regen 1");
            if (FriendlyFire.Balance) lines.Add("friendlyfire balance 1");
            if (FriendlyFire.HitFrequency) lines.Add("friendlyfire hit-frequency 1");
            if (FriendlyFire.Rapid.WindowSeconds != PvpRapidSettings.DefaultWindowSeconds) lines.Add("friendlyfire rapid-window " + FriendlyFire.Rapid.WindowNumber);
            if (FriendlyFire.Rapid.HitsPerSecond != PvpRapidSettings.DefaultHitsPerSecond) lines.Add("friendlyfire rapid-rate " + FriendlyFire.Rapid.RateNumber);
            if (FriendlyFire.Rapid.Exponent != PvpRapidSettings.DefaultExponent) lines.Add("friendlyfire rapid-exponent " + FriendlyFire.Rapid.ExponentNumber);
            if (FriendlyFire.Rapid.BurstPercent != PvpRapidSettings.DefaultBurstPercent) lines.Add("friendlyfire rapid-burst " + FriendlyFire.Rapid.BurstNumber);
            if (!FriendlyFire.Rapid.PeriodicReset) lines.Add("friendlyfire rapid-recovery 0");
            if (FriendlyFire.Rapid.ResetSeconds != PvpRapidSettings.DefaultResetSeconds) lines.Add("friendlyfire rapid-interval " + FriendlyFire.Rapid.ResetNumber);
            if (FriendlyFire.DamagePercent != 100) lines.Add("friendlyfire damage " + FriendlyFire.Number);
            if (FriendlyFire.RefillPercent != FriendlyFireSettings.DefaultRefillPercent) lines.Add("friendlyfire refill " + FriendlyFire.RefillNumber);
            if (FriendlyFire.GuardMpPercent != FriendlyFireSettings.DefaultGuardMpPercent) lines.Add("friendlyfire guard-mp " + FriendlyFire.GuardMpNumber);
            if (FriendlyFire.PerfectGuard.Enabled) lines.Add("friendlyfire perfect-guard 1");
            if (FriendlyFire.PerfectGuard.Multiplier != PvpPerfectGuardSettings.DefaultMultiplier)
                lines.Add("friendlyfire perfect-guard-multiplier " + FriendlyFire.PerfectGuard.Number);
            if (FriendlyFire.LowerBound.Enabled) lines.Add("friendlyfire lower-bound 1");
            if (FriendlyFire.LowerBound.ReferencePercent != PvpLowerBoundSettings.DefaultReferencePercent)
                lines.Add("friendlyfire lower-reference " + FriendlyFire.LowerBound.ReferenceNumber);
            if (FriendlyFire.LowerBound.RefillPercent != PvpLowerBoundSettings.DefaultRefillPercent)
                lines.Add("friendlyfire lower-refill " + FriendlyFire.LowerBound.RefillNumber);
            if (FriendlyFire.SuppressThorns) lines.Add("friendlyfire suppress-thorns 1");
            if (FriendlyFire.ArtifactRate.Enabled) lines.Add("friendlyfire artifact-rate 1");
            if (FriendlyFire.ArtifactRate.Seconds != PvpArtifactRateSettings.DefaultSeconds)
                lines.Add("friendlyfire artifact-interval " + FriendlyFire.ArtifactRate.Number);
            if (FriendlyFire.HasDotDurationChanges)
                lines.Add("friendlyfire dot-duration " + FriendlyFire.DotMaxDurationNumber);
            if (!FriendlyFire.Elements.Enabled) lines.Add("friendlyfire elements 0");
            for (int i = 0; i < 4; i++)
            {
                var element = (PvpElement)i;
                var limit = FriendlyFire.Elements.GetLimit(element);
                if (limit.HasChanges) lines.Add("friendlyfire elements-" + element.ToString().ToLowerInvariant() + " " + limit.SoftNumber + " " + limit.HardNumber);
            }
            if (FriendlyFire.Amplification.Enabled) lines.Add("friendlyfire amplification 1");
            if (FriendlyFire.Amplification.SoftPercent != PvpAmplificationSettings.DefaultSoftPercent || FriendlyFire.Amplification.HardPercent != PvpAmplificationSettings.DefaultHardPercent)
                lines.Add("friendlyfire amplification-limits " + FriendlyFire.Amplification.SoftNumber + " " + FriendlyFire.Amplification.HardNumber);
            for (int i = 0; i < PvpDotSettings.Count; i++)
            {
                var effect = (PvpDotEffect)i;
                if (FriendlyFire.DotIntervals.Seconds(effect) != PvpDotSettings.DefaultSeconds(effect))
                    lines.Add("friendlyfire dot-" + PvpDotSettings.Key(effect) + " " + FriendlyFire.DotIntervals.Number(effect));
                if (PvpDotSettings.SupportsSchedule(effect) && !FriendlyFire.DotIntervals.PreserveSchedule(effect))
                    lines.Add("friendlyfire dot-preserve-" + PvpDotSettings.Key(effect) + " 0");
            }
            string Describe(Setting setting) => (setting.Multiplier ? "multiplier " : setting.Absolute ? "set " : "offset ") +
                setting.Value.ToString("0.##", CultureInfo.InvariantCulture);
            if (fountain.HasValue) lines.Add("fountain " + Describe(fountain.Value));
            for (int i = 0; i < ChoiceCommand.Keys.Length; i++)
                if (choices.TryGetValue(ChoiceCommand.Keys[i], out int extra))
                    lines.Add("choices " + ChoiceNames[i] + " " + extra.ToString(CultureInfo.InvariantCulture));
            foreach (StatDefinition stat in StatCatalog.All)
                if (stats.TryGetValue(stat, out Setting setting)) lines.Add("stats " + stat.Name + " " + Describe(setting));
            lines.AddRange(Resources.Describe());
            if (RabbitPotions.Infinite) lines.Add("rabbit infinite 1");
            if (RabbitPotions.Share) lines.Add("rabbit share 1");
            if (RabbitPotions.ConsumeMp) lines.Add("rabbit mp-cost 1");
            if (RabbitPotions.SuppressSurvival) lines.Add("rabbit suppress-survival 1");
            if (RabbitPotions.MpCostPerDrink != RabbitPotionSettings.DefaultMpCostPerDrink)
                lines.Add("rabbit mp-amount " + RabbitPotions.MpCostPerDrink.ToString(CultureInfo.InvariantCulture));
            if (RabbitPotions.LevelUpPotion) lines.Add("rabbit level-up-potion 1");
            if (!RabbitPotions.BlockPvpShare) lines.Add("rabbit block-pvp-share 0");
            bool merchantTypes = UsesMerchantTypesPreset;
            foreach (var definition in MerchantCatalog.All)
            {
                var settings = Merchants.Get(definition.Id);
                string prefix = "merchant " + (merchantTypes ? definition.Id + " " : "");
                if (settings.Enabled) lines.Add(prefix + "spawns 1");
                if (settings.Chance != definition.DefaultChance) lines.Add(prefix + "chance " + settings.Chance.ToString(CultureInfo.InvariantCulture));
                if (settings.FirstFloor != 1) lines.Add(prefix + "from " + settings.FirstFloor.ToString(CultureInfo.InvariantCulture));
                if (settings.MaxPerRun != 0) lines.Add(prefix + "limit " + settings.MaxPerRun.ToString(CultureInfo.InvariantCulture));
                if (settings.Guarantee != definition.HasGuarantee) lines.Add(prefix + "guarantee " + (settings.Guarantee ? "1" : "0"));
            }
            return lines;
        }

        public string ToPresetText()
        {
            bool expandedStats = false, companionStats = false;
            foreach (var stat in stats.Keys)
            { expandedStats |= stat.RequiresExpandedPreset; companionStats |= stat.IsCompanion; }
            bool multiplier = HasFountainMultiplier;
            foreach (Setting setting in stats.Values) multiplier |= setting.Multiplier;
            return (FriendlyFire.Elements.HasChanges ? ElementalPresetHeader : FriendlyFire.Rapid.HasResetChanges ? PeriodicRecoveryPresetHeader : Projectiles.HasRateChanges ? ProjectileRatePresetHeader : FriendlyFire.LowerBound.HasChanges ? LowerBoundPresetHeader : FriendlyFire.SuppressThorns ? ThornsPresetHeader : FriendlyFire.ArtifactRate.HasChanges ? ArtifactRatePresetHeader : FriendlyFire.HasDotDurationChanges ? DotDurationPresetHeader : FriendlyFire.PerfectGuard.HasChanges ? PerfectGuardPresetHeader : FriendlyFire.Rapid.HasChanges ? RapidPresetHeader : FriendlyFire.Amplification.HasChanges ? AmplificationPresetHeader : Projectiles.HasChanges ? ProjectilePresetHeader : ZombieDynamicBalance ? ZombieDynamicPresetHeader : Requital.Kind != SlateKind.Requital ? SlateSelectionPresetHeader : Experience.HasChanges ? ExperiencePresetHeader : Requital.HasChanges ? RequitalPresetHeader : !FriendlyFire.InfiniteHpPotions || FriendlyFire.DotIntervals.HasSeparatedShockChanges ? PvpPotionPresetHeader : FriendlyFire.AutoPotion ? PotionPresetHeader : FriendlyFire.DotIntervals.HasScheduleChanges ? PvpDotSchedulePresetHeader : FriendlyFire.DotIntervals.HasExtendedChanges ? ExtendedPvpDotPresetHeader : !RabbitPotions.BlockPvpShare ? RabbitPvpSharePresetHeader : ZombieStartingCount != ZombieSurvivalSettings.DefaultStartingCount ? ZombieCountPresetHeader : HasZombieChanges ? ZombiePresetHeader : PlayTagRounds != PlayTagSettings.DefaultRounds ? PlayTagRoundsPresetHeader : FriendlyFire.HitFrequency ? HitFrequencyPresetHeader : FriendlyFire.DotIntervals.HasChanges ? PvpDotPresetHeader : FriendlyFire.GuardMpPercent != FriendlyFireSettings.DefaultGuardMpPercent ? PvpGuardPresetHeader : PlayTagChaser.HasChanges ? PlayTagChaserPresetHeader : PlayTagDuration != PlayTagSettings.DefaultDuration ? PlayTagPresetHeader : FriendlyFire.RefillPercent != FriendlyFireSettings.DefaultRefillPercent ? PvpRefillPresetHeader : FriendlyFire.BlockLobbyRegen ? LobbyRegenPresetHeader : FriendlyFire.Balance ? PvpBalancePresetHeader : companionStats ? CompanionStatsPresetHeader : TournamentDuration != TournamentSettings.DefaultDuration ? TournamentPresetHeader : FriendlyFire.HasFractionalPercent ? FractionalCombatPresetHeader : DeathmatchDuration != DeathmatchSettings.DefaultDuration ? DeathmatchPresetHeader : expandedStats ? ExpandedStatsPresetHeader : EventSpawns.HasChanges ? EventPresetHeader : CollinStartingArtifact ? CollinPresetHeader : BatHpSteal ? BatPresetHeader : JarSpawns.HasChanges ? JarPresetHeader : FriendlyFire.HasChanges ? CombatPresetHeader : ItemUnlock ? ItemUnlockPresetHeader : RabbitPotions.LevelUpPotion ? RabbitLevelUpPresetHeader : UsesMerchantGuaranteePreset ? MerchantGuaranteePresetHeader : UsesMerchantTypesPreset ? MerchantTypesPresetHeader : Merchants.HasChanges ? MerchantPresetHeader : RabbitPotions.MpCostPerDrink != RabbitPotionSettings.DefaultMpCostPerDrink ? RabbitCostPresetHeader :
                RabbitPotions.ConsumeMp || RabbitPotions.SuppressSurvival ? RabbitBalancePresetHeader :
                RabbitPotions.HasChanges ? RabbitPresetHeader : Resources.HasChanges ? ResourcePresetHeader : multiplier ? MultiplierPresetHeader : PresetHeader) + "\n" +
                (HasChanges ? string.Join("\n", DescribeSettings()) + "\n" : "");
        }

        public static bool TryReadPreset(string text, out SessionPolicy policy, out string error)
        {
            policy = new SessionPolicy();
            error = L.T("Invalid saved preset; no saved settings were applied.");
            if (text == null || text.Length > MaximumPresetLength) return false;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || (lines[0] != PresetHeader && lines[0] != MultiplierPresetHeader && lines[0] != ResourcePresetHeader && lines[0] != RabbitPresetHeader && lines[0] != RabbitBalancePresetHeader && lines[0] != RabbitCostPresetHeader && lines[0] != MerchantPresetHeader && lines[0] != MerchantTypesPresetHeader && lines[0] != MerchantGuaranteePresetHeader && lines[0] != RabbitLevelUpPresetHeader && lines[0] != ItemUnlockPresetHeader && lines[0] != CombatPresetHeader && lines[0] != JarPresetHeader && lines[0] != BatPresetHeader && lines[0] != CollinPresetHeader && lines[0] != EventPresetHeader && lines[0] != ExpandedStatsPresetHeader && lines[0] != DeathmatchPresetHeader && lines[0] != FractionalCombatPresetHeader && lines[0] != TournamentPresetHeader && lines[0] != CompanionStatsPresetHeader && lines[0] != PvpBalancePresetHeader && lines[0] != LobbyRegenPresetHeader && lines[0] != PvpRefillPresetHeader && lines[0] != PlayTagPresetHeader && lines[0] != PlayTagChaserPresetHeader && lines[0] != PvpGuardPresetHeader && lines[0] != PvpDotPresetHeader && lines[0] != HitFrequencyPresetHeader && lines[0] != PlayTagRoundsPresetHeader && lines[0] != ZombiePresetHeader && lines[0] != ZombieCountPresetHeader && lines[0] != RabbitPvpSharePresetHeader && lines[0] != ExtendedPvpDotPresetHeader && lines[0] != PvpDotSchedulePresetHeader && lines[0] != PotionPresetHeader && lines[0] != PvpPotionPresetHeader && lines[0] != RequitalPresetHeader && lines[0] != ExperiencePresetHeader && lines[0] != SlateSelectionPresetHeader && lines[0] != ZombieDynamicPresetHeader && lines[0] != ProjectilePresetHeader && lines[0] != AmplificationPresetHeader && lines[0] != RapidPresetHeader && lines[0] != PerfectGuardPresetHeader && lines[0] != DotDurationPresetHeader && lines[0] != ArtifactRatePresetHeader && lines[0] != ThornsPresetHeader && lines[0] != LowerBoundPresetHeader && lines[0] != ProjectileRatePresetHeader && lines[0] != PeriodicRecoveryPresetHeader && lines[0] != ElementalPresetHeader)) return false;
            bool allowElements = lines[0] == ElementalPresetHeader;
            bool allowPeriodicRecovery = lines[0] == PeriodicRecoveryPresetHeader || allowElements;
            bool allowProjectileRate = lines[0] == ProjectileRatePresetHeader || allowPeriodicRecovery;
            bool allowLowerBound = lines[0] == LowerBoundPresetHeader || allowProjectileRate;
            bool allowThorns = lines[0] == ThornsPresetHeader || allowLowerBound;
            bool allowArtifactRate = lines[0] == ArtifactRatePresetHeader || allowThorns;
            bool allowDotDuration = lines[0] == DotDurationPresetHeader || allowArtifactRate;
            bool allowPerfectGuard = lines[0] == PerfectGuardPresetHeader || allowDotDuration;
            bool allowRapid = lines[0] == RapidPresetHeader || allowPerfectGuard;
            bool allowAmplification = lines[0] == AmplificationPresetHeader || allowRapid;
            bool allowProjectiles = lines[0] == ProjectilePresetHeader || allowAmplification;
            bool allowZombieDynamic = lines[0] == ZombieDynamicPresetHeader || allowProjectiles;
            bool allowSlateSelection = lines[0] == SlateSelectionPresetHeader || allowZombieDynamic;
            bool allowExperience = lines[0] == ExperiencePresetHeader || allowSlateSelection;
            bool allowRequital = lines[0] == RequitalPresetHeader || allowExperience;
            bool allowPvpPotions = lines[0] == PvpPotionPresetHeader || allowRequital;
            bool allowPotion = lines[0] == PotionPresetHeader || allowPvpPotions;
            bool allowDotSchedule = lines[0] == PvpDotSchedulePresetHeader || allowPotion;
            bool allowExtendedPvpDot = lines[0] == ExtendedPvpDotPresetHeader || allowDotSchedule;
            bool allowRabbitPvpShare = lines[0] == RabbitPvpSharePresetHeader || allowExtendedPvpDot;
            bool allowZombieCount = lines[0] == ZombieCountPresetHeader || allowRabbitPvpShare;
            bool allowZombie = lines[0] == ZombiePresetHeader || allowZombieCount;
            bool allowPlayTagRounds = lines[0] == PlayTagRoundsPresetHeader || allowZombie;
            bool allowHitFrequency = lines[0] == HitFrequencyPresetHeader || allowPlayTagRounds;
            bool allowPvpDot = lines[0] == PvpDotPresetHeader || allowHitFrequency;
            bool allowPvpGuard = lines[0] == PvpGuardPresetHeader || allowPvpDot;
            bool allowPlayTagChaser = lines[0] == PlayTagChaserPresetHeader || allowPvpGuard;
            bool allowPlayTag = lines[0] == PlayTagPresetHeader || allowPlayTagChaser;
            bool allowPvpRefill = lines[0] == PvpRefillPresetHeader || allowPlayTag;
            bool allowLobbyRegen = lines[0] == LobbyRegenPresetHeader || allowPvpRefill;
            bool allowPvpBalance = lines[0] == PvpBalancePresetHeader || allowLobbyRegen;
            bool allowCompanionStats = lines[0] == CompanionStatsPresetHeader || allowPvpBalance;
            bool allowTournament = lines[0] == TournamentPresetHeader || allowCompanionStats;
            bool allowFractionalCombat = lines[0] == FractionalCombatPresetHeader || allowTournament;
            bool allowDeathmatch = lines[0] == DeathmatchPresetHeader || allowFractionalCombat;
            bool allowExpandedStats = lines[0] == ExpandedStatsPresetHeader || allowDeathmatch;
            bool allowEvents = lines[0] == EventPresetHeader || allowExpandedStats;
            bool allowCollin = lines[0] == CollinPresetHeader || allowEvents;
            bool allowBat = lines[0] == BatPresetHeader || allowCollin;
            bool allowJars = lines[0] == JarPresetHeader || allowBat;
            bool allowCombat = lines[0] == CombatPresetHeader || allowJars;
            bool allowItemUnlock = lines[0] == ItemUnlockPresetHeader || allowCombat;
            bool allowLevelUpPotion = lines[0] == RabbitLevelUpPresetHeader || allowItemUnlock;
            bool allowMerchantGuarantee = lines[0] == MerchantGuaranteePresetHeader || allowLevelUpPotion;
            bool allowMerchantTypes = lines[0] == MerchantTypesPresetHeader || allowMerchantGuarantee;
            bool allowMerchant = lines[0] == MerchantPresetHeader || allowMerchantTypes;
            bool allowCost = lines[0] == RabbitCostPresetHeader || allowMerchant;
            bool allowBalance = lines[0] == RabbitBalancePresetHeader || allowCost;
            bool allowRabbit = lines[0] == RabbitPresetHeader || allowBalance;
            bool allowResources = lines[0] == ResourcePresetHeader || allowRabbit;
            bool allowMultiplier = lines[0] != PresetHeader;
            var pending = new SessionPolicy();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                if (!decimal.TryParse(parts[parts.Length - 1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal value) || value < -int.MaxValue || value > int.MaxValue) return false;
                if (parts[0] == "projectiles")
                {
                    if (!allowProjectiles || (parts.Length != 3 && parts.Length != 4)) return false;
                    string field = parts[1];
                    string number = parts[parts.Length - 1];
                    if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int limit) ||
                        number != limit.ToString(CultureInfo.InvariantCulture)) return false;
                    if (parts.Length == 3)
                    {
                        if (!seen.Add("projectiles " + field)) return false;
                        if (field == "enabled" && (limit == 0 || limit == 1))
                            pending.Projectiles = pending.Projectiles.WithEnabled(limit == 1);
                        else if (field == "total" && ProjectileLimitSettings.ValidLimit(limit))
                            pending.Projectiles = pending.Projectiles.WithPlayerLimit(limit);
                        else if (field == "default" && ProjectileLimitSettings.ValidLimit(limit))
                            pending.Projectiles = pending.Projectiles.WithDefaultSourceLimit(limit);
                        else if (allowProjectileRate && field == "rate-enabled" && (limit == 0 || limit == 1))
                            pending.Projectiles = pending.Projectiles.WithRateEnabled(limit == 1);
                        else if (allowProjectileRate && field == "rate-total" && ProjectileLimitSettings.ValidLimit(limit))
                            pending.Projectiles = pending.Projectiles.WithPlayerRate(limit);
                        else if (allowProjectileRate && field == "rate-source" && ProjectileLimitSettings.ValidLimit(limit))
                            pending.Projectiles = pending.Projectiles.WithSourceRate(limit);
                        else return false;
                    }
                    else
                    {
                        var source = ProjectileLimitCatalog.Find(parts[2]);
                        if (field != "source" || source == null || source.Id != parts[2] ||
                            !ProjectileLimitSettings.ValidLimit(limit) || !seen.Add("projectiles source " + source.Id)) return false;
                        pending.Projectiles = pending.Projectiles.WithSourceLimit(source.Id, limit);
                    }
                    continue;
                }
                if (parts[0] == "xp")
                {
                    if (!allowExperience || parts.Length != 3 || !seen.Add("xp " + parts[1]) ||
                        (parts[2] != "0" && parts[2] != "1")) return false;
                    if (parts[1] == "block") pending.RecordExperience(new ExperienceSettings(value == 1, pending.Experience.RabbitExempt));
                    else if (parts[1] == "rabbit-exempt") pending.RecordExperience(new ExperienceSettings(pending.Experience.Block, value == 1));
                    else return false;
                    continue;
                }
                if (parts[0] == "slates")
                {
                    if (!allowRequital || parts.Length != 3 || !seen.Add("slates " + parts[1])) return false;
                    if (parts[1] == "auto" && (parts[2] == "0" || parts[2] == "1"))
                        pending.RecordRequital(new RequitalSettings(value == 1, pending.Requital.Count, pending.Requital.Kind));
                    else if (parts[1] == "count" && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int count) &&
                        RequitalSettings.ValidCount(count) && parts[2] == count.ToString(CultureInfo.InvariantCulture))
                        pending.RecordRequital(new RequitalSettings(pending.Requital.Automatic, count, pending.Requital.Kind));
                    else if (parts[1] == "type" && allowSlateSelection && (parts[2] == "0" || parts[2] == "1"))
                        pending.RecordRequital(new RequitalSettings(pending.Requital.Automatic, pending.Requital.Count, (SlateKind)(int)value));
                    else return false;
                    continue;
                }
                if (parts[0] == "zombie")
                {
                    if (!allowZombie || parts.Length != 3 || (parts[1] == "count" && !allowZombieCount) || (parts[1] == "dynamic" && !allowZombieDynamic) || !seen.Add("zombie " + parts[1]) ||
                        !pending.ReadZombieSetting(parts[1], parts[2], value)) return false;
                    continue;
                }
                if (parts[0] == "events")
                {
                    if (!allowEvents || parts.Length != 3 || parts[1] != "multiplier" || !seen.Add("events") ||
                        !RelativeMultiplier.IsValid(value) || parts[2] != value.ToString("0.##", CultureInfo.InvariantCulture)) return false;
                    pending.RecordEventSpawns(new EventSpawnSettings(value));
                    continue;
                }
                if (parts[0] == "deathmatch")
                {
                    if (!allowDeathmatch || parts.Length != 3 || parts[1] != "duration" || !seen.Add("deathmatch") ||
                        value != decimal.Truncate(value) || !DeathmatchSettings.ValidDuration((int)value) ||
                        parts[2] != value.ToString("0", CultureInfo.InvariantCulture)) return false;
                    pending.RecordDeathmatchDuration((int)value);
                    continue;
                }
                if (parts[0] == "tournament")
                {
                    if (!allowTournament || parts.Length != 3 || parts[1] != "duration" || !seen.Add("tournament") ||
                        value != decimal.Truncate(value) || !TournamentSettings.ValidDuration((int)value) ||
                        parts[2] != value.ToString("0", CultureInfo.InvariantCulture)) return false;
                    pending.RecordTournamentDuration((int)value);
                    continue;
                }
                if (parts[0] == "tag")
                {
                    if (!allowPlayTag || parts.Length != 3 || !seen.Add("tag " + parts[1])) return false;
                    if (allowPlayTagRounds && parts[1] == "rounds" && value >= PlayTagSettings.MinRounds && value <= PlayTagSettings.MaxRounds &&
                        value == decimal.Truncate(value) && parts[2] == value.ToString("0", CultureInfo.InvariantCulture)) pending.RecordPlayTagRounds((int)value);
                    else if (parts[1] == "duration" && value == decimal.Truncate(value) && PlayTagSettings.ValidDuration((int)value) &&
                        parts[2] == value.ToString("0", CultureInfo.InvariantCulture)) pending.RecordPlayTagDuration((int)value);
                    else if (allowPlayTagChaser && parts[1] == "chaser-refill-enabled" && (parts[2] == "0" || parts[2] == "1"))
                        pending.RecordPlayTagChaser(pending.PlayTagChaser.WithRefill(enabled: value == 1));
                    else if (allowPlayTagChaser && parts[1] == "chaser-damage-enabled" && (parts[2] == "0" || parts[2] == "1"))
                        pending.RecordPlayTagChaser(pending.PlayTagChaser.WithIncoming(enabled: value == 1));
                    else if (allowPlayTagChaser && parts[1] == "chaser-refill" && CombatRoleSettings.ValidRefillMultiplier(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.RecordPlayTagChaser(pending.PlayTagChaser.WithRefill(multiplier: value));
                    else if (allowPlayTagChaser && parts[1] == "chaser-damage" && CombatRoleSettings.ValidIncomingPercent(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.RecordPlayTagChaser(pending.PlayTagChaser.WithIncoming(percent: value));
                    else return false;
                    continue;
                }
                if (parts[0] == "collin")
                {
                    if (!allowCollin || parts.Length != 3 || parts[1] != "starting" ||
                        (parts[2] != "0" && parts[2] != "1") || !seen.Add("collin")) return false;
                    pending.RecordCollin(value == 1);
                    continue;
                }
                if (parts[0] == "bat")
                {
                    if (!allowBat || parts.Length != 3 || parts[1] != "hp-steal" ||
                        (parts[2] != "0" && parts[2] != "1") || !seen.Add("bat")) return false;
                    pending.RecordBat(value == 1);
                    continue;
                }
                if (parts[0] == "jars")
                {
                    if (!allowJars || parts.Length != 3 || !seen.Add("jars") ||
                        parts[2] != value.ToString("0.##", CultureInfo.InvariantCulture)) return false;
                    if (parts[1] == "chance" && value >= 0 && value <= 100 && value * 100 == decimal.Truncate(value * 100))
                        pending.RecordJarSpawns(new JarSpawnSettings(JarSpawnMode.Chance, value));
                    else if (parts[1] == "multiplier" && RelativeMultiplier.IsValid(value))
                        pending.RecordJarSpawns(new JarSpawnSettings(JarSpawnMode.Multiplier, value));
                    else return false;
                    continue;
                }
                if (parts[0] == "friendlyfire")
                {
                    if (parts.Length == 4 && parts[1].StartsWith("elements-", StringComparison.Ordinal))
                    {
                        if (!allowElements || !PvpElementalSettings.TryElement(parts[1].Substring(9), out var element) ||
                            parts[1] != "elements-" + element.ToString().ToLowerInvariant() || !seen.Add("friendlyfire " + parts[1]) ||
                            !decimal.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal elementSoft) ||
                            !PvpStatLimit.ValidLimits(elementSoft, value) || parts[2] != elementSoft.ToString("0.##", CultureInfo.InvariantCulture) ||
                            parts[3] != value.ToString("0.##", CultureInfo.InvariantCulture)) return false;
                        pending.Record(new FriendlyFireCommand(false, elements: new PvpElementalChange(element: element, soft: elementSoft, hard: value)));
                        continue;
                    }
                    if (parts.Length == 4 && parts[1] == "amplification-limits")
                    {
                        if (!allowAmplification || !seen.Add("friendlyfire amplification-limits") ||
                            !decimal.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal soft) ||
                            !PvpAmplificationSettings.ValidLimits(soft, value) || parts[2] != soft.ToString("0.##", CultureInfo.InvariantCulture) ||
                            parts[3] != value.ToString("0.##", CultureInfo.InvariantCulture)) return false;
                        pending.Record(new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(softPercent: soft, hardPercent: value)));
                        continue;
                    }
                    if (!allowCombat || parts.Length != 3 || !seen.Add("friendlyfire " + parts[1])) return false;
                    if (parts[1] == "enabled" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(value == 1));
                    else if (allowPerfectGuard && parts[1] == "perfect-guard" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, perfectGuard: new PvpPerfectGuardChange(enabled: value == 1)));
                    else if (allowLowerBound && parts[1] == "lower-bound" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, lowerBound: new PvpLowerBoundChange(enabled: value == 1)));
                    else if (allowLowerBound && parts[1] == "lower-reference" && PvpLowerBoundSettings.ValidReferencePercent(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, lowerBound: new PvpLowerBoundChange(referencePercent: value)));
                    else if (allowLowerBound && parts[1] == "lower-refill" && PvpLowerBoundSettings.ValidRefillPercent(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, lowerBound: new PvpLowerBoundChange(refillPercent: value)));
                    else if (allowThorns && parts[1] == "suppress-thorns" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, suppressThorns: value == 1));
                    else if (allowArtifactRate && parts[1] == "artifact-rate" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, artifactRate: new PvpArtifactRateChange(enabled: value == 1)));
                    else if (allowArtifactRate && parts[1] == "artifact-interval" && PvpArtifactRateSettings.ValidSeconds(value) &&
                        parts[2] == value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, artifactRate: new PvpArtifactRateChange(seconds: value)));
                    else if (allowDotDuration && parts[1] == "dot-duration" && PvpDotSettings.ValidSeconds(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, dotMaxDurationSeconds: value));
                    else if (allowPerfectGuard && parts[1] == "perfect-guard-multiplier" && PvpPerfectGuardSettings.ValidMultiplier(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, perfectGuard: new PvpPerfectGuardChange(multiplier: value)));
                    else if (allowElements && parts[1] == "elements" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, elements: new PvpElementalChange(enabled: value == 1)));
                    else if (allowAmplification && parts[1] == "amplification" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, amplification: new PvpAmplificationChange(enabled: value == 1)));
                    else if (allowPvpPotions && parts[1] == "infinite-hp" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, infiniteHpPotions: value == 1));
                    else if (allowPotion && parts[1] == "auto-potion" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, autoPotion: value == 1));
                    else if (allowLobbyRegen && parts[1] == "block-lobby-regen" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, blockLobbyRegen: value == 1));
                    else if (allowPvpBalance && parts[1] == "balance" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, balance: value == 1));
                    else if (allowHitFrequency && parts[1] == "hit-frequency" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, hitFrequency: value == 1));
                    else if (allowPeriodicRecovery && parts[1] == "rapid-recovery" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(false, rapid: new PvpRapidChange(periodicReset: value == 1)));
                    else if (allowRapid && (parts[1] == "rapid-window" || parts[1] == "rapid-rate" || parts[1] == "rapid-exponent" || parts[1] == "rapid-burst" ||
                        allowPeriodicRecovery && parts[1] == "rapid-interval") &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture) && PvpRapidChange.TryParse(parts[1].Substring(6), parts[2], out var rapid))
                        pending.Record(new FriendlyFireCommand(false, rapid: rapid));
                    else if (allowPvpRefill && parts[1] == "refill" && FriendlyFireSettings.ValidRefillPercent(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, refillPercent: value));
                    else if (allowPvpGuard && parts[1] == "guard-mp" && FriendlyFireSettings.ValidGuardMpPercent(value) &&
                        parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, guardMpPercent: value));
                    else if (allowDotSchedule && parts[1].StartsWith("dot-preserve-", StringComparison.Ordinal) &&
                        PvpDotSettings.TryEffect(parts[1].Substring(13), out var scheduleEffect) &&
                        PvpDotSettings.SupportsSchedule(scheduleEffect) && (scheduleEffect != PvpDotEffect.PlasmaShock || allowPvpPotions) && parts[1] == "dot-preserve-" + PvpDotSettings.Key(scheduleEffect) &&
                        (parts[2] == "0" || parts[2] == "1"))
                    {
                        pending.Record(new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(scheduleEffect, value == 1)));
                        if (!allowPvpPotions && scheduleEffect == PvpDotEffect.Lightning)
                            pending.Record(new FriendlyFireCommand(false, dotSchedule: new PvpDotScheduleChange(PvpDotEffect.PlasmaShock, value == 1)));
                    }
                    else if (allowPvpDot && parts[1].StartsWith("dot-", StringComparison.Ordinal) &&
                        PvpDotSettings.TryEffect(parts[1].Substring(4), out var effect) && parts[1] == "dot-" + PvpDotSettings.Key(effect) &&
                        ((int)effect < 4 || allowExtendedPvpDot) && (effect != PvpDotEffect.PlasmaShock || allowPvpPotions) && PvpDotSettings.ValidSeconds(value) && parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                    {
                        pending.Record(new FriendlyFireCommand(false, dot: new PvpDotChange(effect, value)));
                        if (!allowPvpPotions && effect == PvpDotEffect.Lightning)
                            pending.Record(new FriendlyFireCommand(false, dot: new PvpDotChange(PvpDotEffect.PlasmaShock, value)));
                    }
                    else if (parts[1] == "damage" && FriendlyFireSettings.ValidPercent(value) &&
                        (allowFractionalCombat || value == decimal.Truncate(value)) && parts[2] == value.ToString("0.##", CultureInfo.InvariantCulture))
                        pending.Record(new FriendlyFireCommand(false, value));
                    else return false;
                    continue;
                }
                if (parts[0] == "items")
                {
                    if (!allowItemUnlock || parts.Length != 3 || parts[1] != "unlock" ||
                        (parts[2] != "0" && parts[2] != "1") || !seen.Add("items unlock")) return false;
                    pending.RecordItemUnlock(value == 1);
                    continue;
                }
                if (parts[0] == "merchant")
                {
                    if (!allowMerchant || (parts.Length != 3 && (!allowMerchantTypes || parts.Length != 4))) return false;
                    bool typed = parts.Length == 4;
                    var definition = MerchantCatalog.Find(typed ? parts[1] : MerchantCatalog.DefaultId);
                    if (definition == null || (typed && definition.Id != parts[1])) return false;
                    string field = parts[typed ? 2 : 1];
                    string number = parts[typed ? 3 : 2];
                    if (!seen.Add("merchant " + definition.Id + " " + field)) return false;
                    MerchantOption option;
                    int minimum = 0, maximum;
                    if (field == "spawns") { option = MerchantOption.Toggle; maximum = 1; }
                    else if (field == "chance") { option = MerchantOption.Chance; maximum = 100; }
                    else if (typed && field == "from") { option = MerchantOption.FirstFloor; minimum = 1; maximum = 1000; }
                    else if (typed && field == "limit") { option = MerchantOption.MaxPerRun; maximum = 1000; }
                    else if (typed && allowMerchantGuarantee && field == "guarantee") { option = MerchantOption.Guarantee; maximum = 1; }
                    else return false;
                    if (!MerchantCommand.TryParseNumber(number, minimum, maximum, out int merchantValue) ||
                        number != merchantValue.ToString(CultureInfo.InvariantCulture)) return false;
                    pending.Record(new MerchantCommand(definition.Id, option, merchantValue));
                    continue;
                }
                if (parts[0] == "rabbit")
                {
                    if (!allowRabbit || parts.Length != 3 || !seen.Add("rabbit " + parts[1])) return false;
                    if (allowCost && parts[1] == "mp-amount")
                    {
                        if (!RabbitCommand.TryParseMpCost(parts[2], out int mpCost) ||
                            parts[2] != mpCost.ToString(CultureInfo.InvariantCulture)) return false;
                        pending.Record(new RabbitCommand(RabbitOption.MpAmount, pending.RabbitPotions.ConsumeMp, mpCost));
                        continue;
                    }
                    if (parts[2] != "0" && parts[2] != "1") return false;
                    RabbitOption option;
                    if (parts[1] == "infinite") option = RabbitOption.Infinite;
                    else if (parts[1] == "share") option = RabbitOption.Share;
                    else if (allowBalance && parts[1] == "mp-cost") option = RabbitOption.ConsumeMp;
                    else if (allowBalance && parts[1] == "suppress-survival") option = RabbitOption.SuppressSurvival;
                    else if (allowLevelUpPotion && parts[1] == "level-up-potion") option = RabbitOption.LevelUpPotion;
                    else if (allowRabbitPvpShare && parts[1] == "block-pvp-share") option = RabbitOption.BlockPvpShare;
                    else return false;
                    pending.Record(new RabbitCommand(option, value == 1));
                    continue;
                }
                if (parts[0] == "choices")
                {
                    if (parts.Length != 3 || value < 0 || value > ChoiceCommand.MaximumExtra || value != decimal.Truncate(value)) return false;
                    int index = Array.IndexOf(ChoiceNames, parts[1]);
                    if (index < 0 || !seen.Add("choices " + parts[1])) return false;
                    pending.RecordChoice(ChoiceCommand.Keys[index], (int)value);
                    continue;
                }
                bool isFountain = parts[0] == "fountain";
                if (parts[0] == "resources")
                {
                    if (!allowResources || parts.Length != 4) return false;
                    ResourceDefinition? definition = ResourceCatalog.Find(parts[1]);
                    if (definition == null || definition.Name != parts[1] || !seen.Add("resources " + parts[1])) return false;
                    ResourceMode resourceMode;
                    if (parts[2] == "multiplier")
                    { if (!RelativeMultiplier.IsValid(value)) return false; resourceMode = ResourceMode.Multiplier; }
                    else
                    {
                        if (value != decimal.Truncate(value)) return false;
                        if (parts[2] == "set")
                        { if (value < definition.Minimum || value > definition.Maximum) return false; resourceMode = ResourceMode.Set; }
                        else if (parts[2] == "offset") resourceMode = ResourceMode.Offset;
                        else return false;
                    }
                    pending.Resources.Set(definition.Kind, new ResourceSetting(resourceMode, value));
                    continue;
                }
                if (!(isFountain && parts.Length == 3) && !(parts[0] == "stats" && parts.Length == 4)) return false;
                string mode = parts[parts.Length - 2];
                bool multiplier = mode == "multiplier";
                if (multiplier ? !allowMultiplier || !RelativeMultiplier.IsValid(value) : mode != "set" && mode != "offset") return false;
                var setting = new Setting(mode == "set", value, multiplier);
                if (isFountain)
                {
                    if (!seen.Add("fountain") || (!multiplier && value != decimal.Truncate(value)) || (setting.Absolute && value < 0)) return false;
                    if (!setting.Empty) pending.fountain = setting;
                }
                else
                {
                    StatDefinition? stat = StatCatalog.Find(parts[1]);
                    if (stat == null || (stat.RequiresExpandedPreset && !allowExpandedStats) || (stat.IsCompanion && !allowCompanionStats) || stat.Name != parts[1] || !seen.Add("stats " + stat.Name) ||
                        (!multiplier && value * stat.Scale != decimal.Truncate(value * stat.Scale)) ||
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
