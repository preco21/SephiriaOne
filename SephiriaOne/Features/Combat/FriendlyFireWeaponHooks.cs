using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    // Weapon-specific offensive searches complement shared Bullet homing.
    // No global input-aim, faction-mask or projectile movement changes.
    internal static class FriendlyFireWeaponHooks
    {
        internal static IEnumerable<KeyValuePair<MethodInfo, string>> Selectors()
        {
            yield return Entry(typeof(WeaponSimple_Katana), "OnCreateMagicServerside", nameof(RewriteNearest));
            yield return Entry(typeof(WeaponSimple_SwordAndShield), "Update", nameof(RewriteNearest));
            yield return Entry(typeof(WeaponAddonKatana_Nastrond), "SearchTarget", nameof(RewriteReverse));
            yield return Entry(typeof(BulletMoveModule_CrossbowMine), "OnMove", nameof(RewriteMine));
            yield return Entry(typeof(BulletMoveModule_CrossbowMineHoming), "OnMove", nameof(RewriteMine));
        }

        private static KeyValuePair<MethodInfo, string> Entry(Type type, string method, string rewrite) =>
            new KeyValuePair<MethodInfo, string>(AccessTools.DeclaredMethod(type, method), rewrite);

        internal static void Install(Harmony harmony)
        {
            var selectors = Selectors().ToArray();
            foreach (var patch in selectors)
            {
                if (patch.Key == null) throw new InvalidOperationException("Native weapon target entry point changed: " + patch.Value);
                var rewrite = AccessTools.DeclaredMethod(typeof(FriendlyFireWeaponHooks), patch.Value);
                ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { PatchProcessor.GetOriginalInstructions(patch.Key) })).ToList();
            }
            foreach (var patch in selectors)
                harmony.Patch(patch.Key, transpiler: new HarmonyMethod(typeof(FriendlyFireWeaponHooks), patch.Value));
        }

        internal static IEnumerable<CodeInstruction> RewriteNearest(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireSpellHooks.RewriteNearest(instructions);

        internal static IEnumerable<CodeInstruction> RewriteReverse(IEnumerable<CodeInstruction> instructions) =>
            FriendlyFireEffectHooks.RewriteFaction(instructions, EDamageFromType.None, nameof(FriendlyFireRuntime.ItemTarget));

        internal static IEnumerable<CodeInstruction> RewriteMine(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var contains = AccessTools.DeclaredMethod(typeof(CombatManager), "ContainsAttackableFaction");
            var filters = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(contains)).ToArray();
            if (contains == null || filters.Length != 1) throw new InvalidOperationException("Native crossbow mine target filter changed.");
            int filter = filters[0];
            if (filter < 4 || !code[filter - 4].IsLdarg(0) || code[filter - 3].opcode != OpCodes.Ldfld ||
                !(code[filter - 3].operand is FieldInfo field) || field.Name != "hostileLayers" || field.FieldType != typeof(long) ||
                (field.DeclaringType != typeof(BulletMoveModule_CrossbowMine) && field.DeclaringType != typeof(BulletMoveModule_CrossbowMineHoming)) ||
                !code[filter - 2].IsLdloc() || !code[filter - 1].LoadsField(AccessTools.Field(typeof(UnitAvatar), "faction")) ||
                code.Skip(filter - 3).Take(4).Any(i => i.labels.Count != 0 || i.blocks.Count != 0))
                throw new InvalidOperationException("Native crossbow mine cached mask/target pairing changed.");
            // Stack: cached mask, candidate, movement module. Preserve native
            // cadence, height/ground/radius gates and stop/explosion behavior.
            code[filter - 1].opcode = OpCodes.Ldarg_0; code[filter - 1].operand = null;
            code[filter].opcode = OpCodes.Call;
            code[filter].operand = AccessTools.Method(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.WeaponMineTarget));
            return code;
        }
    }
}
