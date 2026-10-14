using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class LobbyRegenHooks
    {
        private const string Id = "SephiriaOne.LobbyRegen";
        private static readonly MethodInfo heal = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Heal", new[] { typeof(float) });
        private static readonly MethodInfo healPlace = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Heal", new[] { typeof(float), typeof(bool), typeof(bool) });
        private static readonly MethodInfo healMp = AccessTools.DeclaredMethod(typeof(UnitAvatar), "HealMp", new[] { typeof(int) });

        internal static void Install()
        {
            var player = AccessTools.DeclaredMethod(typeof(PlayerAvatar), "Update", Type.EmptyTypes);
            var unit = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Update", Type.EmptyTypes);
            if (player == null || unit == null || player.IsStatic || unit.IsStatic || player.ReturnType != typeof(void) || unit.ReturnType != typeof(void) ||
                !ValidatePlayer(PatchProcessor.GetOriginalInstructions(player)) || !ValidateUnit(PatchProcessor.GetOriginalInstructions(unit)))
                throw new InvalidOperationException("Native lobby/passive HP/MP recovery contract changed.");
            var harmony = new Harmony(Id);
            try
            {
                harmony.Patch(player, transpiler: new HarmonyMethod(typeof(LobbyRegenHooks), nameof(RewritePlayer)));
                harmony.Patch(unit, transpiler: new HarmonyMethod(typeof(LobbyRegenHooks), nameof(RewriteUnit)));
            }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() => new Harmony(Id).UnpatchAll(Id);

        // Validate the specific call sites, rather than intercepting all healing
        // or skipping Update (which also drives combat, networking and movement).
        internal static bool ValidatePlayer(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int at = code.FindIndex(i => i.Calls(healPlace));
            return healPlace != null && code.Count(i => i.Calls(healPlace)) == 1 && at >= 4 &&
                code[at - 4].opcode == OpCodes.Ldarg_0 && code[at - 3].LoadsConstant(10f) &&
                code[at - 2].LoadsConstant(0) && code[at - 1].LoadsConstant(0) &&
                code.Take(at).Any(i => i.operand is FieldInfo f && f.DeclaringType == typeof(RaceEntity) && f.Name == "healPlaces");
        }
        internal static bool ValidateUnit(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            bool Site(MethodInfo method, string counter, string stat, bool hp)
            {
                int at = code.FindIndex(i => i.Calls(method));
                return method != null && code.Count(i => i.Calls(method)) == 1 && at >= 2 && code[at - 2].opcode == OpCodes.Ldarg_0 &&
                    (hp ? code[at - 1].LoadsConstant(1f) : code[at - 1].LoadsConstant(1)) &&
                    code.Take(at).Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType == typeof(UnitAvatar) && f.Name == counter) &&
                    code.Take(at).Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, stat));
            }
            return Site(heal, "hpRegenCounter", "HPREGEN", true) && Site(healMp, "mpRegenCounter", "MPREGEN", false);
        }
        internal static IEnumerable<CodeInstruction> RewritePlayer(IEnumerable<CodeInstruction> instructions) => Rewrite(instructions, true);
        internal static IEnumerable<CodeInstruction> RewriteUnit(IEnumerable<CodeInstruction> instructions) => Rewrite(instructions, false);
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, bool player)
        {
            var code = instructions.ToList();
            if (!(player ? ValidatePlayer(code) : ValidateUnit(code))) throw new InvalidOperationException("Recovery call site changed during patching.");
            foreach (var instruction in code)
            {
                string wrapper = player ? (instruction.Calls(healPlace) ? nameof(LobbyRegenRuntime.HealPlace) : null) :
                    instruction.Calls(heal) ? nameof(LobbyRegenRuntime.HealPassive) : instruction.Calls(healMp) ? nameof(LobbyRegenRuntime.HealMpPassive) : null;
                if (wrapper != null)
                {
                    // Same stack shape. Preserve labels and exception boundaries.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.DeclaredMethod(typeof(LobbyRegenRuntime), wrapper);
                }
                yield return instruction;
            }
        }
    }
}
