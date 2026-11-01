using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    // This contract is optional; a changed native writer disables only Max HP.
    internal static class PvpMaxHpFeature
    {
        internal static bool Available { get; private set; }

        internal static void Initialize()
        {
            if (Available) return;
            try
            {
                HarmonyRuntime.EnsureLoaded();
                Install();
                Available = true;
            }
            catch (Exception error)
            {
                Available = false;
                PvpMaxHpRuntime.Clear();
                Debug.LogWarning("[SephiriaOne] PvP Max HP normalization unavailable: " + error);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => PvpMaxHpHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => PvpMaxHpHooks.Uninstall();

        internal static void Shutdown()
        {
            Available = false;
            // Restore actual synchronized health before removing its safeguards.
            PvpMaxHpRuntime.Clear();
            Uninstall();
        }
    }
}
