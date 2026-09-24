using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class SessionBoundaryFeature
    {
        public static bool Available { get; private set; }

        public static void Initialize()
        {
            try
            {
                HarmonyRuntime.EnsureLoaded();
                Install();
                Available = true;
                Debug.Log("[SephiriaOne] Fountain grant synchronization guard ready.");
            }
            catch (Exception exception)
            {
                Available = false;
                Debug.LogError("[SephiriaOne] Fountain grant synchronization guard unavailable; frame polling remains active: " + exception);
            }
        }

        public static void Shutdown()
        {
            if (!Available) return;
            Uninstall();
            Available = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => SessionBoundaryHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Uninstall() => SessionBoundaryHooks.Uninstall();
    }
}
