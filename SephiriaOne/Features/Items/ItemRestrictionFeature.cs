using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ItemRestrictionFeature
    {
        private static bool loaded;
        internal static bool Available => loaded && Probe() && ItemRestrictionRuntime.Fault == null;
        internal static bool Enabled => ItemRestrictionRuntime.Enabled;
        internal static string Fault => ItemRestrictionRuntime.Fault;
        internal static void Initialize()
        {
            if (loaded) return;
            try { HarmonyRuntime.EnsureLoaded(); Install(); loaded = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Item restriction hooks unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Install() => ItemRestrictionHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool Probe() => ItemRestrictionHooks.Available;
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Uninstall() => ItemRestrictionHooks.Uninstall();
        internal static void Bind(DungeonManager dungeon, bool enabled)
        {
            if (!loaded || !Probe()) return;
            try { ItemRestrictionRuntime.Bind(dungeon, enabled); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Item restriction scope needs reset: " + error); }
        }
        internal static void SetEnabled(bool enabled) => ItemRestrictionRuntime.SetEnabled(enabled);
        internal static void Clear() => ItemRestrictionRuntime.Clear();
        internal static void Shutdown()
        {
            Clear(); // Retain journal and hooks if restoration throws; caller can retry.
            if (loaded) Uninstall();
            loaded = false;
        }
    }
}
