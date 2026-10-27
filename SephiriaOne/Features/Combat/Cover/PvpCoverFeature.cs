using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class PvpCoverFeature
    {
        internal static bool Available { get; private set; }
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); Available = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] PvP cover unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => PvpCoverNative.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => PvpCoverNative.Uninstall();
        internal static void Shutdown()
        {
            Available = false;
            PvpCoverRuntime.Clear();
            if (PvpCoverRuntime.ActiveCount != 0)
                throw new InvalidOperationException("PvP cover cleanup is pending; keep native guards and the controller loaded for retry.");
            Uninstall();
        }
    }
}
