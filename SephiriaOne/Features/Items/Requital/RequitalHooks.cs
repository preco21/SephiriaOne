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
                // 1.1.0 keeps the old entry as a wrapper; placement-aware restarts
                // initialize and restock in the new overload. Resolve by name so
                // the same guard can still validate the earlier game contract.
                var placement = typeof(PlayerSpawner).Assembly.GetType("ERestartPlacement");
                if (placement != null)
                {
                    var placedRestart = AccessTools.DeclaredMethod(typeof(PlayerSpawner), nameof(PlayerSpawner.RestartNewGame),
                        new[] { typeof(int), placement });
                    if (!placement.IsEnum || restart == null || placedRestart == null ||
                        PatchProcessor.GetOriginalInstructions(restart).Count(i => i.Calls(placedRestart)) != 1) return false;
                    restart = placedRestart;
                }
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
