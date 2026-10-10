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
            if (DeathmatchDuration != DeathmatchSettings.DefaultDuration) lines.Add("deathmatch duration " + DeathmatchDuration.ToString(CultureInfo.InvariantCulture));
            if (EventSpawns.HasChanges) lines.Add("events multiplier " + EventSpawns.Number);
            if (CollinStartingArtifact) lines.Add("collin starting 1");
            if (BatHpSteal) lines.Add("bat hp-steal 1");
            if (JarSpawns.HasChanges) lines.Add("jars " + (JarSpawns.Mode == JarSpawnMode.Chance ? "chance " : "multiplier ") + JarSpawns.Number);
            if (ItemUnlock) lines.Add("items unlock 1");
            if (FriendlyFire.Enabled) lines.Add("friendlyfire enabled 1");
            if (FriendlyFire.DamagePercent != 100) lines.Add("friendlyfire damage " + FriendlyFire.Number);
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
            bool expandedStats = false;
            foreach (var stat in stats.Keys) expandedStats |= stat.RequiresExpandedPreset;
            bool multiplier = HasFountainMultiplier;
            foreach (Setting setting in stats.Values) multiplier |= setting.Multiplier;
            return (FriendlyFire.HasFractionalPercent ? FractionalCombatPresetHeader : DeathmatchDuration != DeathmatchSettings.DefaultDuration ? DeathmatchPresetHeader : expandedStats ? ExpandedStatsPresetHeader : EventSpawns.HasChanges ? EventPresetHeader : CollinStartingArtifact ? CollinPresetHeader : BatHpSteal ? BatPresetHeader : JarSpawns.HasChanges ? JarPresetHeader : FriendlyFire.HasChanges ? CombatPresetHeader : ItemUnlock ? ItemUnlockPresetHeader : RabbitPotions.LevelUpPotion ? RabbitLevelUpPresetHeader : UsesMerchantGuaranteePreset ? MerchantGuaranteePresetHeader : UsesMerchantTypesPreset ? MerchantTypesPresetHeader : Merchants.HasChanges ? MerchantPresetHeader : RabbitPotions.MpCostPerDrink != RabbitPotionSettings.DefaultMpCostPerDrink ? RabbitCostPresetHeader :
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
            if (lines.Length == 0 || (lines[0] != PresetHeader && lines[0] != MultiplierPresetHeader && lines[0] != ResourcePresetHeader && lines[0] != RabbitPresetHeader && lines[0] != RabbitBalancePresetHeader && lines[0] != RabbitCostPresetHeader && lines[0] != MerchantPresetHeader && lines[0] != MerchantTypesPresetHeader && lines[0] != MerchantGuaranteePresetHeader && lines[0] != RabbitLevelUpPresetHeader && lines[0] != ItemUnlockPresetHeader && lines[0] != CombatPresetHeader && lines[0] != JarPresetHeader && lines[0] != BatPresetHeader && lines[0] != CollinPresetHeader && lines[0] != EventPresetHeader && lines[0] != ExpandedStatsPresetHeader && lines[0] != DeathmatchPresetHeader && lines[0] != FractionalCombatPresetHeader)) return false;
            bool allowFractionalCombat = lines[0] == FractionalCombatPresetHeader;
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
                    if (!allowCombat || parts.Length != 3 || !seen.Add("friendlyfire " + parts[1])) return false;
                    if (parts[1] == "enabled" && (parts[2] == "0" || parts[2] == "1"))
                        pending.Record(new FriendlyFireCommand(value == 1));
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
                    if (stat == null || (stat.RequiresExpandedPreset && !allowExpandedStats) || stat.Name != parts[1] || !seen.Add("stats " + stat.Name) ||
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
