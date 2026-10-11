using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    // Only offensive spell searches. Native input aim, healing, summon/buff
    // targeting and global faction relationships remain outside these scopes.
    internal static class FriendlyFireSpellHooks
    {
        internal static IEnumerable<MethodInfo> MagicSelectors()
        {
            yield return FriendlyFireEffectHooks.Iterator(typeof(ActiveSkill_FireBulletsToEnemies), "FireCoroutine");
            yield return FriendlyFireEffectHooks.Iterator(typeof(ActiveSkill_MeteorShower), "CreateMeteorCoroutine");
        }

        internal static void Install(Harmony harmony)
        {
            var patches = new List<KeyValuePair<MethodInfo, string>>();
            foreach (var method in MagicSelectors()) patches.Add(new KeyValuePair<MethodInfo, string>(method,
                method?.DeclaringType.DeclaringType == typeof(ActiveSkill_FireBulletsToEnemies) ? nameof(RewriteVolley) : nameof(RewriteMagic)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(ActiveSkill_Ball), "Update"), nameof(RewriteNearest)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(AutoMagicCaster), "SearchTarget"), nameof(RewriteAuto)));
            patches.Add(new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(typeof(DungeonManager), "SearchTargetForChainLightning"), nameof(RewriteChain)));
            var update = AccessTools.DeclaredMethod(typeof(AutoMagicCaster), "Update");
            var owner = AccessTools.Field(typeof(AutoMagicCaster), "owner");
            var aimed = AccessTools.Field(typeof(AutoMagicCaster), "aimedEnemy");
            if (update == null || owner?.FieldType != typeof(UnitAvatar) || aimed?.FieldType != typeof(UnitAvatar) ||
                !PatchProcessor.GetOriginalInstructions(update).Any(i => i.LoadsField(aimed)))
                throw new InvalidOperationException("Native automatic magic target lifetime changed.");
            foreach (var patch in patches)
            {
                if (patch.Key == null) throw new InvalidOperationException("Native offensive spell entry point changed: " + patch.Value);
                var rewrite = AccessTools.DeclaredMethod(typeof(FriendlyFireSpellHooks), patch.Value);
                ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { PatchProcessor.GetOriginalInstructions(patch.Key) })).ToList();
            }
            foreach (var patch in patches) harmony.Patch(patch.Key, transpiler: new HarmonyMethod(typeof(FriendlyFireSpellHooks), patch.Value));
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeAutoMagic)));
        }

        internal static IEnumerable<CodeInstruction> RewriteMagic(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.Magic, nameof(FriendlyFireRuntime.ItemHitTarget));

        internal static IEnumerable<CodeInstruction> RewriteVolley(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireTargetingHooks.ReplaceCall(RewriteMagic(instructions), typeof(Component).GetMethods()
                .Single(m => m.Name == "GetComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                .MakeGenericMethod(typeof(UnitAvatar)), nameof(FriendlyFireRuntime.SpellOverlapAvatar), 1);

        internal static IEnumerable<CodeInstruction> RewriteAuto(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.None, nameof(FriendlyFireRuntime.ItemHitTarget));

        internal static IEnumerable<CodeInstruction> RewriteChain(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.None, nameof(FriendlyFireRuntime.ItemTarget));

        internal static IEnumerable<CodeInstruction> RewriteNearest(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireTargetingHooks.ReplaceCall(instructions, AccessTools.DeclaredMethod(typeof(PlayerInputController), "SearchTargetNearestPoint"),
                nameof(FriendlyFireRuntime.OffensiveNearestPoint), 1);
    }
}
