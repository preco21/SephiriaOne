using System;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal struct HitContext
        {
            internal UnitAvatar Victim;
            internal PlayerAvatar Attacker;
            internal UnitAvatar Source;
            internal DamageInstance Damage;
            internal bool Friendly, InFriendlyChain, Reflection;
            internal bool InDebuffDamage, InBurnExplosion, InArtifactDamage;
            internal double DamageScale;
            internal long KdaEpoch;
            internal long ParticipantEpoch;
            internal FriendlyFireParticipants.Receipt AttackerReceipt, VictimReceipt;
            internal BalanceWindow BudgetWindow;
            internal long BudgetGeneration;
            internal double BudgetCharged, BudgetMaxHp, BudgetAccounted, BudgetIncomingScale;
            internal int ParryMpBefore;
            internal bool ActiveShieldSpend, ShieldAbsorbed, DefenseMpSpent;
            internal bool PerfectGuard;
            internal int PerfectGuardMpCost;
        }
        [ThreadStatic] private static HitContext current;
        private static bool warned;
        internal static void Clear() { current = default; debuffScope = default; ClearDebuffOrigins(); ClearDarkCloudWindows(); ClearBalanceBudgets(); FriendlyFireKda.Reset(); FriendlyFireDeathVisuals.Reset(); FriendlyFireDeathAudio.Reset(); NativePlayerChat.Reset(); warned = false; }

        internal static bool BeforeHit(UnitAvatar __instance, DamageInstance damage, ref EApplyDamageResult __result, out HitContext __state)
        {
            __state = current;
            current = new HitContext { InFriendlyChain = __state.InFriendlyChain,
                InDebuffDamage = __state.InDebuffDamage, InBurnExplosion = __state.InBurnExplosion, InArtifactDamage = __state.InArtifactDamage };
            if (NetworkServer.active && BlockOrphanedDebuffHit(__instance, damage))
            { __result = EApplyDamageResult.Fail_Absolute; return false; }
            if (!NetworkServer.active || damage == null || damage.isSystemDamage ||
                !(damage.origin is UnitAvatar source) || !__instance || source == __instance) return true;
            // Unity fake-null must not turn a recognizable player or previously
            // owned companion into an unclassified native team-damage source.
            // Already-fired attacks against native enemies retain their path.
            if (!source)
            {
                if (IsPlayerTeamPair(source, __instance))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                return true;
            }
            var attacker = source as PlayerAvatar;
            bool companion = !attacker;
            if (companion)
            {
                attacker = TeamOwner(source);
                // Companion hostility is restricted to other players. Native
                // monster/NPC damage and target selection remain unchanged.
                if (ReferenceEquals(attacker, null) || !(__instance is PlayerAvatar)) return true;
            }
            try
            {
                var settings = SessionSettings.FriendlyFireForHit;
                FriendlyFireKda.SetEnabled(CombatMatchRuntime.ScoresEnabled(settings.Enabled));
                bool reflection = IsNativeReflection(damage);
                // Native debuff ticks use an all-faction mask and bypass the
                // normal player veto. Recheck policy before every team impact.
                if (IsPlayerTeamPair(source, __instance) && !CanAffectTeam(source, __instance))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                // Recheck at impact: even a projectile fired while enabled (or
                // with a broad native mask) cannot hurt the owner or hit players
                // after off/reset. Reject before guard costs and attack procs.
                if (companion && attacker == __instance)
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                if (!settings.Enabled || !Allied(attacker, __instance)) return true;
                if (!CanAffectTeam(source, __instance))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                if (__state.InFriendlyChain && !HitPermitted(__state))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                // Admit a recognized return only along the exact reverse of the
                // immediate allied hit. Reflected hits cannot reflect again;
                // unrelated procs and enemy-mediated chains remain blocked.
                bool returnHit = reflection && __state.Friendly && !__state.Reflection &&
                    ReferenceEquals(source, __state.Victim) && ReferenceEquals(__instance, __state.Source);
                bool debuffHit = CanNestDebuff(__state, source, __instance, damage);
                bool artifactHit = CanNestArtifact(__state, source, damage);
                bool burnExplosion = IsBurnExplosion(damage);
                bool burnProc = burnExplosion && __state.Friendly && !__state.Reflection && !__state.InBurnExplosion &&
                    __state.Victim && __state.Victim.IsDead && ReferenceEquals(source, __state.Source);
                if (__state.InFriendlyChain && !returnHit && !debuffHit && !burnProc && !artifactHit || !settings.HasDamage || damage.damage < 0 ||
                    float.IsNaN(damage.damage) || float.IsInfinity(damage.damage))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                current = new HitContext { Victim = __instance, Attacker = attacker, Source = source, Damage = damage, Friendly = true,
                    InFriendlyChain = true, Reflection = reflection, DamageScale = settings.DamageScale, KdaEpoch = FriendlyFireKda.Epoch,
                    InDebuffDamage = __state.InDebuffDamage || debuffHit, InBurnExplosion = __state.InBurnExplosion || burnExplosion,
                    InArtifactDamage = __state.InArtifactDamage || ReferenceEquals(damage, artifactDamage),
                    AttackerReceipt = FriendlyFireParticipants.Capture(attacker), VictimReceipt = FriendlyFireParticipants.Capture(TeamOwner(__instance)),
                    ParticipantEpoch = FriendlyFireParticipants.Epoch };
                if (settings.Balance && !AutonomousArtifactReady(settings.ArtifactRate))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
            }
            catch (Exception error) { Warn(error); }
            return true;
        }

        // Audited native direct-return effects. Keep this list narrow: an
        // arbitrary on-hit proc is not permission to re-enter allied damage.
        private static bool IsNativeReflection(DamageInstance damage) => damage.fromType == EDamageFromType.None &&
            (damage.id == "Ability_Thorns" || damage.id == "Weapon_Reflect" || damage.id == "Charm_VenomSporePouch");

        private static bool Allied(PlayerAvatar attacker, UnitAvatar victim)
        {
            if (victim is PlayerAvatar || victim.NetworkLeader is PlayerAvatar || attacker.NetworkLeader == victim) return true;
            if (string.IsNullOrEmpty(attacker.faction) || string.IsNullOrEmpty(victim.faction)) return false;
            return attacker.faction == victim.faction || RuntimeFactionManager.Instance &&
                RuntimeFactionManager.Instance.GetRelationBehaviour(attacker.faction, victim.faction,
                    attacker.attackableTargetSelector) == ERelationBehaviour.Friendly;
        }

        internal static Exception AfterHit(Exception __exception, HitContext __state)
        {
            try { if (__exception == null) { FinishDefenseMp(); FinishBalancedDamage(); } }
            finally { current = __state; }
            return __exception;
        }

        // Admission protects an already-delivered lethal hit through native Die.
        // Permission for further writes/attribution must instead stay current
        // across arbitrary native attack and damage callbacks.
        private static bool CurrentHitPermitted() => HitPermitted(current);
        internal static bool ContinueHit() => !current.Friendly || CurrentHitPermitted();
        private static bool HitPermitted(HitContext hit) => hit.Friendly &&
            FriendlyFireParticipants.IsCurrent(hit.AttackerReceipt) &&
            (hit.VictimReceipt == null || FriendlyFireParticipants.IsCurrent(hit.VictimReceipt)) &&
            CanAffectTeam(hit.Source, hit.Victim) && hit.ParticipantEpoch == FriendlyFireParticipants.Epoch;

        // The native all-dead callback emits an irreversible game-over RPC.
        // Keep only this exact friendly-fire death recoverable; no global
        // game-over flag or exemption survives the active damage scope.
        internal static bool BeforeGameOverCheck(PlayerSpawner __instance, DamageInstance damage) =>
            !NetworkServer.active || !ReviveAllFeature.Available || !CombatMatchRuntime.ProtectDeath(__instance.PlayerAvatar) &&
            (!current.Friendly || !(current.Victim is PlayerAvatar victim) ||
            !ReferenceEquals(__instance.PlayerAvatar, victim) || !victim.IsDead ||
            !ReferenceEquals(current.Damage, damage));

        internal static void AfterReceivedDamage(UnitAvatar __instance, float damage)
        {
            if (!NetworkServer.active || !CurrentHitPermitted() || !ReferenceEquals(current.Victim, __instance) ||
                !(__instance is PlayerAvatar victim) || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            if (current.BudgetWindow != null) current.BudgetAccounted += damage;
            try { FriendlyFireKda.Damage(current.Attacker, victim, current.KdaEpoch); }
            catch (Exception error) { Warn(error); }
        }

        internal static void AfterCompanionRelation(UnitAI_NewBasic __instance, UnitAvatar target, ref ERelationBehaviour __result)
        {
            if (!NetworkServer.active || !(target is PlayerAvatar) || !target) return;
            var avatar = __instance.Avatar;
            if (!avatar || avatar is PlayerAvatar || !(avatar.NetworkLeader is PlayerAvatar owner) || !owner) return;
            try
            {
                // SearchTarget and OnAIUpdate both use this query. No retained
                // target hostility survives off/reset or a change of owner.
                __result = CanAffectTeam(avatar, target)
                    ? ERelationBehaviour.Hostile : ERelationBehaviour.Friendly;
            }
            catch (Exception error) { Warn(error); }
        }

        internal static void BeforeCompanionUpdate(UnitAI_NewBasic __instance, bool ___isInBattleActiveByAI)
        {
            if (!NetworkServer.active || !___isInBattleActiveByAI || !(__instance.CurrentTarget is PlayerAvatar target)) return;
            var avatar = __instance.Avatar;
            if (!avatar || avatar is PlayerAvatar || !(avatar.NetworkLeader is PlayerAvatar owner) || !owner) return;
            try
            {
                if (!CanAffectTeam(avatar, target))
                    // Native target loss releases held attacks (including archers
                    // whose follow handler alone would leave the trigger held).
                    // Use native battle state: prior relation isn't populated
                    // until the first search, but retaliation can attack sooner.
                    __instance.SetTarget(null);
            }
            catch (Exception error) { Warn(error); }
        }

        // PlayerAvatar has a second team-protection callback after the faction
        // admission check. Skip only that callback for the exact active team hit;
        // other subscribers may still block it. Ordinary NPC safe-mode/crime
        // handling must keep running, even when the initial faction gate passed.
        internal static bool BeforePlayerAttack(PlayerAvatar __instance, UnitAvatar target, DamageInstance damage) =>
            !NetworkServer.active || !CurrentHitPermitted() ||
            !ReferenceEquals(current.Source, __instance) || !ReferenceEquals(current.Victim, target) ||
            !ReferenceEquals(current.Damage, damage) ||
            !(target is PlayerAvatar || target.NetworkLeader is PlayerAvatar);

        internal static bool ProtectLeader(bool native) => native && !CurrentHitPermitted();
        internal static bool AllowFaction(bool native) => native || CurrentHitPermitted();
        internal static float Scale(float resolved)
        {
            if (!current.Friendly) return resolved;
            if (!CurrentHitPermitted()) return 0;
            // Avoid propagating NaN/infinity/overflow into native HP or SyncVars.
            if (float.IsNaN(resolved) || float.IsInfinity(resolved) || resolved <= 0) return 0;
            double scaled = resolved * current.DamageScale;
            if (current.Victim is PlayerAvatar victim)
            {
                var settings = SessionSettings.FriendlyFireForHit;
                double incoming = CombatMatchRuntime.IncomingRoleScale(victim);
                if (incoming <= 0) return 0;
                if (settings.Balance)
                {
                    double maxHp = victim.MaxHp;
                    double refill = settings.RefillScale * CombatMatchRuntime.RefillRoleScale(current.Attacker);
                    scaled = LimitBalancedDamage(FriendlyFireBalance.Soften(scaled, maxHp), maxHp, refill, settings.RefillScale, incoming, settings.HitFrequency, settings.DotIntervals, settings.Rapid, settings.ArtifactRate);
                }
                // Reduce the damage native code can actually accept, including
                // extreme unbalanced hits that would otherwise reach this cap.
                return PositiveScaledDamage(Math.Min(scaled, 2147483520d) * incoming);
            }
            return PositiveScaledDamage(scaled);
        }

        // Preserve native vetoes/zero damage and apply the minimum only once,
        // before shields. Later absorption must never be promoted back to a hit.
        private static float PositiveScaledDamage(double damage) =>
            double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0 ? 0 :
            (float)Math.Max(1d, Math.Min(damage, 2147483520d));

        internal static float Sanitize(float resolved) => !current.Friendly ? resolved :
            !CurrentHitPermitted() || float.IsNaN(resolved) || float.IsInfinity(resolved) || resolved < 0 ? 0 : Math.Min(resolved, 2147483520f);

        private static void Warn(Exception error)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[SephiriaOne] Friendly-fire hook fallback: " + error);
        }
    }
}
