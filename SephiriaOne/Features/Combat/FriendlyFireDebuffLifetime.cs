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
            internal PlayerAvatar Owner, TargetOwner;
            internal FriendlyFireParticipants.Receipt OwnerReceipt, TargetReceipt;
            internal bool Valid => Source && Target && Owner &&
                FriendlyFireParticipants.IsCurrent(OwnerReceipt) &&
                (TargetReceipt == null || FriendlyFireParticipants.IsCurrent(TargetReceipt)) &&
                OwnerMatches(Source, Owner) && (TargetOwner == null || OwnerMatches(Target, TargetOwner));
        }
        private sealed class CompanionOwner
        {
            internal PlayerAvatar Owner;
            internal FriendlyFireParticipants.Receipt Receipt;
        }
        private static ConditionalWeakTable<UnitAvatar, CompanionOwner> companionOwners = new ConditionalWeakTable<UnitAvatar, CompanionOwner>();
        private static ConditionalWeakTable<CharacterDebuff, DebuffOrigin> debuffOrigins = new ConditionalWeakTable<CharacterDebuff, DebuffOrigin>();
        [ThreadStatic] private static DebuffOrigin activeDebuff;

        private static void ClearDebuffOrigins()
        {
            activeDebuff = null; debuffOrigins = new ConditionalWeakTable<CharacterDebuff, DebuffOrigin>();
            companionOwners = new ConditionalWeakTable<UnitAvatar, CompanionOwner>();
        }

        private static bool OwnerMatches(UnitAvatar source, PlayerAvatar owner) => ReferenceEquals(source, owner) ||
            ReferenceEquals(source.NetworkLeader, owner) || source.IsDead && !source.NetworkLeader;

        private static PlayerAvatar TeamOwner(UnitAvatar source)
        {
            if (ReferenceEquals(source, null)) return null;
            if (source is PlayerAvatar player) return player;
            if (source && source.NetworkLeader is PlayerAvatar owner)
            {
                var receipt = FriendlyFireParticipants.Capture(owner);
                if (!companionOwners.TryGetValue(source, out var previous) || !ReferenceEquals(previous.Owner, owner))
                {
                    companionOwners.Remove(source);
                    companionOwners.Add(source, new CompanionOwner { Owner = owner, Receipt = receipt });
                }
                else if (!source.IsDead && receipt != null) previous.Receipt = receipt;
                return owner;
            }
            if (activeDebuff != null && ReferenceEquals(activeDebuff.Source, source)) return activeDebuff.Owner;
            // Native Die clears NetworkLeader before an already-fired projectile
            // lands. Preserve classification, including a stale owner which must
            // be rejected, rather than treating that projectile as NPC damage.
            return companionOwners.TryGetValue(source, out var known) ? known.Owner : null;
        }

        private static bool OwnerCurrent(UnitAvatar source, PlayerAvatar owner) => source && owner &&
            OwnerMatches(source, owner) && (source is PlayerAvatar ||
            companionOwners.TryGetValue(source, out var known) && ReferenceEquals(known.Owner, owner) && FriendlyFireParticipants.IsCurrent(known.Receipt));

        private static void RememberCompanionOwner(UnitAvatar source)
        { if (source && !(source is PlayerAvatar)) TeamOwner(source); }

        internal static void BeforeDebuffSpawn(UnitAvatar attacker, UnitAvatar target, out DebuffOrigin __state)
        {
            __state = null;
            if (!NetworkServer.active || !IsPlayerTeamPair(attacker, target)) return;
            var owner = TeamOwner(attacker); var targetOwner = TeamOwner(target);
            __state = new DebuffOrigin { Source = attacker, Target = target, Owner = owner, TargetOwner = targetOwner,
                OwnerReceipt = FriendlyFireParticipants.Capture(owner), TargetReceipt = FriendlyFireParticipants.Capture(targetOwner) };
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
