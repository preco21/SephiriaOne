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
            internal DamageInstance Damage;
            internal bool Friendly, InFriendlyChain;
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
                !(damage.origin is PlayerAvatar attacker) || !attacker || !__instance || attacker == __instance) return true;
            try
            {
                var settings = SessionSettings.FriendlyFireForHit;
                if (!settings.Enabled || !Allied(attacker, __instance)) return true;
                // Native thorns can fire before hit invulnerability is established.
                // Block nested ally hits, including those reached via an enemy proc.
                if (__state.InFriendlyChain || settings.DamagePercent == 0 || damage.damage < 0 ||
                    float.IsNaN(damage.damage) || float.IsInfinity(damage.damage))
                { __result = EApplyDamageResult.Fail_Absolute; return false; }
                current = new HitContext { Victim = __instance, Attacker = attacker, Damage = damage, Friendly = true,
                    InFriendlyChain = true, Percent = settings.DamagePercent };
            }
            catch (Exception error) { Warn(error); }
            return true;
        }

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
