using System;

namespace SephiriaOne
{
    internal static class PvpElementalRuntime
    {
        [ThreadStatic] private static int nativeReads;

        // The native converter has already applied amps, highest-element bonuses
        // and elemental conversion. Never write the underlying synchronized maps.
        internal static void AfterRead(UnitAvatar __instance, int __0, ref int __result)
        {
            if (nativeReads != 0 || (uint)__0 >= 4 || __result <= 0 || !(__instance is PlayerAvatar player)) return;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance || !settings.Elements.Enabled || !PvpParticipation.IsActive(player)) return;
            // Admission can prune stale connections and stop a match. Use the
            // current policy again rather than retaining a pre-admission setting.
            settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Balance || !settings.Elements.Enabled) return;
            float limited = settings.Elements.GetLimit((PvpElement)__0).Limit(__result);
            __result = Math.Min(__result, (int)Math.Floor(limited));
        }

        // Only the exact native stat reads in derivation/progression/UI callers
        // bypass the overlay. Later callbacks in those methods remain balanced.
        internal static int ReadNativeUnsafe(UnitAvatar avatar, string key)
        {
            int previous = nativeReads;
            nativeReads = previous + 1;
            try { return avatar.GetCustomStatUnsafe(key); }
            finally { nativeReads = previous; }
        }

        internal static int ReadNativeEnum(UnitAvatar avatar, ECustomStat key)
        {
            int previous = nativeReads;
            nativeReads = previous + 1;
            try { return avatar.GetCustomStat(key); }
            finally { nativeReads = previous; }
        }
    }
}
