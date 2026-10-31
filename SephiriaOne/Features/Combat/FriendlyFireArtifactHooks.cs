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

        internal static IEnumerable<MethodInfo> AutonomousDebuffCallers()
        {
            yield return AccessTools.DeclaredMethod(typeof(Charm_EchoOfTheGlacier), "CreateFrostbite");
            yield return AccessTools.DeclaredMethod(typeof(Charm_FireChakram), "OnUpdate");
            yield return AccessTools.DeclaredMethod(typeof(Charm_ElectricEarring), "HandleAttack");
            yield return AccessTools.DeclaredMethod(typeof(Charm_FlameGround_Meteor), "HandleAttack");
            yield return AccessTools.DeclaredMethod(typeof(Charm_FireFeather), "OnAttackFeather");
        }

        internal static void Install(Harmony harmony)
        {
            var cloud = AccessTools.DeclaredMethod(typeof(ComboEffect_DarkCloud), "FireLightning", new[] { typeof(UnitAvatar), typeof(int) });
            if (cloud == null || cloud.IsStatic || cloud.ReturnType != typeof(void) ||
                !ValidateDarkCloudStrike(PatchProcessor.GetOriginalInstructions(cloud)))
                throw new InvalidOperationException("Native Dark Cloud strike contract changed.");
            var patches = new List<KeyValuePair<MethodInfo, string>>();
            foreach (var m in BattleReaders()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteBattle)));
            foreach (var m in NearestCallers()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteNearestCalls)));
            foreach (var m in DamageProcs()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteDamageProc)));
            foreach (var m in AutonomousDebuffCallers()) patches.Add(new KeyValuePair<MethodInfo, string>(m, nameof(RewriteAutonomousDebuff)));
            patches.Add(new KeyValuePair<MethodInfo, string>(cloud, nameof(RewriteDarkCloudCallback)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(DaggerGrowthBullet), "HitCheck"), nameof(RewriteDagger)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(IceSword), "Update"), nameof(RewriteIceSword)));
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
            harmony.Patch(cloud, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeDarkCloudStrike)));
            FriendlyFireArtifactRangeHooks.Install(harmony);
        }

        internal static bool ValidateDarkCloudStrike(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var owner = AccessTools.PropertyGetter(typeof(ComboEffectBase), "Networkavatar");
            var playerCombo = AccessTools.Field(typeof(ComboEffectBase), "isPlayerCombo");
            var attacked = AccessTools.Field(typeof(ComboEffect_DarkCloud), "OnAttackUnit");
            var damage = AccessTools.DeclaredMethod(typeof(CombatBehaviour), "ApplyDamage", new[] { typeof(DamageInstance) });
            var invoke = AccessTools.Method(typeof(Action<UnitAvatar>), "Invoke");
            var fx = AccessTools.DeclaredMethod(typeof(ComboEffect_DarkCloud), "TargetCreateFx");
            if (owner == null || playerCombo == null || attacked == null || damage == null || invoke == null || fx == null) return false;
            int hit = code.FindIndex(i => i.Calls(damage));
            int callback = code.FindIndex(i => i.Calls(invoke));
            int effect = code.FindIndex(i => i.Calls(fx));
            return code.Any(i => i.Calls(owner)) && code.Any(i => i.LoadsField(playerCombo)) && code.Any(i => i.LoadsField(attacked)) &&
                hit >= 0 && callback > hit && effect > callback && code.Count(i => i.Calls(damage)) == 1 &&
                code.Count(i => i.Calls(invoke)) == 1 && code.Count(i => i.Calls(fx)) == 1;
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

        internal static IEnumerable<CodeInstruction> RewriteAutonomousDebuff(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireTargetingHooks.ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDebuff", new[] { typeof(CharacterDebuff), typeof(UnitAvatar) }),
                nameof(FriendlyFireRuntime.ApplyAutonomousArtifactDebuff), 1);

        internal static IEnumerable<CodeInstruction> RewriteDarkCloudCallback(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var invoke = AccessTools.Method(typeof(Action<UnitAvatar>), "Invoke");
            var calls = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(invoke)).ToArray();
            if (calls.Length != 1 || code[calls[0]].blocks.Count != 0)
                throw new InvalidOperationException("Native Dark Cloud callback contract changed.");
            int index = calls[0];
            var owner = new CodeInstruction(OpCodes.Ldarg_0);
            owner.labels.AddRange(code[index].labels); code[index].labels.Clear();
            code[index].opcode = OpCodes.Call;
            code[index].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.InvokeDarkCloudAttack));
            code.Insert(index, owner);
            return code;
        }

        internal static IEnumerable<CodeInstruction> RewriteDagger(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.DirectAttack, nameof(FriendlyFireRuntime.ItemHitTarget));

        internal static IEnumerable<CodeInstruction> RewriteIceSword(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var mask = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetHostileFactionLayers");
            var manager = AccessTools.PropertyGetter(typeof(RuntimeFactionManager), "Instance");
            var parent = AccessTools.Field(typeof(IceSword), "parentCharm");
            var owner = AccessTools.PropertyGetter(typeof(Charm_Basic), "NetworkAvatar");
            var faction = AccessTools.Field(typeof(UnitAvatar), "faction");
            var layer = AccessTools.DeclaredMethod(typeof(RuntimeFactionManager), "FindFactionLayer");
            if (mask == null || manager == null || parent == null || owner == null || faction == null || layer == null)
                throw new InvalidOperationException("Native Plitvice sword target members changed.");
            var filters = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(layer)).ToArray();
            if (filters.Length != 1) throw new InvalidOperationException("Native Plitvice sword target filter changed.");
            int start = filters[0] - 6;
            if (start < 1 || start + 8 >= code.Count || !code[start - 1].LoadsConstant((int)EDamageFromType.None) ||
                !code[start].Calls(mask) || !code[start + 1].Calls(manager) || !code[start + 2].IsLdarg(0) ||
                !code[start + 3].LoadsField(parent) || !code[start + 4].Calls(owner) || !code[start + 5].LoadsField(faction) ||
                code[start + 7].opcode != OpCodes.And ||
                (code[start + 8].opcode != OpCodes.Brfalse && code[start + 8].opcode != OpCodes.Brfalse_S) ||
                code.Skip(start + 1).Take(7).Any(i => i.labels.Count != 0 || i.blocks.Count != 0))
                throw new InvalidOperationException("Native Plitvice sword target/source pairing changed.");
            // Leave target, damage type and the native parent owner on the stack.
            // Preserve the long-valued mask test, collision cadence and hit RPCs.
            foreach (int offset in new[] { 0, 1, 5, 6 })
            { code[start + offset].opcode = OpCodes.Nop; code[start + offset].operand = null; }
            code[start + 7].opcode = OpCodes.Call;
            code[start + 7].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ItemTarget));
            code.Insert(start + 8, new CodeInstruction(OpCodes.Conv_I8));
            return code;
        }

    }
}
