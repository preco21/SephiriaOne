using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class JarSpawnFeature
    {
        private static bool loaded;
        internal static bool Available => loaded;
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); loaded = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Mystic Jar spawn hooks unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => JarSpawnHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => JarSpawnHooks.Uninstall();
        internal static void Shutdown() { loaded = false; Uninstall(); }
    }
}
