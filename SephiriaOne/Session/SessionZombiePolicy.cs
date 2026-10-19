using System;
using System.Collections.Generic;
using System.Globalization;

namespace SephiriaOne
{
    internal sealed partial class SessionPolicy
    {
        public int ZombieDuration { get; private set; } = ZombieSurvivalSettings.DefaultDuration;
        public int ZombieStartingCount { get; private set; } = ZombieSurvivalSettings.DefaultStartingCount;
        public bool ZombieAnyDeath { get; private set; }
        public CombatRoleSettings ZombieModifiers { get; private set; } = ZombieSurvivalSettings.DefaultModifiers;
        private bool HasZombieChanges => ZombieDuration != ZombieSurvivalSettings.DefaultDuration || ZombieStartingCount != ZombieSurvivalSettings.DefaultStartingCount || ZombieAnyDeath ||
            !ZombieModifiers.Equals(ZombieSurvivalSettings.DefaultModifiers);
        public void RecordZombieDuration(int seconds)
        {
            if (!ZombieSurvivalSettings.ValidDuration(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            ZombieDuration = seconds;
        }
        public void RecordZombieAnyDeath(bool value) => ZombieAnyDeath = value;
        public void RecordZombieStartingCount(int count)
        {
            if (!ZombieSurvivalSettings.ValidStartingCount(count)) throw new ArgumentOutOfRangeException(nameof(count));
            ZombieStartingCount = count;
        }
        public void RecordZombieModifiers(CombatRoleSettings settings) => ZombieModifiers = settings;
        private void ClearZombieSettings()
        { ZombieDuration = ZombieSurvivalSettings.DefaultDuration; ZombieStartingCount = ZombieSurvivalSettings.DefaultStartingCount; ZombieAnyDeath = false; ZombieModifiers = ZombieSurvivalSettings.DefaultModifiers; }
        private void DescribeZombieSettings(List<string> lines)
        {
            if (ZombieDuration != ZombieSurvivalSettings.DefaultDuration) lines.Add("zombie duration " + ZombieDuration.ToString(CultureInfo.InvariantCulture));
            if (ZombieStartingCount != ZombieSurvivalSettings.DefaultStartingCount) lines.Add("zombie count " + ZombieStartingCount.ToString(CultureInfo.InvariantCulture));
            if (ZombieAnyDeath) lines.Add("zombie any-death 1");
            if (!ZombieModifiers.RefillEnabled) lines.Add("zombie refill-enabled 0");
            if (ZombieModifiers.RefillMultiplier != ZombieSurvivalSettings.DefaultRefillMultiplier) lines.Add("zombie refill " + ZombieModifiers.RefillNumber);
            if (!ZombieModifiers.IncomingEnabled) lines.Add("zombie damage-enabled 0");
            if (ZombieModifiers.IncomingPercent != ZombieSurvivalSettings.DefaultIncomingPercent) lines.Add("zombie damage " + ZombieModifiers.IncomingNumber);
        }
        private bool ReadZombieSetting(string key, string text, decimal value)
        {
            if (text != value.ToString("0.##", CultureInfo.InvariantCulture)) return false;
            switch (key)
            {
                case "count":
                    if (value != decimal.Truncate(value) || !ZombieSurvivalSettings.ValidStartingCount((int)value)) return false;
                    RecordZombieStartingCount((int)value); return true;
                case "duration":
                    if (value != decimal.Truncate(value) || !ZombieSurvivalSettings.ValidDuration((int)value)) return false;
                    RecordZombieDuration((int)value); return true;
                case "any-death":
                    if (value != 0 && value != 1) return false;
                    RecordZombieAnyDeath(value == 1); return true;
                case "refill-enabled":
                    if (value != 0 && value != 1) return false;
                    ZombieModifiers = ZombieModifiers.WithRefill(enabled: value == 1); return true;
                case "damage-enabled":
                    if (value != 0 && value != 1) return false;
                    ZombieModifiers = ZombieModifiers.WithIncoming(enabled: value == 1); return true;
                case "refill":
                    if (!CombatRoleSettings.ValidRefillMultiplier(value)) return false;
                    ZombieModifiers = ZombieModifiers.WithRefill(multiplier: value); return true;
                case "damage":
                    if (!CombatRoleSettings.ValidIncomingPercent(value)) return false;
                    ZombieModifiers = ZombieModifiers.WithIncoming(percent: value); return true;
                default: return false;
            }
        }
    }
}
