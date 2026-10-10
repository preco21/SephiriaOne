using System;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        [ThreadStatic] private static UnitAvatar artifactSeeker;
        [ThreadStatic] private static DamageInstance artifactDamage;
        [ThreadStatic] private static UnitAvatar artifactDebuffSource, artifactDebuffTarget;

        // Use the native battle check's 10-unit proximity only for audited
        // offensive consumers. Never write IsInBattle: travel/healing stay native.
        internal static bool ArtifactInBattle(UnitAvatar source)
        {
            if (source.IsInBattle) return true;
            if (!NetworkServer.active || !source || source.IsDead || !CombatManager.Instance || CombatManager.Instance.PeaceMode) return false;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Enabled || !settings.HasDamage) return false;
            var owner = TeamOwner(source);
            if (!owner || string.IsNullOrEmpty(owner.currentFloorGuid)) return false;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner) continue;
                var target = spawner.PlayerAvatar;
                if (target && target != owner && !target.IsDead && !target.canBeTarget.IsFalse() && target.gameObject.activeSelf &&
                    target.currentFloorGuid == owner.currentFloorGuid && (target.transform.position - source.transform.position).sqrMagnitude <= 100f)
                    return true;
            }
            return false;
        }

        // Forward mask consumers differ from ItemTarget's native reverse mask.
        internal static bool ItemHitTarget(UnitAvatar source, EDamageFromType type, UnitAvatar target)
        {
            if (NetworkServer.active && IsPlayerTeamPair(source, target)) return CanAffectTeam(source, target);
            return CombatManager.ContainsAttackableFaction(source.GetHostileFactionLayers(type), target.faction);
        }

        internal static UnitAvatar ArtifactNearestPoint(UnitAvatar source, Vector2 point, float squaredRadius)
        {
            var previous = artifactSeeker;
            artifactSeeker = source;
            try { return PlayerInputController.SearchTargetNearestPoint(source, point, squaredRadius); }
            finally { artifactSeeker = previous; }
        }

        internal static long ArtifactNearestMask(UnitAvatar source, EDamageFromType type, UnitAvatar target)
        {
            if (NetworkServer.active && ReferenceEquals(source, artifactSeeker) && IsPlayerTeamPair(source, target))
                return CanAffectTeam(source, target) ? 1L : 0L;
            return source.GetHostileFactionLayers(type) & RuntimeFactionManager.Instance.FindFactionLayer(target.faction);
        }

        // Verified artifact identities are read on every acquisition/loss check:
        // pooled bullets must not retain eligibility from their previous owner.
        internal static bool IsArtifactHoming(Bullet bullet) => bullet.FromType == EDamageFromType.None &&
            (bullet.damageId == "Charm_PallasCard" || bullet.damageId == "Charm_NearMagicBullet" ||
             bullet.damageId == "Charm_IceBow" || bullet.damageId == "Charm_IcicleVine" || bullet.damageId == "Charm_FireFeather" ||
             bullet.damageId == "Charm_Planet_Gray" || bullet.damageId == "Charm_Planet_Red" || bullet.damageId == "Charm_Planet_White");

        internal static bool ArtifactHomingTarget(UnitAvatar source, EDamageFromType type, UnitAvatar target, Bullet bullet) =>
            IsArtifactHoming(bullet) ? ItemHitTarget(source, type, target) :
            CombatManager.ContainsAttackableFaction(source.GetHostileFactionLayers(type), target.faction);

        internal static bool ArtifactHomingLost(UnitAvatar target, Bullet bullet) => target.IsDead ||
            NetworkServer.active && IsArtifactHoming(bullet) && IsPlayerTeamPair(bullet.NetworkOwner, target) &&
            !CanAffectTeam(bullet.NetworkOwner, target);

        // Only audited native ApplyDamage callsites enter this receipt. Binding
        // the exact damage object prevents unrelated callbacks borrowing it.
        internal static EApplyDamageResult ApplyArtifactDamage(CombatBehaviour target, DamageInstance damage)
        {
            var previous = artifactDamage;
            artifactDamage = damage;
            try { return target.ApplyDamage(damage); }
            finally { artifactDamage = previous; }
        }

        private static bool CanNestArtifact(HitContext parent, UnitAvatar source, DamageInstance damage) =>
            parent.Friendly && !parent.Reflection && !parent.InArtifactDamage && !parent.InDebuffDamage && !parent.InBurnExplosion &&
            ReferenceEquals(damage, artifactDamage) && damage.fromType == EDamageFromType.None &&
            (ReferenceEquals(source, parent.Source) || ReferenceEquals(source, parent.Victim));

        internal static void ApplyArtifactDebuff(UnitAvatar target, CharacterDebuff prefab, UnitAvatar source)
        {
            var previousSource = artifactDebuffSource; var previousTarget = artifactDebuffTarget;
            artifactDebuffSource = source; artifactDebuffTarget = target;
            try { target.ApplyDebuff(prefab, source); }
            finally { artifactDebuffSource = previousSource; artifactDebuffTarget = previousTarget; }
        }

        private static bool IsArtifactDebuffTarget(HitContext parent, UnitAvatar source, UnitAvatar target) =>
            !parent.InArtifactDamage && ReferenceEquals(source, artifactDebuffSource) && ReferenceEquals(target, artifactDebuffTarget);
    }
}
