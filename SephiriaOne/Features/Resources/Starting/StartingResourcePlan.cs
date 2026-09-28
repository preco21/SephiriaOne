#nullable disable
using System;

namespace SephiriaOne
{
    // Leaves have two native grants. Never advance more than the native seed:
    // a later invalid multiplier/bonus must be recoverable without reclaiming
    // leaves that the player could already have spent in town.
    internal static class StartingResourcePlan
    {
        public static bool TrySeed(int nativeSeed, ResourceSetting setting, int maximum,
            out int grant, out string error)
        {
            grant = nativeSeed;
            error = null;
            if (nativeSeed < 0) { error = L.T("Native starting leaves are negative."); return false; }
            try
            {
                decimal provisional;
                switch (setting.Mode)
                {
                    case ResourceMode.Set:
                        if (!setting.TryTarget(0, 0, maximum, out int total, out error)) return false;
                        provisional = total;
                        break;
                    case ResourceMode.Offset:
                        if (setting.Amount != decimal.Truncate(setting.Amount))
                        { error = L.T("Starting-leaf offsets must be whole numbers."); return false; }
                        provisional = nativeSeed + setting.Amount;
                        break;
                    case ResourceMode.Multiplier:
                        if (setting.Amount < 0) { error = L.T("Starting-leaf multipliers cannot be negative."); return false; }
                        // This is an allocation, not rounding the requested final
                        // result. TryTarget checks the complete total exactly later.
                        provisional = decimal.Floor(nativeSeed * setting.Amount);
                        break;
                    default:
                        error = L.T("Unknown starting-leaf mode.");
                        return false;
                }
                grant = (int)Math.Min(nativeSeed, Math.Max(0m, provisional));
                return true;
            }
            catch (OverflowException)
            {
                error = L.T("Starting-leaf arithmetic overflowed.");
                return false;
            }
        }

        public static bool TryDeparture(int nativeSeed, int grantedSeed, int nativeBonus,
            bool hasSetting, ResourceSetting setting, int maximum,
            out int grant, out bool fallback, out string error)
        {
            grant = 0;
            fallback = false;
            error = null;
            if (nativeSeed < 0 || grantedSeed < 0 || grantedSeed > nativeSeed || nativeBonus < 0)
            { error = L.T("Invalid starting-leaf checkpoint."); return false; }
            long nativeTotal = (long)nativeSeed + nativeBonus;
            if (nativeTotal > int.MaxValue)
            { error = L.T("Native starting-leaf total exceeds the native integer range."); return false; }
            int target = (int)nativeTotal;
            if (hasSetting)
            {
                if (!setting.TryTarget(target, 0, maximum, out int configured, out error) || configured < grantedSeed)
                {
                    // Restore withheld native seed plus the native departure bonus.
                    // No withdrawal, balance reset, or replay of previous grants.
                    fallback = true;
                    if (string.IsNullOrEmpty(error)) error = L.T("Configured total is smaller than the committed starting allocation.");
                }
                else target = configured;
            }
            grant = target - grantedSeed;
            return true;
        }
    }
}
