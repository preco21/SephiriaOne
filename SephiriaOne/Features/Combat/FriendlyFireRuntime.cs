using System;
using System.Text;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class FriendlyFireRuntime
    {
        internal struct HitContext
        {
            internal UnitAvatar Victim;
            internal PlayerAvatar Attacker;
            internal UnitAvatar Source;
            internal DamageInstance Damage;
            internal bool Friendly, InFriendlyChain, Reflection;
            internal int Percent;
        }
        [ThreadStatic] private static HitContext current;
        private static bool warned;
        internal static void Clear() { current = default; warned = false; }

        internal static bool BeforeHit(UnitAvatar __instance, DamageInstance damage, ref EApplyDamageResult __result, out HitContext __state)
        {
            __state = current;
            current = new HitContext { InFriendlyChain = __state.InFriendlyChain };
            if (!NetworkServer.active || damage == null || damage.isSystemDamage ||
                !(damage.origin is UnitAvatar source) || !source || !__instance || source == __instance) return true;
            var attacker = source as PlayerAvatar;
            bool companion = !attacker;
            if (companion)
            {
                attacker = source.NetworkLeader as PlayerAvatar;
                // Companion hostility is restricted to other players. Native
                // monster/NPC damage and target selection remain unchanged.
                if (!attacker || !(__instance is PlayerAvatar)) return true;
            }
            try
            {
                var settings = SessionSettings.FriendlyFireForHit;
                bool reflection = IsNativeReflection(damage);
                // Recheck at impact: even a projectile fired while enabled (or
                // with a broad native mask) cannot hurt the owner or hit players
                // after off/reset. Reject before guard costs and attack procs.
                if (companion && (attacker == __instance || !settings.Enabled))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                // Native reflection uses an all-faction mask. Off must reject
                // team returns before guarding can spend MP, including when
                // policy changes during the original hit's callbacks.
                if (reflection && !settings.Enabled && (__instance is PlayerAvatar || __instance.NetworkLeader is PlayerAvatar))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                if (!settings.Enabled || !Allied(attacker, __instance)) return true;
                // Admit a recognized return only along the exact reverse of the
                // immediate allied hit. Reflected hits cannot reflect again;
                // unrelated procs and enemy-mediated chains remain blocked.
                bool returnHit = reflection && __state.Friendly && !__state.Reflection &&
                    ReferenceEquals(source, __state.Victim) && ReferenceEquals(__instance, __state.Source);
                if (__state.InFriendlyChain && !returnHit || settings.DamagePercent == 0 || damage.damage < 0 ||
                    float.IsNaN(damage.damage) || float.IsInfinity(damage.damage))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                current = new HitContext { Victim = __instance, Attacker = attacker, Source = source, Damage = damage, Friendly = true,
                    InFriendlyChain = true, Reflection = reflection, Percent = settings.DamagePercent };
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
        { current = __state; return __exception; }

        internal static void AfterCompanionRelation(UnitAI_NewBasic __instance, UnitAvatar target, ref ERelationBehaviour __result)
        {
            if (!NetworkServer.active || !(target is PlayerAvatar) || !target) return;
            var avatar = __instance.Avatar;
            if (!avatar || avatar is PlayerAvatar || !(avatar.NetworkLeader is PlayerAvatar owner) || !owner) return;
            try
            {
                var settings = SessionSettings.FriendlyFireForHit;
                // SearchTarget and OnAIUpdate both use this query. No retained
                // target hostility survives off/reset or a change of owner.
                __result = target != owner && settings.Enabled && settings.DamagePercent > 0
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
                var settings = SessionSettings.FriendlyFireForHit;
                if (target == owner || !settings.Enabled || settings.DamagePercent == 0)
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
            !NetworkServer.active || !current.Friendly ||
            !ReferenceEquals(current.Source, __instance) || !ReferenceEquals(current.Victim, target) ||
            !ReferenceEquals(current.Damage, damage) ||
            !(target is PlayerAvatar || target.NetworkLeader is PlayerAvatar);

        internal static bool ProtectLeader(bool native) => native && !current.Friendly;
        internal static bool AllowFaction(bool native) => native || current.Friendly;
        internal static float Scale(float resolved)
        {
            if (!current.Friendly) return resolved;
            // Avoid propagating NaN/infinity/overflow into native HP or SyncVars.
            if (float.IsNaN(resolved) || float.IsInfinity(resolved) || resolved <= 0) return 0;
            return (float)Math.Min((double)resolved * current.Percent / 100d, 2147483520d);
        }

        internal static float Sanitize(float resolved) => !current.Friendly ? resolved :
            float.IsNaN(resolved) || float.IsInfinity(resolved) || resolved < 0 ? 0 : Math.Min(resolved, 2147483520f);

        internal static void BeforeDeath(UnitAvatar __instance, DamageInstance diedFrom, out bool __state) =>
            __state = NetworkServer.active && !__instance.IsDead && current.Friendly &&
                current.Victim == __instance && ReferenceEquals(current.Damage, diedFrom);

        internal static void AfterDeath(UnitAvatar __instance, DamageInstance diedFrom, bool __state)
        {
            if (!__state || !__instance.IsDead) return;
            try
            {
                var dungeon = DungeonManager.Instance;
                if (!NetworkServer.active || !NetworkClient.active || !dungeon || !dungeon.isServer) return;
                // DamageInstance is pooled; death callbacks can reuse it.
                var attacker = current.Attacker;
                if (!attacker) return;
                // Null avatar makes this a system-style chat notice, not a bubble
                // impersonating the killer. The stock RpcChat reaches every guest.
                dungeon.Chat(null, "SephiriaOne", L.F("Friendly fire: {0} killed {1}.", SafeName(attacker.Name), SafeName(__instance.Name)));
            }
            catch (Exception error) { Warn(error); } // A notice cannot interrupt death/respawn.
        }

        internal static string SafeName(string name)
        {
            var text = new StringBuilder(32);
            bool tag = false;
            foreach (char c in name ?? "")
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (!tag && !char.IsControl(c) && text.Length < 32) text.Append(c);
            }
            return text.Length == 0 ? "?" : text.ToString();
        }
        private static void Warn(Exception error)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[SephiriaOne] Friendly-fire hook fallback: " + error);
        }
    }
}
