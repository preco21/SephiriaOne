using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class CombatModeFeature
    {
        internal static bool Available { get; private set; }
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); Available = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Combat Mode unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install()
        {
            if (!CombatModeHooks.ValidatePresentation()) throw new InvalidOperationException("Native combat menu/replication contract changed.");
            CombatModeHooks.Install();
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => CombatModeHooks.Uninstall();
        internal static void Shutdown() { Available = false; CombatModeRuntime.Clear(); Uninstall(); }
    }
}
