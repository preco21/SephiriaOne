using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class BatCostumeHooks
    {
        private const string Owner = "SephiriaOne.BatCostume";
        internal static void Install()
        {
            if (!ValidateNative()) throw new InvalidOperationException("Native Bat costume/status contract changed.");
            var harmony = new Harmony(Owner);
            try
            {
                harmony.Patch(AccessTools.Method(typeof(PlayerAvatar), nameof(PlayerAvatar.UpdateCostumeData)),
                    transpiler: new HarmonyMethod(typeof(BatCostumeHooks), nameof(Rewrite)));
                harmony.Patch(AccessTools.Method(typeof(StatusInstance), nameof(StatusInstance.ClearTarget)),
                    postfix: new HarmonyMethod(typeof(BatCostumeHooks), nameof(Cleared)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(PlayerAvatar), "OnDestroy"),
                    prefix: new HarmonyMethod(typeof(BatCostumeHooks), nameof(Destroying)));
            }
            catch { harmony.UnpatchAll(Owner); throw; }
        }
        internal static void Uninstall() => new Harmony(Owner).UnpatchAll(Owner);
        private static void Cleared(StatusInstance __instance) => BatCostumeRuntime.ForgetStatus(__instance);
        private static void Destroying(PlayerAvatar __instance) => BatCostumeRuntime.ForgetPlayer(__instance);
        internal static bool ValidateNative()
        {
            if (!BatCostumeRuntime.ValidateFields()) return false;
            try
            {
                var update = AccessTools.DeclaredMethod(typeof(PlayerAvatar), nameof(PlayerAvatar.UpdateCostumeData), new[] { typeof(string), typeof(bool) });
                Rewrite(PatchProcessor.GetOriginalInstructions(update)).ToList();
                foreach (string name in new[] { "ApplyStatusInner", "RemoveStatusInner" })
                {
                    var code = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(StatusInstance_HPSteal), name));
                    if (code.Count(i => i.Calls(AccessTools.Method(typeof(UnitAvatar), nameof(UnitAvatar.AddCustomStat), new[] { typeof(ECustomStat), typeof(int) }))) != 1 ||
                        code.Count(i => i.Calls(AccessTools.PropertyGetter(typeof(StatusInstance), nameof(StatusInstance.Value)))) != 1 ||
                        !code.Any(i => i.LoadsConstant((int)ECustomStat.HPSteal)) ||
                        code.Count(i => i.opcode == OpCodes.Neg) != (name == "RemoveStatusInner" ? 1 : 0)) return false;
                }
                return true;
            }
            catch { return false; }
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var factory = AccessTools.Method(typeof(StatusDatabase), nameof(StatusDatabase.CreateStatusEntity), new[] { typeof(string) });
            int at = code.FindIndex(i => i.Calls(factory));
            if (at < 0 || code.Count(i => i.Calls(factory)) != 1 ||
                !code.Any(i => i.Calls(AccessTools.Method(typeof(StatusInstance), nameof(StatusInstance.ApplyStatus)))) ||
                !code.Any(i => i.Calls(AccessTools.Method(typeof(StatusInstance), nameof(StatusInstance.RemoveStatus)))) ||
                !code.Any(i => i.Calls(AccessTools.Method(typeof(StatusInstance), nameof(StatusInstance.ClearTarget)))))
                throw new InvalidOperationException("Native costume status ownership changed.");
            var player = new CodeInstruction(OpCodes.Ldarg_0);
            player.labels.AddRange(code[at].labels); player.blocks.AddRange(code[at].blocks);
            code[at].labels.Clear(); code[at].blocks.Clear();
            code[at].operand = AccessTools.Method(typeof(BatCostumeRuntime), nameof(BatCostumeRuntime.Create));
            code.InsertRange(at, new[] { player, new CodeInstruction(OpCodes.Ldarg_1) });
            return code;
        }
    }
}
