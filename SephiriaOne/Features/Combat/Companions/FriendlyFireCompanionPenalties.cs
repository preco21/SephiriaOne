using System;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        // Capture comes from the admitted hit, before native death callbacks can
        // clear NetworkLeader, change policy or reuse damage.origin. Never grant
        // an enduring exemption to an avatar, faction or pooled damage instance.
        private static bool AdmittedCompanionHit(UnitAvatar avatar, DamageInstance damage) =>
            NetworkServer.active && current.Friendly && current.VictimReceipt != null &&
            avatar && !(avatar is PlayerAvatar) && ReferenceEquals(current.Victim, avatar) &&
            ReferenceEquals(current.Damage, damage);

        internal static bool BeforeCompanionCrime(UnitAI_NewBasic npc, DamageInstance damageInstance) =>
            !npc || !npc.Avatar || !npc.Avatar.IsDead || !AdmittedCompanionHit(npc.Avatar, damageInstance);

        internal static void CompanionTempHostility(RuntimeFactionManager manager, string lhs, string rhs,
            UnitAI_NewBasic npc, DamageInstance damage)
        {
            if (!AdmittedCompanionHit(npc.Avatar, damage)) manager.SetTempEnemyRelation(lhs, rhs);
        }

        internal static void CompanionWandererHostility(DungeonManager dungeon, UnitAI_NewBasic npc, DamageInstance damage)
        {
            if (!AdmittedCompanionHit(npc.Avatar, damage)) dungeon.NotifyWanderersHostile();
        }
    }
}
