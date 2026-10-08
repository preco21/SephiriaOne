using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class BatCostumeFeature
    {
        internal static bool Available { get; private set; }
        internal static void Initialize()
        {
            if (Available) return;
            try { HarmonyRuntime.EnsureLoaded(); Install(); Available = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Bat costume hooks unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => BatCostumeHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => BatCostumeHooks.Uninstall();
        internal static void Shutdown()
        {
            SessionSettings.BeforeBatShutdown();
            SessionSettings.RestoreBat();
            if (Available) Uninstall();
            Available = false;
        }
    }
}
