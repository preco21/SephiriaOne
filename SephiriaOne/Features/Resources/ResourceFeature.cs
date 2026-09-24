using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ResourceFeature
    {
        private static bool loaded;
        private static string startingError = "Not initialized.", inventoryError = "Not initialized.", budgetError = "Not initialized.";
        public static bool IsAvailable(ResourceKind kind) => loaded && Probe(kind);
        public static string UnavailableReason(ResourceKind kind) => !loaded ? "Resource patch runtime is unavailable." : Reason(kind);
        public static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); loaded = true; Install(); }
            catch (Exception error) { Debug.LogError("[SephiriaOne] Resource guards unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install()
        {
            // One unsupported boundary must not disable unrelated native controls.
            TryInstall(ResourceBudgetHooks.Install, "Talent/fruit", ref budgetError);
            TryInstall(StartingResourceHooks.Install, "Starting dice/leaves", ref startingError);
            TryInstall(InventoryResourceHooks.Install, "Inventory", ref inventoryError);
        }
        private static void TryInstall(Action install, string group, ref string error)
        {
            try { install(); error = ""; }
            catch (Exception exception)
            { error = exception.GetBaseException().Message; Debug.LogError("[SephiriaOne] " + group + " guard unavailable: " + exception); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Probe(ResourceKind kind) => kind == ResourceKind.Dice || kind == ResourceKind.Leaves ? StartingResourceHooks.Available :
            kind == ResourceKind.Slots ? InventoryResourceHooks.Available : ResourceBudgetHooks.Available;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Reason(ResourceKind kind)
        {
            string reason = kind == ResourceKind.Dice || kind == ResourceKind.Leaves ? StartingResourceHooks.Error ?? startingError :
                kind == ResourceKind.Slots ? inventoryError : budgetError;
            return string.IsNullOrEmpty(reason) ? "Native resource guard is unavailable. Check Player.log." : reason;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Shutdown()
        {
            // Inventory refuses unsafe unload while expanded occupied capacity is
            // present; keep the controller and native restore guards in that case.
            InventoryResourceHooks.Uninstall();
            StartingResourceHooks.Uninstall(); ResourceBudgetHooks.Uninstall();
            loaded = false;
        }
    }
}
