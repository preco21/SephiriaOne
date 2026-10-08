using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class CollinHooks
    {
        private const string Owner = "SephiriaOne.CollinStartingArtifact";
        internal static bool ValidateNative()
        {
            if (!CollinRuntime.ValidateFields()) return false;
            try
            {
                var update = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(PlayerAvatar), nameof(PlayerAvatar.UpdateCostumeData)));
                var restock = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(GridInventory), nameof(GridInventory.RestockStartingItem)));
                return update.Count(i => i.Calls(AccessTools.Method(typeof(GridInventory), nameof(GridInventory.AddStartingItem)))) == 1 &&
                    update.Count(i => i.Calls(AccessTools.Method(typeof(GridInventory), nameof(GridInventory.RemoveStartingItem)))) == 1 &&
                    restock.Any(i => i.operand is FieldInfo field && field.Name == "startingItems");
            }
            catch { return false; }
        }
        internal static void Install()
        {
            if (!ValidateNative()) throw new InvalidOperationException("Native costume starting-item lifecycle changed.");
            var harmony = new Harmony(Owner);
            try
            {
                harmony.Patch(AccessTools.DeclaredMethod(typeof(PlayerAvatar), nameof(PlayerAvatar.UpdateCostumeData)),
                    prefix: new HarmonyMethod(typeof(CollinHooks), nameof(BeforeCostume)),
                    postfix: new HarmonyMethod(typeof(CollinHooks), nameof(AfterCostume)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(GridInventory), nameof(GridInventory.RestockStartingItem)),
                    prefix: new HarmonyMethod(typeof(CollinHooks), nameof(Restock)));
            }
            catch { harmony.UnpatchAll(Owner); throw; }
        }
        private static void BeforeCostume(PlayerAvatar __instance) => CollinRuntime.BeforeCostume(__instance);
        private static void AfterCostume(PlayerAvatar __instance, string costumeID) => CollinRuntime.AfterCostume(__instance, costumeID);
        private static void Restock(GridInventory __instance) => CollinRuntime.BeforeRestock(__instance);
        internal static void Uninstall() => new Harmony(Owner).UnpatchAll(Owner);
    }
}
