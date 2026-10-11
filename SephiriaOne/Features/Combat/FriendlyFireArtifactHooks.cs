using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    // Registries describe offensive native consumers, never global faction or
    // battle state. Every rewrite is checked against installed-game contracts.
    internal static class FriendlyFireArtifactHooks
    {
        internal static IEnumerable<MethodInfo> BattleReaders()
        {
            yield return AccessTools.DeclaredMethod(typeof(Charm_IceBat), "OnUpdate");
            yield return AccessTools.DeclaredMethod(typeof(Charm_ElectricEarring), "OnUpdate");
            yield return AccessTools.DeclaredMethod(typeof(Charm_FlameGround_Meteor), "OnUpdate");
            yield return AccessTools.DeclaredMethod(typeof(Charm_IceSpear), "OnUpdate");
            yield return AccessTools.DeclaredMethod(typeof(ComboEffect_DarkCloud), "Update");
            yield return AccessTools.DeclaredMethod(typeof(GreenBat), "Update");
        }
        internal static IEnumerable<MethodInfo> NearestCallers()
        {
            yield return AccessTools.DeclaredMethod(typeof(Charm_GuardCounter), "FireRipostelaser");
            yield return AccessTools.DeclaredMethod(typeof(Charm_RockElephant), "SpawnFlag");
            yield return AccessTools.DeclaredMethod(typeof(Charm_IceBow), "FireCastingServer");
            yield return FriendlyFireEffectHooks.Iterator(typeof(Charm_IceBow), "FireCoroutine");
        }
        internal static IEnumerable<MethodInfo> DamageProcs()
        {
            yield return AccessTools.DeclaredMethod(typeof(Charm_FrostiumRing), "HandleAttackUnit");
            yield return AccessTools.DeclaredMethod(typeof(Charm_TheTyphoonSheetmusic), "HandleAttackUnit");
            yield return AccessTools.DeclaredMethod(typeof(Charm_Reddew), "SummonDueFromVictim");
            yield return AccessTools.DeclaredMethod(typeof(Charm_TuningForks), "CreateAttack");
            yield return AccessTools.DeclaredMethod(typeof(Charm_EchoOfTheGlacier), "CreateFrostbite");
            yield return AccessTools.DeclaredMethod(typeof(ComboEffect_DarkCloud), "FireLightning");
        }

        internal static void Install(Harmony harmony)
        {
            var patches = new List<KeyValuePair<MethodInfo, string>>();
            foreach (var m in BattleReaders()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteBattle)));
            foreach (var m in NearestCallers()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteNearestCalls)));
            foreach (var m in DamageProcs()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteDamageProc)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(DaggerGrowthBullet), "HitCheck"), nameof(RewriteDagger)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(Charm_AttackChim), "HandleAddedDebuffOnTarget"), nameof(RewriteDebuffProc)));
            foreach (var patch in patches)
            {
                if (patch.Key == null) throw new InvalidOperationException("Native offensive artifact entry point changed: " + patch.Value);
                var rewrite = AccessTools.DeclaredMethod(typeof(FriendlyFireArtifactHooks), patch.Value);
                var code = PatchProcessor.GetOriginalInstructions(patch.Key);
                var args = patch.Value == nameof(RewriteNearestCalls) ? new object[] { code, patch.Key } : new object[] { code };
                ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, args)).ToList();
            }
            foreach (var patch in patches) harmony.Patch(patch.Key, transpiler: new HarmonyMethod(typeof(FriendlyFireArtifactHooks), patch.Value));
        }

        internal static IEnumerable<CodeInstruction> RewriteBattle(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireTargetingHooks.ReplaceCall(instructions, AccessTools.PropertyGetter(typeof(UnitAvatar), "IsInBattle"), nameof(FriendlyFireRuntime.ArtifactInBattle), 1);

        internal static IEnumerable<CodeInstruction> RewriteNearestCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
            FriendlyFireTargetingHooks.ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(PlayerInputController), "SearchTargetNearestPoint"),
                nameof(FriendlyFireRuntime.OffensiveNearestPoint), __originalMethod.DeclaringType == typeof(Charm_GuardCounter) ? 1 : 2);

        internal static IEnumerable<CodeInstruction> RewriteDamageProc(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireTargetingHooks.ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(CombatBehaviour), "ApplyDamage", new[] { typeof(DamageInstance) }),
                nameof(FriendlyFireRuntime.ApplyArtifactDamage), 1);

        internal static IEnumerable<CodeInstruction> RewriteDebuffProc(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireTargetingHooks.ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDebuff", new[] { typeof(CharacterDebuff), typeof(UnitAvatar) }),
                nameof(FriendlyFireRuntime.ApplyArtifactDebuff), 1);

        internal static IEnumerable<CodeInstruction> RewriteDagger(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.DirectAttack, nameof(FriendlyFireRuntime.ItemHitTarget));

    }
}
