using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ExperienceFeature
    {
        private static bool loaded;
        public static bool Available => loaded && Probe();
        public static void Initialize()
        {
            if (loaded) return;
            try { HarmonyRuntime.EnsureLoaded(); loaded = true; Install(); }
            catch (Exception error)
            { loaded = false; Debug.LogWarning("[SephiriaOne] XP runtime unavailable: " + error); }
        }
        public static void Shutdown()
        {
            if (!loaded) return;
            Uninstall(); loaded = false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => ExperienceHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Probe() => ExperienceHooks.Available;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Uninstall() => ExperienceHooks.Uninstall();
    }
}
