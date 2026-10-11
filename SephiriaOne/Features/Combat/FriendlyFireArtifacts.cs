using System;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
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
                if (target && target != owner && CanAffectTeam(source, target) && !target.IsDead && !target.canBeTarget.IsFalse() && target.gameObject.activeSelf &&
                    target.currentFloorGuid == owner.currentFloorGuid && (target.transform.position - source.transform.position).sqrMagnitude <= 100f)
                    return true;
            }
            return false;
        }

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
