using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    // Optional native contract: its failure must not disable ordinary combat.
    internal static class LobbyRegenFeature
    {
        internal static bool Available { get; private set; }
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Install(); Available = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Lobby recovery control unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => LobbyRegenHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => LobbyRegenHooks.Uninstall();
        internal static void Shutdown() { Available = false; Uninstall(); }
    }
}
