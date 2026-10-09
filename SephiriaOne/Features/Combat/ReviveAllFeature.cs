using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ReviveAllFeature
    {
        internal static bool Available { get; private set; }
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); Available = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Revive all unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => ReviveAllHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => ReviveAllHooks.Uninstall();
        internal static void Shutdown() { Available = false; Uninstall(); }
    }
}
