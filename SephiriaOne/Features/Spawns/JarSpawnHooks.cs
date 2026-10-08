using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class JarSpawnHooks
    {
        private const string Id = "SephiriaOne.JarSpawns";
        [ThreadStatic] private static int hiddenRewardDepth;
        private static bool warned;

        internal static void Install()
        {
            var spawn = AccessTools.DeclaredMethod(typeof(MysticPot), "OnStartServer", Type.EmptyTypes);
            var hidden = AccessTools.DeclaredMethod(typeof(HiddenRoomRewardSpawner), "OnStartServer", Type.EmptyTypes);
            if (spawn == null || spawn.ReturnType != typeof(void) || !Validate(PatchProcessor.GetOriginalInstructions(spawn)) || !ValidateHidden())
                throw new InvalidOperationException("Native Mystic Jar spawn contract changed.");
            var harmony = new Harmony(Id);
            try
            {
                harmony.Patch(hidden, prefix: new HarmonyMethod(typeof(JarSpawnHooks), nameof(BeforeHiddenReward)),
                    finalizer: new HarmonyMethod(typeof(JarSpawnHooks), nameof(AfterHiddenReward)));
                harmony.Patch(spawn, transpiler: new HarmonyMethod(typeof(JarSpawnHooks), nameof(Rewrite)));
            }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() { new Harmony(Id).UnpatchAll(Id); hiddenRewardDepth = 0; warned = false; }

        internal static bool ValidateHidden()
        {
            var hidden = AccessTools.DeclaredMethod(typeof(HiddenRoomRewardSpawner), "OnStartServer", Type.EmptyTypes);
            var spawn = AccessTools.DeclaredMethod(typeof(HiddenRoomRewardSpawner), "SpawnProp");
            if (hidden == null || hidden.ReturnType != typeof(void) || spawn == null || spawn.ReturnType != typeof(void)) return false;
            var code = PatchProcessor.GetOriginalInstructions(hidden);
            return code.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "MysticPot")) == 1 &&
                code.Any(i => i.Calls(spawn)) && code.Any(i => Calls(i, typeof(PropDatabase), "FindPropById")) &&
                PatchProcessor.GetOriginalInstructions(spawn).Count(i => Calls(i, typeof(NetworkServer), "Spawn")) == 1;
        }
        internal static bool Validate(IEnumerable<CodeInstruction> instructions)
        {
            try { Rewrite(instructions).ToList(); return true; }
            catch (InvalidOperationException) { return false; }
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            bool Field(CodeInstruction i, string name) => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.DeclaringType == typeof(MysticPot) && f.Name == name;
            int chance = code.FindIndex(i => Field(i, "appearRate"));
            if (chance < 2 || chance + 2 >= code.Count || code.Count(i => Field(i, "appearRate")) != 1 ||
                code[chance - 1].opcode != OpCodes.Ldarg_0 || !Calls(code[chance - 2], typeof(System.Random), "NextDouble") ||
                code[chance + 1].opcode != OpCodes.Conv_R8 ||
                (code[chance + 2].opcode != OpCodes.Ble_Un_S && code[chance + 2].opcode != OpCodes.Ble_Un) ||
                code.Count(i => Calls(i, typeof(System.Random), "NextDouble")) != 1 ||
                code.Count(i => Field(i, "useRandomAppear")) != 1 || code.Count(i => Field(i, "minChapterNum")) != 2 ||
                code.Count(i => Calls(i, typeof(MysticPot), "set_NetworkisGenerated")) != 2)
                throw new InvalidOperationException("Mystic Jar chance/chapter/visibility IL changed.");
            // Preserve all native instructions and branch labels except this
            // single read. No second RNG draw, field mutation or custom spawn.
            code[chance].opcode = OpCodes.Call;
            code[chance].operand = AccessTools.Method(typeof(JarSpawnHooks), nameof(Chance));
            return code;
        }
        private static bool Calls(CodeInstruction i, Type owner, string name) =>
            (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.DeclaringType == owner && m.Name == name;

        internal static float Chance(MysticPot pot)
        {
            float native = pot.appearRate;
            if (!NetworkServer.active || hiddenRewardDepth != 0) return native;
            try
            {
                var settings = SessionSettings.JarSpawnsForGeneration;
                if (!settings.HasChanges) return native;
                float chance = settings.Probability(native);
                // Native uses draw > chance. A negative threshold ensures an
                // explicit 0% also rejects the rare exact-zero seeded draw.
                return chance == 0 ? -1f : chance;
            }
            catch (Exception error)
            {
                if (!warned) { warned = true; Debug.LogWarning("[SephiriaOne] Mystic Jar uses native spawn chance after hook failure: " + error); }
                return native;
            }
        }
        internal static void BeforeHiddenReward(out int __state) { __state = hiddenRewardDepth; hiddenRewardDepth++; }
        internal static Exception AfterHiddenReward(Exception __exception, int __state) { hiddenRewardDepth = __state; return __exception; }
    }
}
