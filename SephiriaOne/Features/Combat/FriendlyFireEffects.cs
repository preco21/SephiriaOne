using System;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal struct DebuffScope
        {
            internal UnitAvatar Source, Target;
        }
        [ThreadStatic] private static DebuffScope debuffScope;

        private static bool IsPlayerTeamPair(UnitAvatar source, UnitAvatar target) => source && target && source != target &&
            (source is PlayerAvatar ? target is PlayerAvatar || target.NetworkLeader is PlayerAvatar :
                TeamOwner(source) && target is PlayerAvatar);

        private static bool CanAffectTeam(UnitAvatar source, UnitAvatar target)
        {
            var settings = SessionSettings.FriendlyFireForHit;
            return settings.Enabled && settings.DamagePercent > 0 &&
                (source is PlayerAvatar || TeamOwner(source) != target);
        }

        // These helpers replace only audited offensive item target filters.
        // Factions, healing/buffs, AI and unrelated spell searches stay native.
        internal static bool ItemTarget(UnitAvatar target, EDamageFromType type, UnitAvatar source)
        {
            if (NetworkServer.active && IsPlayerTeamPair(source, target)) return CanAffectTeam(source, target);
            return CombatManager.ContainsAttackableFaction(target.GetHostileFactionLayers(type), source.faction);
        }

        internal static long ItemTargetMask(UnitAvatar target, EDamageFromType type, long mask, UnitAvatar source)
        {
            if (NetworkServer.active && IsPlayerTeamPair(source, target)) return CanAffectTeam(source, target) ? 1L : 0L;
            return target.GetHostileFactionLayers(type) & mask;
        }

        internal static bool BeforeDebuff(UnitAvatar __instance, UnitAvatar caster, out DebuffScope __state)
        {
            __state = debuffScope;
            debuffScope = default; // Unrelated nested applications cannot borrow admission.
            if (!NetworkServer.active || !IsPlayerTeamPair(caster, __instance)) return true;
            if (!CanAffectTeam(caster, __instance)) return false;
            debuffScope = new DebuffScope { Source = caster, Target = __instance };
            return true;
        }

        internal static Exception AfterDebuff(Exception __exception, DebuffScope __state)
        { debuffScope = __state; return __exception; }

        private static bool CanNestDebuff(HitContext parent, UnitAvatar source, UnitAvatar target, DamageInstance damage) =>
            parent.Friendly && !parent.Reflection && !parent.InDebuffDamage &&
            damage.damageType == EDamageType.ElementalEffectDamage && damage.fromType == EDamageFromType.None &&
            ReferenceEquals(source, parent.Source) && (ReferenceEquals(target, parent.Victim) || IsArtifactDebuffTarget(parent, source, target)) &&
            ReferenceEquals(source, debuffScope.Source) && ReferenceEquals(target, debuffScope.Target);

        private static bool IsBurnExplosion(DamageInstance damage) => damage.id == "Charm_BurnExplosion" &&
            damage.damageType == EDamageType.Projectile && damage.fromType == EDamageFromType.None;
    }
}
