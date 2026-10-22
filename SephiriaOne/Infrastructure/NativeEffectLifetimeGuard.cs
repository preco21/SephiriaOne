using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    // Independent of optional combat rules: this native visual can outlive its
    // target after a disconnect in any hosted session.
    internal static class NativeEffectLifetimeGuard
    {
        public static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Optional native effect lifetime guard unavailable: " + error); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => NativeEffectLifetimeGuardHooks.Install();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Shutdown() => NativeEffectLifetimeGuardHooks.Uninstall();
    }
}
