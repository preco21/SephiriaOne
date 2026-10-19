using System;

namespace SephiriaOne
{
    internal static class PvpWhiteBread
    {
        private const int ItemId = 1119;

        // Adapt the admission decision only. Native inventory callbacks retain
        // ownership of invincibleTime; the original window continues aging so
        // disabling PvP immediately restores the remaining native protection.
        internal static bool IsHitInvincible(bool native, UnitAvatar avatar, float elapsed,
            EInvincibleType kind, Timer fallRecovery)
        {
            if (!native || !(avatar is PlayerAvatar player) || !PvpParticipation.IsActive(player)) return native;
            var inventory = player.Inventory;
            var item = ItemDatabase.FindItemById(ItemId);
            if (!inventory || item == null || !inventory.TryGetUniqueEffect(item, out var effect) ||
                !(effect is Charm_HitInvincible bread) || !bread || !bread.IsEffectEnabled ||
                bread.NetworkAvatar != player) return native;
            var durations = bread.addTimeByLevel;
            if (durations == null || durations.Length == 0) return native;
            float extra = durations[Math.Max(0, Math.Min(bread.CurrentLevelToIdx(), durations.Length - 1))];
            float duration = avatar.invincibleTime;
            if (!Finite(extra) || extra <= 0 || !Finite(duration) || !Finite(elapsed) || elapsed < 0) return native;
            duration = Math.Max(0, duration - extra);
            if (kind == EInvincibleType.Fall)
            {
                if (fallRecovery == null || !Finite(fallRecovery.time) || fallRecovery.time < 0) return native;
                duration += fallRecovery.time;
            }
            else if (kind != EInvincibleType.Normal) return native;
            return elapsed < duration;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
