using System;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        [ThreadStatic] private static UnitAvatar offensiveSeeker;

        // Forward mask consumers differ from ItemTarget's native reverse mask.
        internal static bool ItemHitTarget(UnitAvatar source, EDamageFromType type, UnitAvatar target)
        {
            if (NetworkServer.active && IsPlayerTeamPair(source, target)) return CanAffectTeam(source, target);
            return CombatManager.ContainsAttackableFaction(source.GetHostileFactionLayers(type), target.faction);
        }

        internal static UnitAvatar OffensiveNearestPoint(UnitAvatar source, Vector2 point, float squaredRadius)
        {
            var previous = offensiveSeeker;
            offensiveSeeker = source;
            try { return PlayerInputController.SearchTargetNearestPoint(source, point, squaredRadius); }
            finally { offensiveSeeker = previous; }
        }

        internal static long OffensiveNearestMask(UnitAvatar source, EDamageFromType type, UnitAvatar target)
        {
            if (NetworkServer.active && ReferenceEquals(source, offensiveSeeker) && IsPlayerTeamPair(source, target))
                return CanAffectTeam(source, target) ? 1L : 0L;
            return source.GetHostileFactionLayers(type) & RuntimeFactionManager.Instance.FindFactionLayer(target.faction);
        }

        // Verified projectile identities are read on every acquisition/loss check:
        // pooled bullets must not retain eligibility from their previous owner.
        internal static bool IsArtifactHoming(Bullet bullet) => bullet.FromType == EDamageFromType.None &&
            (bullet.damageId == "Charm_PallasCard" || bullet.damageId == "Charm_NearMagicBullet" ||
             bullet.damageId == "Charm_IceBow" || bullet.damageId == "Charm_IcicleVine" || bullet.damageId == "Charm_FireFeather" ||
             bullet.damageId == "Charm_Planet_Gray" || bullet.damageId == "Charm_Planet_Red" || bullet.damageId == "Charm_Planet_White");

        // ActiveSkill_Bolt, FireBullet and ArrowRain use these installed IDs.
        // Water Bolt also uses Skill_FireBolt. This only widens acquisition in
        // the native enabled-homing branch; it never grants homing itself.
        private static bool IsSpellHoming(Bullet bullet)
        {
            if (bullet.FromType != EDamageFromType.Magic) return false;
            switch (bullet.damageId)
            {
                case "Skill_FireBolt": case "Skill_AirBolt": case "Skill_EarthBolt":
                case "Skill_LightningBolt": case "Skill_IceBolt":
                case "Skill_FrostDagger": case "Skill_ArrowRain": return true;
                default: return false;
            }
        }

        private static bool UsesFriendlyHoming(Bullet bullet) =>
            IsArtifactHoming(bullet) || IsSpellHoming(bullet) || FriendlyFireProjectileIdentity.HasWeaponHoming(bullet);

        // Native mines cache a faction mask at spawn. Keep it for native targets,
        // but consult live PvP policy for players and their companions.
        internal static bool WeaponMineTarget(long nativeMask, UnitAvatar target, BulletMoveModule module)
        {
            var bullet = module.Bullet;
            if (NetworkServer.active && FriendlyFireProjectileIdentity.IsCrossbowMine(bullet) && IsPlayerTeamPair(bullet.NetworkOwner, target))
                return CanAffectTeam(bullet.NetworkOwner, target);
            return CombatManager.ContainsAttackableFaction(nativeMask, target.faction);
        }

        internal static bool HomingTargetAllowed(UnitAvatar source, EDamageFromType type, UnitAvatar target, Bullet bullet) =>
            UsesFriendlyHoming(bullet) ? ItemHitTarget(source, type, target) :
            CombatManager.ContainsAttackableFaction(source.GetHostileFactionLayers(type), target.faction);

        internal static bool HomingTargetLost(UnitAvatar target, Bullet bullet) => target.IsDead ||
            UsesFriendlyHoming(bullet) && FriendlyTargetLost(bullet.NetworkOwner, target);

        private static bool FriendlyTargetLost(UnitAvatar source, UnitAvatar target) =>
            NetworkServer.active && IsPlayerTeamPair(source, target) && !CanAffectTeam(source, target);

        // SearchTarget refreshes only every 1.3s. Drop a now-ineligible player
        // before the cached aim can start another cast between refreshes.
        internal static void BeforeAutoMagic(AutoMagicCaster __instance, ref UnitAvatar ___aimedEnemy)
        {
            if (___aimedEnemy && FriendlyTargetLost(__instance.owner, ___aimedEnemy)) ___aimedEnemy = null;
        }

        // Players' Topdown hitboxes live on their costume, not the avatar root.
        // Only the audited volley lookup gains this fallback; native NPC
        // component lookup and client-side behavior are kept intact.
        internal static UnitAvatar SpellOverlapAvatar(Component collider)
        {
            var avatar = collider.GetComponent<UnitAvatar>();
            if (avatar || !NetworkServer.active) return avatar;
            var hitbox = collider.GetComponent<Hitbox>();
            return hitbox ? hitbox.GetCombatBehaviour(0) as PlayerAvatar : null;
        }

    }
}
