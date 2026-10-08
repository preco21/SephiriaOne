using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class EventSpawnFeature
    {
        private static bool loaded;
        internal static bool Available => loaded;
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); loaded = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Random event hooks unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => EventSpawnHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => EventSpawnHooks.Uninstall();
        internal static void Shutdown() { loaded = false; Uninstall(); }
    }
}
