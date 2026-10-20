using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ProjectileLimitFeature
    {
        private static bool loaded;
        internal static bool Available => loaded;
        internal static void Initialize()
        {
            if (loaded) return;
            try { HarmonyRuntime.EnsureLoaded(); Install(); loaded = true; }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Secondary projectile limits unavailable: " + error); }
        }
        internal static void Shutdown()
        {
            ProjectileLimitRuntime.Clear();
            if (loaded) Uninstall();
            loaded = false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => ProjectileLimitHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Uninstall() => ProjectileLimitHooks.Uninstall();
    }
}
