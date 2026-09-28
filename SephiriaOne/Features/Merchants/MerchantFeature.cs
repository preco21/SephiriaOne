using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class MerchantFeature
    {
        private static bool loaded;
        public static bool Available => loaded && Probe();

        public static void Initialize()
        {
            if (loaded) return;
            try
            {
                HarmonyRuntime.EnsureLoaded();
                Install();
                loaded = true;
                HorayModAPI.OnFloorAllocatedClientside += MerchantRuntime.OnFloorReady;
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Merchant runtime unavailable: " + error); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => MerchantNativeHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Probe() => MerchantNativeHooks.Available;

        public static void Refresh() { if (Available) MerchantRuntime.Refresh(); }
        public static void Clear() => MerchantRuntime.Clear();

        public static void Shutdown()
        {
            // If destruction fails, retain the hooks and provenance for surviving actors.
            Clear();
            if (!loaded) return;
            HorayModAPI.OnFloorAllocatedClientside -= MerchantRuntime.OnFloorReady;
            Uninstall();
            loaded = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Uninstall() => MerchantNativeHooks.Uninstall();
    }
}
