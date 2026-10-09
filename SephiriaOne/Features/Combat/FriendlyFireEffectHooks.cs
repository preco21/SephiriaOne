using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireEffectHooks
    {
        // Explicit consumers keep a targeting fix out of healing, UI warnings
        // and unrelated automatic spells. Add a contract test with each entry.
        internal static IEnumerable<MethodInfo> Selectors()
        {
            yield return AccessTools.DeclaredMethod(typeof(WeaponAddonCommon_BurnRing), "DamageNearbyEnemies");
            yield return AccessTools.DeclaredMethod(typeof(Charm_FireFeather), "SearchTarget");
            yield return AccessTools.DeclaredMethod(typeof(Charm_FlameGround_Meteor), "SearchTarget");
            yield return Iterator(typeof(ComboEffect_DarkCloud), "UseCloudCoroutine");
            yield return Iterator(typeof(Charm_ThunderousSteps), "CreateAttack");
        }

        private static MethodInfo Iterator(Type type, string method)
        {
            var state = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(t => t.Name.StartsWith("<" + method + ">", StringComparison.Ordinal));
            return state == null ? null : AccessTools.DeclaredMethod(state, "MoveNext", Type.EmptyTypes);
        }

        internal static void Install(Harmony harmony)
        {
            var selectors = Selectors().ToArray();
            var chakram = AccessTools.DeclaredMethod(typeof(Charm_FireChakram), "OnUpdate");
            var debuff = AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDebuff", new[] { typeof(CharacterDebuff), typeof(UnitAvatar) });
            if (debuff == null || debuff.ReturnType != typeof(void) || selectors.Any(m => m == null) || chakram == null)
                throw new InvalidOperationException("Native friendly-fire item/debuff entry points changed.");
            foreach (var selector in selectors) RewriteSelector(PatchProcessor.GetOriginalInstructions(selector)).ToList();
            RewriteChakram(PatchProcessor.GetOriginalInstructions(chakram)).ToList();
            foreach (var selector in selectors)
                harmony.Patch(selector, transpiler: new HarmonyMethod(typeof(FriendlyFireEffectHooks), nameof(RewriteSelector)));
            harmony.Patch(chakram, transpiler: new HarmonyMethod(typeof(FriendlyFireEffectHooks), nameof(RewriteChakram)));
            harmony.Patch(debuff, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeDebuff)),
                finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterDebuff)));
            var spawn = AccessTools.DeclaredMethod(typeof(CharacterDebuff), "InitializeAndSpawn");
            harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeDebuffSpawn)),
                postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterDebuffSpawn)));
            foreach (string method in new[] { "Update", "Destroy", "AddStack" })
                harmony.Patch(AccessTools.DeclaredMethod(typeof(CharacterDebuff), method, Type.EmptyTypes),
                    prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), method == "Update" ? nameof(FriendlyFireRuntime.BeforeDebuffUpdate) : nameof(FriendlyFireRuntime.BeforeDebuffOperation)),
                    finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterDebuffOperation)));
        }

        internal static IEnumerable<CodeInstruction> RewriteSelector(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var filters = Enumerable.Range(0, code.Count).Where(i => Calls(code[i], typeof(CombatManager), "ContainsAttackableFaction")).ToArray();
            if (filters.Length != 1) throw new InvalidOperationException("Native offensive item selector changed.");
            int filter = filters[0];
            int mask = code.FindLastIndex(filter, i => Calls(i, typeof(UnitAvatar), "GetHostileFactionLayers"));
            if (mask < 1 || filter - mask < 3 || filter - mask > 16 || !code[mask - 1].LoadsConstant((int)EDamageFromType.None) ||
                code[filter].labels.Count != 0 || code[filter].blocks.Count != 0 ||
                code[filter - 1].opcode != OpCodes.Ldfld || !(code[filter - 1].operand is FieldInfo field) ||
                field.DeclaringType != typeof(UnitAvatar) || field.Name != "faction" ||
                code.Skip(mask + 1).Take(filter - mask - 1).Any(i => i.labels.Count != 0 || i.blocks.Count != 0 ||
                    i.opcode.FlowControl == FlowControl.Branch || i.opcode.FlowControl == FlowControl.Cond_Branch))
                throw new InvalidOperationException("Native offensive target/source pairing changed.");
            // Leave target, damage type and source on the stack. Keep labels and
            // exception boundaries on their original instructions.
            code[mask].opcode = OpCodes.Nop; code[mask].operand = null;
            code[filter - 1].opcode = OpCodes.Nop; code[filter - 1].operand = null;
            code[filter].opcode = OpCodes.Call;
            code[filter].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ItemTarget));
            return code;
        }

        internal static IEnumerable<CodeInstruction> RewriteChakram(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var matches = Enumerable.Range(1, Math.Max(0, code.Count - 3)).Where(i =>
                Calls(code[i], typeof(UnitAvatar), "GetHostileFactionLayers") && code[i - 1].LoadsConstant((int)EDamageFromType.None) &&
                code[i + 1].IsLdloc() && code[i + 2].opcode == OpCodes.And).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Native chakram target mask changed.");
            int mask = matches[0];
            if (code[mask + 1].labels.Count != 0 || code[mask + 2].labels.Count != 0 ||
                code[mask + 1].blocks.Count != 0 || code[mask + 2].blocks.Count != 0)
                throw new InvalidOperationException("Native chakram mask boundaries changed.");
            code[mask].opcode = OpCodes.Nop; code[mask].operand = null;
            code[mask + 2].opcode = OpCodes.Call;
            code[mask + 2].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ItemTargetMask));
            code.InsertRange(mask + 2, new[] { new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Charm_Basic), "NetworkAvatar")) });
            return code;
        }
        private static bool Calls(CodeInstruction i, Type type, string name) =>
            (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.DeclaringType == type && m.Name == name;
    }
}
