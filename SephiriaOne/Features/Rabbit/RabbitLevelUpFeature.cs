using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    // Bootstrap Harmony before JIT compilation touches any patch types.
    internal static class RabbitLevelUpFeature
    {
        private static bool loaded;
        public static bool Available => loaded && RabbitLevelUpCatalog.Available && Probe();

        public static void Initialize()
        {
            if (loaded) return;
            try
            {
                HarmonyRuntime.EnsureLoaded();
                loaded = true;
                Install();
            }
            catch (Exception error)
            { loaded = false; Debug.LogWarning("[SephiriaOne] Rabbit level-up runtime unavailable: " + error); }
        }

        public static void OnDatabasesReady() => RabbitLevelUpCatalog.Load();

        public static void Shutdown()
        {
            RabbitLevelUpCatalog.Clear();
            if (!loaded) return;
            Uninstall();
            loaded = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install() => RabbitLevelUpHooks.Install();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Probe() => RabbitLevelUpHooks.Available;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Uninstall() => RabbitLevelUpHooks.Uninstall();
    }
}
