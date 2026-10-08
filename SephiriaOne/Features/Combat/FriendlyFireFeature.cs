using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class FriendlyFireFeature
    {
        private static bool loaded;
        internal static bool Available => loaded;
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); loaded = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Friendly fire unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => FriendlyFireHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => FriendlyFireHooks.Uninstall();
        internal static void Shutdown() { loaded = false; Uninstall(); }
    }
}
