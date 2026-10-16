using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private const float PvpBurnMaximumSeconds = 2f;

        // Plasma is a sibling, not a Burn subclass. Blue/evolved forms use
        // properties on these same native types, so they share this lifetime.
        private static bool IsBurnFamily(CharacterDebuff effect) =>
            effect is CharacterDebuff_Burn || effect is CharacterDebuff_Plasma;

        internal static void AfterBurnRefresh(CharacterDebuff __instance)
        {
            if (!NetworkServer.active || !IsBurnFamily(__instance) || __instance.IsEndBuff ||
                !debuffOrigins.TryGetValue(__instance, out var origin) || origin.BurnEnded || !origin.Valid ||
                !ReferenceEquals(__instance.NetworkAttacker, origin.Source) ||
                !ReferenceEquals(__instance.NetworkTarget, origin.Target)) return;
            // Track native refreshes even while balancing is off so enabling it
            // uses the current application age. Never resurrect a retired effect.
            origin.BurnAppliedAt = Time.time;
        }

        // Called inside the validated origin scope, before native server ticks.
        // Each successful native refresh starts another capped lifetime; duration
        // bonuses cannot enlarge it. Native timers/stats need no restoration.
        private static bool UpdateWithinBurnLifetime(CharacterDebuff effect, DebuffOrigin origin)
        {
            if (!IsBurnFamily(effect)) return true;
            if (origin.BurnEnded) return false;
            if (!SessionSettings.FriendlyFireForHit.Balance || !(origin.Target is PlayerAvatar) ||
                !CanAffectTeam(origin.Source, origin.Target)) return true;
            if (!effect.IsEndBuff && Time.time - origin.BurnAppliedAt < PvpBurnMaximumSeconds) return true;

            // Destroy retains Plasma's expiry attack, removes native status and
            // replicates despawn. Native Update still runs during that delay:
            // never permit a late damage tick
            // or repeat destruction, even if balancing is subsequently disabled.
            origin.BurnEnded = true;
            if (!effect.IsEndBuff) effect.Destroy();
            return false;
        }
    }
}
