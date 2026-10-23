using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireElementalHooks
    {
        private static readonly MethodInfo UnsafeRead = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStatUnsafe", new[] { typeof(string) });
        private static readonly MethodInfo EnumRead = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetCustomStat", new[] { typeof(ECustomStat) });

        internal static void Install(Harmony harmony)
        {
            var converter = AccessTools.DeclaredMethod(typeof(UnitAvatar), "GetConvertedElementalDamage", new[] { typeof(int) });
            if (converter == null || converter.IsStatic || converter.ReturnType != typeof(int) ||
                !ValidateConverter(PatchProcessor.GetOriginalInstructions(converter)))
                throw new InvalidOperationException("Native converted elemental stat contract changed.");
            var index = AccessTools.DeclaredMethod(typeof(UnitAvatar), "ElementalIndexOf", new[] { typeof(string) });
            if (index == null || !ValidateIndices(PatchProcessor.GetOriginalInstructions(index)) || UnsafeRead == null || EnumRead == null ||
                PatchProcessor.GetOriginalInstructions(UnsafeRead).Count(i => i.Calls(converter)) != 1 ||
                PatchProcessor.GetOriginalInstructions(UnsafeRead).Count(i => i.Calls(index)) != 1)
                throw new InvalidOperationException("Native elemental key routing changed.");
            // Resolve and validate the entire preservation set before any patch.
            var readers = NativeReaders().ToArray();
            foreach (var reader in readers) RewriteNativeReads(PatchProcessor.GetOriginalInstructions(reader)).ToList();
            foreach (var reader in readers)
                harmony.Patch(reader, transpiler: new HarmonyMethod(typeof(FriendlyFireElementalHooks), nameof(RewriteNativeReads)));
            harmony.Patch(converter, postfix: new HarmonyMethod(typeof(PvpElementalRuntime), nameof(PvpElementalRuntime.AfterRead)));
        }

        internal static bool ValidateConverter(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            bool Calls(CodeInstruction i, string name) => i.operand is MethodInfo m && m.DeclaringType == typeof(UnitAvatar) && m.Name == name;
            return code.Count(i => Calls(i, "GetRawStatUnsafe")) == 2 &&
                code.Count(i => Calls(i, "SelectElementalConversion")) == 2 &&
                !code.Any(i => Calls(i, "GetCustomStat") || Calls(i, "GetCustomStatUnsafe") ||
                    i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld) &&
                code.Count(i => i.operand is FieldInfo f && f.Name == "ElementalDamageKeys") == 2 &&
                code.Any(i => i.LoadsConstant(20));
        }

        internal static bool ValidateIndices(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Where(i => i.opcode != OpCodes.Nop).ToList();
            var keys = new[] { "PHYSICALDAMAGE", "FIREDAMAGE", "ICEDAMAGE", "LIGHTNINGDAMAGE" };
            for (int element = 0; element < keys.Length; element++)
            {
                int at = code.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, keys[element]));
                if (at < 0 || at + 2 >= code.Count || !(code[at + 1].operand is MethodInfo equals) ||
                    equals.DeclaringType != typeof(string) || equals.Name != "op_Equality" ||
                    (code[at + 2].opcode != OpCodes.Brtrue && code[at + 2].opcode != OpCodes.Brtrue_S) ||
                    !(code[at + 2].operand is Label label)) return false;
                int target = code.FindIndex(i => i.labels.Contains(label));
                if (target < 0 || !code[target].LoadsConstant(element)) return false;
            }
            return true;
        }

        internal static IEnumerable<MethodInfo> NativeReaders()
        {
            var assembly = typeof(UnitAvatar).Assembly;
            foreach (var entry in new[]
            {
                ("UnitAvatar", "UpdateHighestElementalBonus"),
                ("PotionEffect_ElementalDamageBoost", "SetStatusEffect"),
                ("Charm_AddStatByAnotherStat", "OnEnabledEffect"),
                ("Charm_AddStatByAnotherStat", "OnUpdate"),
                ("Charm_AddStatByAnotherStat", "OnCharmEffectRefreshed"),
                ("CostumeUnlockEntity_RequiresStat", "UpdateCheck"),
                ("UnitStatDependencyManager", "CalculateBonus"),
                ("AvatarStatsHooker", "HookStat"),
                ("AvatarStatsHooker", "HasStat"),
                ("AvatarStatsHooker", "GetCustomStatUnsafe")
            })
            {
                var type = assembly.GetType(entry.Item1);
                var matches = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == entry.Item2).ToArray();
                if (matches == null || matches.Length != 1)
                    throw new InvalidOperationException("Native elemental preservation reader changed: " + entry.Item1 + "." + entry.Item2);
                yield return matches[0];
            }

            // This is a deferred script intrinsic, not a read inside Initialize.
            // Find its exact native stat-read body rather than a compiler ordinal.
            var scripts = assembly.GetType("MiniscriptGlobal");
            var deferred = scripts?.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(m => m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i => i.Calls(UnsafeRead))).ToArray();
            if (deferred == null || deferred.Length != 1 ||
                !PatchProcessor.GetOriginalInstructions(deferred[0]).Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "statName")))
                throw new InvalidOperationException("Native elemental script-stat predicate changed.");
            yield return deferred[0];
        }

        internal static IEnumerable<CodeInstruction> RewriteNativeReads(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            if (UnsafeRead == null || EnumRead == null) throw new InvalidOperationException("Native stat read signatures changed.");
            int count = 0;
            foreach (var instruction in code)
            {
                string helper = instruction.Calls(UnsafeRead) ? nameof(PvpElementalRuntime.ReadNativeUnsafe) :
                    instruction.Calls(EnumRead) ? nameof(PvpElementalRuntime.ReadNativeEnum) : null;
                if (helper == null) continue;
                // The instance and key are already on the stack. Preserve every
                // branch label/exception marker and all subsequent callbacks.
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.DeclaredMethod(typeof(PvpElementalRuntime), helper);
                count++;
            }
            if (count == 0) throw new InvalidOperationException("Native elemental preservation read changed.");
            return code;
        }
    }
}
