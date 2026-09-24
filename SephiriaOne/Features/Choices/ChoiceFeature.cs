using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ChoiceFeature
    {
        public static bool Available { get; private set; }

        public static void Initialize()
        {
            try
            {
                HarmonyRuntime.EnsureLoaded();
                InstallGuards();
                Available = true;
                Debug.Log("[SephiriaOne] Candidate commands ready: /choices (extra choices 0..20)");
            }
            catch (Exception exception)
            {
                Available = false;
                Debug.LogError("[SephiriaOne] Candidate commands unavailable: " + exception);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallGuards() => ChoiceSafety.Install();

        public static void Shutdown()
        {
            if (!Available) return;
            ChoicePoints.RemoveContributions();
            RemoveGuards();
            Available = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RemoveGuards() => ChoiceSafety.Uninstall();
    }
}
