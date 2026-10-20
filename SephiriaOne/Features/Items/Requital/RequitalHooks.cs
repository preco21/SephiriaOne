using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class RequitalHooks
    {
        private const string Owner = "SephiriaOne.RequitalStartingItems";
        internal static bool ValidateNative()
        {
            if (!RequitalRuntime.ValidateFields()) return false;
            try
            {
                var restock = AccessTools.DeclaredMethod(typeof(GridInventory), nameof(GridInventory.RestockStartingItem), new[] { typeof(int) });
                var restart = AccessTools.DeclaredMethod(typeof(PlayerSpawner), nameof(PlayerSpawner.RestartNewGame), new[] { typeof(int) });
                return restock != null && restart != null &&
                    PatchProcessor.GetOriginalInstructions(restock).Any(i => i.operand is FieldInfo field && field.Name == "startingItems") &&
                    PatchProcessor.GetOriginalInstructions(restock).Count(i => i.Calls(AddItem)) == 1 &&
                    PatchProcessor.GetOriginalInstructions(restart).Count(i => i.Calls(restock)) == 1;
            }
            catch { return false; }
        }
        internal static void Install()
        {
            if (!ValidateNative()) throw new InvalidOperationException("Native Requital starting-item lifecycle changed.");
            var harmony = new Harmony(Owner);
            try
            {
                harmony.Patch(AccessTools.DeclaredMethod(typeof(GridInventory), nameof(GridInventory.RestockStartingItem)),
                    prefix: new HarmonyMethod(typeof(RequitalHooks), nameof(Restock)),
                    transpiler: new HarmonyMethod(typeof(RequitalHooks), nameof(ReplaceRestockDelivery)));
            }
            catch { harmony.UnpatchAll(Owner); throw; }
        }
        private static void Restock(GridInventory __instance) => RequitalRuntime.BeforeRestock(__instance);
        private static MethodInfo AddItem => AccessTools.DeclaredMethod(typeof(GridInventory), nameof(GridInventory.AddItem),
            new[] { typeof(ItemMetadata), typeof(int), typeof(bool), typeof(bool) });
        private static IEnumerable<CodeInstruction> ReplaceRestockDelivery(IEnumerable<CodeInstruction> source)
        {
            var instructions = source.ToList();
            if (instructions.Count(i => i.Calls(AddItem)) != 1) throw new InvalidOperationException("Native starting-item delivery changed.");
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(AddItem))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(RequitalRuntime), nameof(RequitalRuntime.RestockItem));
                }
                yield return instruction;
            }
        }
        internal static void Uninstall() => new Harmony(Owner).UnpatchAll(Owner);
    }
}
