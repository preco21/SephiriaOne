#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class StatDefinition
    {
        public string Name { get; }
        public string Key { get; }
        public string Label { get; }
        public string MenuId { get; }
        public bool RequiresExpandedPreset { get; }
        public int Scale { get; }
        public int Offset { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public string Unit { get; }
        public string Marker { get; }

        public StatDefinition(string name, string key, string unit, int scale = 1,
            int offset = 0, int minimum = 0, int maximum = 10000,
            string label = "", string menuId = "", bool requiresExpandedPreset = false)
        {
            Name = name; Label = label.Length == 0 ? name : label;
            MenuId = menuId; RequiresExpandedPreset = requiresExpandedPreset;
            Key = key;
            Marker = "SEPHIRIAONE_STAT_" + key;
            Unit = unit;
            Scale = scale;
            Offset = offset;
            Minimum = minimum;
            Maximum = maximum;
        }

        public decimal Display(int effective) => (decimal)effective / Scale + Offset;
    }

    internal static class StatCatalog
    {
        public static readonly IReadOnlyList<StatDefinition> All = Array.AsReadOnly(new[]
        {
            new StatDefinition("luck", "LUCK", "points", label: "Luck", menuId: "LUCK"),
            new StatDefinition("defense", "DAMAGEREDUCTION", "defense points", label: "Defense", menuId: "DEFENSE"),
            new StatDefinition("attackspeed", "ATTACKSPEED", "total % (100 = normal)", offset: 100, minimum: 1, maximum: 1000, label: "Attack speed", menuId: "ATTACK_SPEED"),
            new StatDefinition("critical", "CRITICAL", "chance %", scale: 100, maximum: 100, label: "Critical chance", menuId: "CRITICAL_CHANCE"),
            new StatDefinition("criticaldamage", "CRITICALDAMAGEBONUS", "bonus % (50 = default)", offset: 50, label: "Critical damage", menuId: "CRITICAL_DAMAGE_RATE"),
            new StatDefinition("evasion", "EVASION", "rating, not dodge %", scale: 100, maximum: 100, label: "Evasion rating", menuId: "EVASION"),
            new StatDefinition("cooldown", "COOLDOWNRECOVERYSPEED", "recovery points", label: "Cooldown recovery", menuId: "COOLDOWN_RECOVERY_SPEED"),
            new StatDefinition("mpregen", "MPREGEN", "regeneration points", label: "MP regeneration", menuId: "MP_REGEN"),
            new StatDefinition("negotiation", "NEGOTIATION", "points", label: "Negotiation", menuId: "NEGOTIATION"),
            new StatDefinition("truedamage", "TRUEDAMAGE", "points", label: "True damage", menuId: "TRUE_DAMAGE"),

            Expanded("toughness", "TOUGHNESS", "Toughness", "TOUGHNESS", "damage reduced per hit"),
            Expanded("dashrecovery", "DASHRECOVERY", "Dash recovery speed", "DASH_RECOVERY_SPEED", "total % (100 = normal)", offset: 100, minimum: 1, maximum: 1000),
            Expanded("expdrop", "EXPDROP", "Experience drop", "EXP_DROP", "total % (100 = normal)", offset: 100),
            Expanded("leafdrop", "MONEYDROP", "Leaf drop", "LEAF_DROP", "total % (100 = normal)", offset: 100),
            Expanded("thorns", "THORNS", "Thorns", "THORNS", "reflection % of defense"),
            Expanded("normaldamage", "BASICATTACKDAMAGEBONUS", "Normal attack damage", "BASIC_ATTACK_DAMAGE", "total % (100 = normal)", offset: 100),
            Expanded("dashdamage", "DASHATTACKDAMAGEBONUS", "Dash attack damage", "DASH_ATTACK_DAMAGE", "total % (100 = normal)", offset: 100),
            Expanded("specialdamage", "SPECIALATTACKDAMAGEBONUS", "Special attack damage", "SPECIAL_ATTACK_DAMAGE", "total % (100 = normal)", offset: 100),
            Expanded("weapondamage", "FINALWEAPONDAMAGE", "Weapon damage", "FINAL_WEAPONDAMAGE", "total % (100 = normal)", offset: 100),
            Expanded("grimoiredamage", "MAGICDAMAGEBONUS", "Grimoire damage", "MAGIC_DAMAGE_BONUS", "total % (100 = normal)", offset: 100),
            Expanded("alldamage", "ALLDAMAGEBONUS", "Universal damage boost", "FINAL_DAMAGE", "total % (100 = normal)", offset: 100),
            Expanded("hpsteal", "HPSTEAL", "Life steal", "HP_STEAL", "points (1 = 0.1% steal)"),
            Expanded("mpsteal", "MPSTEAL", "MP steal", "MP_STEAL", "points (1 = 0.1% steal)"),
            Expanded("ignoredefense", "IGNOREDEFENSE", "Ignore defense", "IGNORE_DEFENSE", "defense ignored %", maximum: 100),
            Expanded("debuffduration", "DEBUFFDURATION", "Debuff duration", "DEBUFFDURATION", "bonus %"),
            Expanded("debuffdamage", "DEBUFFDAMAGE", "Debuff damage", "DEBUFFDAMAGE", "bonus %"),
            Expanded("crossbowreload", "CROSSBOWRELOADSPEED", "Crossbow reload speed", "CROSSBOWRELOADSPEED", "total % (100 = normal)", offset: 100, minimum: 1, maximum: 1000)
        });

        // Explicit menu allowlist: do not enumerate StatusDatabase/ECustomStat.
        // Several native stats are hidden, and some visible rows use composite
        // fields rather than the custom-stat arithmetic supported here.
        private static StatDefinition Expanded(string name, string key, string label, string menuId, string unit,
            int offset = 0, int minimum = 0, int maximum = 10000) =>
            new StatDefinition(name, key, unit, offset: offset, minimum: minimum, maximum: maximum,
                label: label, menuId: menuId, requiresExpandedPreset: true);

        public static StatDefinition? Find(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "armor": name = "defense"; break;
                case "attack-speed": name = "attackspeed"; break;
                case "crit": case "crit-chance": name = "critical"; break;
                case "critdamage": case "crit-damage": name = "criticaldamage"; break;
                case "cooldownrecovery": name = "cooldown"; break;
                case "mp-regen": name = "mpregen"; break;
                case "dash-recovery": name = "dashrecovery"; break;
                case "experience": case "expgain": name = "expdrop"; break;
                case "moneydrop": case "leafgain": name = "leafdrop"; break;
                case "basicdamage": case "normalattackdamage": name = "normaldamage"; break;
                case "dashattackdamage": name = "dashdamage"; break;
                case "specialattackdamage": name = "specialdamage"; break;
                case "magicdamage": name = "grimoiredamage"; break;
                case "finaldamage": name = "alldamage"; break;
                case "lifesteal": case "hp-steal": name = "hpsteal"; break;
                case "mp-steal": name = "mpsteal"; break;
                case "penetration": name = "ignoredefense"; break;
                case "crossbowreloadspeed": name = "crossbowreload"; break;
            }
            foreach (StatDefinition stat in All)
                if (stat.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return stat;
            return null;
        }
    }

}
