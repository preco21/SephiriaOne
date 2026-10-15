using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private const float PvpBurnMaximumSeconds = 2f;

        internal static void AfterBurnRefresh(CharacterDebuff __instance)
        {
            if (!NetworkServer.active || !(__instance is CharacterDebuff_Burn) || __instance.IsEndBuff ||
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
            if (!(effect is CharacterDebuff_Burn)) return true;
            if (origin.BurnEnded) return false;
            if (!SessionSettings.FriendlyFireForHit.Balance || !(origin.Target is PlayerAvatar) ||
                !CanAffectTeam(origin.Source, origin.Target)) return true;
            if (!effect.IsEndBuff && Time.time - origin.BurnAppliedAt < PvpBurnMaximumSeconds) return true;

            // Destroy removes native status and replicates despawn. Burn's native
            // Update still runs during that delay: never permit a late damage tick
            // or repeat destruction, even if balancing is subsequently disabled.
            origin.BurnEnded = true;
            if (!effect.IsEndBuff) effect.Destroy();
            return false;
        }
    }
}
