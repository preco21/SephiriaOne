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
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(PlayerInputController), "SearchTargetNearestPoint"), nameof(RewriteNearestMask)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(DaggerGrowthBullet), "HitCheck"), nameof(RewriteDagger)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(Bullet), "Update"), nameof(RewriteHoming)));
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
            ReplaceCall(instructions, AccessTools.PropertyGetter(typeof(UnitAvatar), "IsInBattle"), nameof(FriendlyFireRuntime.ArtifactInBattle), 1);

        internal static IEnumerable<CodeInstruction> RewriteNearestCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
            ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(PlayerInputController), "SearchTargetNearestPoint"),
                nameof(FriendlyFireRuntime.ArtifactNearestPoint), __originalMethod.DeclaringType == typeof(Charm_GuardCounter) ? 1 : 2);

        internal static IEnumerable<CodeInstruction> RewriteDamageProc(IEnumerable<CodeInstruction> instructions) =>
            ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(CombatBehaviour), "ApplyDamage", new[] { typeof(DamageInstance) }),
                nameof(FriendlyFireRuntime.ApplyArtifactDamage), 1);

        internal static IEnumerable<CodeInstruction> RewriteDebuffProc(IEnumerable<CodeInstruction> instructions) =>
            ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDebuff", new[] { typeof(CharacterDebuff), typeof(UnitAvatar) }),
                nameof(FriendlyFireRuntime.ApplyArtifactDebuff), 1);

        private static List<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> instructions, MethodInfo original, string helper, int count)
        {
            if (original == null) throw new InvalidOperationException("Native artifact call missing: " + helper);
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var calls = code.Where(i => i.Calls(original)).ToArray();
            if (calls.Length != count) throw new InvalidOperationException("Native artifact call contract changed: " + helper);
            foreach (var call in calls) { call.opcode = OpCodes.Call; call.operand = AccessTools.Method(typeof(FriendlyFireRuntime), helper); }
            return code;
        }

        internal static IEnumerable<CodeInstruction> RewriteDagger(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.DirectAttack, nameof(FriendlyFireRuntime.ItemHitTarget));

        internal static IEnumerable<CodeInstruction> RewriteHoming(IEnumerable<CodeInstruction> instructions)
        {
            var code = FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.None, nameof(FriendlyFireRuntime.ArtifactHomingTarget));
            int filter = code.FindIndex(i => i.Calls(AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ArtifactHomingTarget))));
            code.Insert(filter, new CodeInstruction(OpCodes.Ldarg_0));
            var reads = Enumerable.Range(1, code.Count - 1).Where(i => code[i].LoadsField(AccessTools.Field(typeof(UnitAvatar), "IsDead")) &&
                code[i - 1].Calls(AccessTools.PropertyGetter(typeof(Bullet), "HomingTarget"))).ToArray();
            if (reads.Length != 1) throw new InvalidOperationException("Native artifact homing target lifetime changed.");
            int read = reads[0];
            code[read].opcode = OpCodes.Ldarg_0; code[read].operand = null;
            code.Insert(read + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ArtifactHomingLost))));
            return code;
        }

        internal static IEnumerable<CodeInstruction> RewriteNearestMask(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var masks = Enumerable.Range(1, code.Count - 1).Where(i =>
                code[i].Calls(AccessTools.Method(typeof(UnitAvatar), "GetHostileFactionLayers"))).ToArray();
            if (masks.Length != 1) throw new InvalidOperationException("Native nearest-point mask changed.");
            int mask = masks[0];
            // source, None, mask, faction-manager, target, faction, layer, AND
            if (mask + 5 >= code.Count || !code[mask - 1].LoadsConstant((int)EDamageFromType.None) ||
                !code[mask + 1].Calls(AccessTools.PropertyGetter(typeof(RuntimeFactionManager), "Instance")) ||
                !code[mask + 2].IsLdloc() || !code[mask + 3].LoadsField(AccessTools.Field(typeof(UnitAvatar), "faction")) ||
                !code[mask + 4].Calls(AccessTools.Method(typeof(RuntimeFactionManager), "FindFactionLayer")) || code[mask + 5].opcode != OpCodes.And ||
                code.Skip(mask + 1).Take(5).Any(i => i.labels.Count != 0 || i.blocks.Count != 0))
                throw new InvalidOperationException("Native nearest-point source/target pairing changed.");
            foreach (int offset in new[] { 0, 1, 3, 4 }) { code[mask + offset].opcode = OpCodes.Nop; code[mask + offset].operand = null; }
            code[mask + 5].opcode = OpCodes.Call;
            code[mask + 5].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ArtifactNearestMask));
            return code;
        }
    }
}
