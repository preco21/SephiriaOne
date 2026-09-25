using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    // Keep Harmony types outside this facade so its initializer can bootstrap the
    // embedded runtime before the CLR JIT compiles any patch code.
    internal static class RabbitPotionFeature
    {
        private static bool loaded;
        public static bool Available => loaded && Probe();

        public static void Initialize()
        {
            if (Available) return;
            try
            {
                HarmonyRuntime.EnsureLoaded();
                loaded = true;
                Install();
            }
            catch (Exception error)
            { loaded = false; Debug.LogWarning("[SephiriaOne] Rabbit potion runtime unavailable: " + error); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => RabbitPotionNativeHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Probe() => RabbitPotionNativeHooks.Available;

        public static void Shutdown()
        {
            if (!loaded) return;
            Uninstall();
            loaded = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Uninstall() => RabbitPotionNativeHooks.Uninstall();
    }
}
