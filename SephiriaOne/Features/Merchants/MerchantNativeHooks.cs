using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    internal static class MerchantNativeHooks
    {
        private const string Owner = "preco21.SephiriaOne.Merchants";
        private static Harmony harmony;
        internal static bool Available { get; private set; }

        internal static bool ValidateContracts()
        {
            var crime = AccessTools.DeclaredMethod(typeof(DungeonManager), nameof(DungeonManager.NPCDeadCheckServerside),
                new[] { typeof(UnitAI_NewBasic), typeof(DamageInstance) });
            var damage = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "OnDamaged", new[] { typeof(DamageInstance) });
            var death = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "OnDie", new[] { typeof(DamageInstance) });
            if (crime == null || damage == null || death == null || crime.ReturnType != typeof(void) ||
                damage.ReturnType != typeof(void) || !MerchantRooms.ValidateContracts()) return false;
            var crimeCode = PatchProcessor.GetOriginalInstructions(crime).ToList();
            return crimeCode.Any(i => i.operand is FieldInfo f && f.Name == "crimeDebuff" && f.DeclaringType == typeof(DungeonManager)) &&
                crimeCode.Count(i => i.operand is MethodInfo m && m.Name == "ApplyBuff") == 1 &&
                PatchProcessor.GetOriginalInstructions(death).Count(i => Equals(i.operand, crime)) == 1 &&
                PatchProcessor.GetOriginalInstructions(damage).Any(i => i.operand is MethodInfo m &&
                    m.DeclaringType == typeof(RuntimeFactionManager) && m.Name == "SetTempEnemyRelation");
        }

        internal static void Install()
        {
            if (Available) return;
            try
            {
                if (!ValidateContracts()) throw new InvalidOperationException("Native merchant crime/damage/room contracts changed.");
                harmony = new Harmony(Owner);
                harmony.Patch(AccessTools.DeclaredMethod(typeof(DungeonManager), nameof(DungeonManager.NPCDeadCheckServerside)),
                    prefix: new HarmonyMethod(typeof(MerchantNativeHooks), nameof(AllowCrime)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "OnDamaged"),
                    prefix: new HarmonyMethod(typeof(MerchantNativeHooks), nameof(HandleDamage)));
                Available = true;
            }
            catch (Exception error)
            {
                Available = false;
                harmony?.UnpatchAll(Owner); harmony = null;
                Debug.LogWarning("[SephiriaOne] Merchant compatibility checks failed: " + error);
            }
        }

        internal static void Uninstall()
        {
            harmony?.UnpatchAll(Owner); harmony = null; Available = false;
        }

        private static bool AllowCrime(UnitAI_NewBasic npc) => !MerchantRuntime.Owns(npc);

        private static bool HandleDamage(UnitAI_NewBasic __instance, DamageInstance damage)
        {
            if (!MerchantRuntime.Owns(__instance)) return true;
            // Native OnDamaged can make an entire faction temporarily hostile. Our actor
            // already belongs to a hostile faction; it must never edit shared relations.
            try
            {
                UnitAvatar avatar = __instance.Avatar;
                if (avatar && damage != null && damage.origin is UnitAvatar attacker && attacker &&
                    RuntimeFactionManager.Instance && RuntimeFactionManager.Instance.GetRelationBehaviour(
                        avatar.faction, attacker.faction, avatar.attackableTargetSelector) == ERelationBehaviour.Hostile &&
                    !avatar.IsInBattle)
                    __instance.SetTarget(attacker);
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Extra merchant targeting failed: " + error.Message); }
            return false;
        }
    }
}
