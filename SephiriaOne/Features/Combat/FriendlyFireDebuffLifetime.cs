using System;
using System.Runtime.CompilerServices;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal sealed class DebuffOrigin
        {
            internal UnitAvatar Source, Target;
            internal PlayerAvatar Owner;
            internal bool Valid => Source && Target && Owner &&
                (Source is PlayerAvatar || ReferenceEquals(Source.NetworkLeader, Owner) || Source.IsDead && !Source.NetworkLeader);
        }
        private static ConditionalWeakTable<CharacterDebuff, DebuffOrigin> debuffOrigins = new ConditionalWeakTable<CharacterDebuff, DebuffOrigin>();
        [ThreadStatic] private static DebuffOrigin activeDebuff;

        private static void ClearDebuffOrigins()
        { activeDebuff = null; debuffOrigins = new ConditionalWeakTable<CharacterDebuff, DebuffOrigin>(); }

        private static PlayerAvatar TeamOwner(UnitAvatar source)
        {
            if (source is PlayerAvatar player) return player;
            if (source.NetworkLeader is PlayerAvatar owner && owner) return owner;
            return activeDebuff != null && activeDebuff.Valid && ReferenceEquals(activeDebuff.Source, source)
                ? activeDebuff.Owner : null;
        }

        internal static void BeforeDebuffSpawn(UnitAvatar attacker, UnitAvatar target, out DebuffOrigin __state)
        {
            __state = null;
            if (!NetworkServer.active || !IsPlayerTeamPair(attacker, target)) return;
            __state = new DebuffOrigin { Source = attacker, Target = target, Owner = TeamOwner(attacker) };
        }

        internal static void AfterDebuffSpawn(CharacterDebuff __instance, DebuffOrigin __state)
        {
            debuffOrigins.Remove(__instance);
            if (__state != null && ReferenceEquals(__instance.NetworkAttacker, __state.Source) &&
                ReferenceEquals(__instance.NetworkTarget, __state.Target)) debuffOrigins.Add(__instance, __state);
        }

        internal static void BeforeDebuffOperation(CharacterDebuff __instance, out DebuffOrigin __state)
        {
            __state = activeDebuff;
            activeDebuff = null;
            if (NetworkServer.active) debuffOrigins.TryGetValue(__instance, out activeDebuff);
        }

        internal static bool BeforeDebuffUpdate(CharacterDebuff __instance, out DebuffOrigin __state)
        {
            BeforeDebuffOperation(__instance, out __state);
            if (activeDebuff == null) return true;
            if (activeDebuff.Valid && ReferenceEquals(__instance.NetworkAttacker, activeDebuff.Source) &&
                ReferenceEquals(__instance.NetworkTarget, activeDebuff.Target)) return true;
            // The old caster/owner cannot authorize a lingering effect after
            // disconnect or owner transfer. Native destruction removes status
            // and replicated FX. Its electric expiry hit stays in this scope.
            if (!__instance.IsEndBuff) __instance.Destroy();
            return false;
        }

        internal static Exception AfterDebuffOperation(Exception __exception, DebuffOrigin __state)
        { activeDebuff = __state; return __exception; }

        private static bool BlockOrphanedDebuffHit(UnitAvatar target, DamageInstance damage) =>
            activeDebuff != null && ReferenceEquals(activeDebuff.Target, target) && damage != null &&
            damage.damageType == EDamageType.ElementalEffectDamage && damage.fromType == EDamageFromType.None &&
            (!activeDebuff.Valid || !ReferenceEquals(activeDebuff.Source, damage.origin));
    }
}
