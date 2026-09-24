using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class DisconnectDiagnosticsFeature
    {
        public static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Optional disconnect diagnostics unavailable: " + error); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => DisconnectDiagnostics.Install();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Shutdown()
        {
            try { DisconnectDiagnostics.Uninstall(); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Disconnect diagnostics cleanup failed: " + error); }
        }
    }
}
