using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class CollinFeature
    {
        private static bool loaded;
        internal static bool Available => loaded && CollinRuntime.ItemAvailable;
        internal static void Initialize()
        {
            if (loaded) return;
            try { HarmonyRuntime.EnsureLoaded(); Install(); loaded = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Collin starting-artifact hooks unavailable: " + error); }
        }
        internal static void OnDatabasesReady()
        {
            try
            {
                if (!CollinRuntime.ValidateItem()) Debug.LogWarning("[SephiriaOne] Collin starting-artifact identity checks failed; native behavior continues.");
            }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Collin starting-artifact database check failed: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => CollinHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => CollinHooks.Uninstall();
        internal static void Shutdown() { loaded = false; Uninstall(); }
    }
}
