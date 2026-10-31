using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        // Only callers with audited autonomous provenance use this predicate.
        // Live owner positions prevent remote effects/pool reuse extending range.
        private static bool WithinAutonomousArtifactRange(UnitAvatar source, UnitAvatar target)
        {
            if (!NetworkServer.active || !(source is PlayerAvatar) || !(target is PlayerAvatar)) return true;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Enabled || !settings.Balance) return true;
            return source && target && (target.transform.position - source.transform.position).sqrMagnitude <= settings.ArtifactRange.Squared;
        }

        internal static bool AutonomousArtifactTarget(UnitAvatar target, EDamageFromType type, UnitAvatar source) =>
            ItemTarget(target, type, source) && WithinAutonomousArtifactRange(source, target);

        internal static long AutonomousArtifactTargetMask(UnitAvatar target, EDamageFromType type, long mask, UnitAvatar source) =>
            WithinAutonomousArtifactRange(source, target) ? ItemTargetMask(target, type, mask, source) : 0L;

        internal static void ApplyAutonomousArtifactDebuff(UnitAvatar target, CharacterDebuff prefab, UnitAvatar source)
        {
            if (WithinAutonomousArtifactRange(source, target)) target.ApplyDebuff(prefab, source);
        }

        internal static void InvokeDarkCloudAttack(System.Action<UnitAvatar> callback, UnitAvatar target, ComboEffect_DarkCloud cloud)
        {
            if (WithinAutonomousArtifactRange(cloud.Networkavatar, target)) callback(target);
        }

        private static bool AutonomousEmissionAllowed(UnitAvatar source, UnitAvatar target)
        {
            var settings = SessionSettings.FriendlyFireForHit;
            return !NetworkServer.active || !settings.Enabled || !settings.Balance || !(source is PlayerAvatar) || !(target is PlayerAvatar) ||
                AutonomousArtifactTarget(target, EDamageFromType.None, source);
        }

        internal static bool BeforeGuillotineEmission(Charm_Guillotine __instance, UnitAvatar __0) =>
            AutonomousEmissionAllowed(__instance.NetworkAvatar, __0);

        internal static int PruneAutonomousTargets(System.Collections.Generic.List<UnitAvatar> targets,
            System.Predicate<UnitAvatar> nativePredicate, UnitAvatar source)
        {
            int removed = targets.RemoveAll(nativePredicate);
            var settings = SessionSettings.FriendlyFireForHit;
            if (!NetworkServer.active || !settings.Enabled || !settings.Balance || !(source is PlayerAvatar)) return removed;
            for (int i = targets.Count - 1; i >= 0; i--)
                if (targets[i] is PlayerAvatar && !AutonomousArtifactTarget(targets[i], EDamageFromType.None, source))
                { targets.RemoveAt(i); removed++; }
            return removed;
        }

        private static bool ArtifactHitWithinRange(HitContext hit) => hit.Reflection || hit.InDebuffDamage || hit.InBurnExplosion ||
            !ReferenceEquals(hit.Source, hit.Attacker) || !AutonomousArtifactCatalog.HasRangeLimit(hit.Damage) ||
            WithinAutonomousArtifactRange(hit.Source, hit.Victim);

        private static bool AutonomousHomingWithinRange(Bullet bullet, UnitAvatar target) =>
            bullet.FromType != EDamageFromType.None || AutonomousArtifactCatalog.Index(bullet.damageId) < 0 ||
            WithinAutonomousArtifactRange(bullet.NetworkOwner, target);
    }
}
