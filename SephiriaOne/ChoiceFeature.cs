using System;
using System.IO;
using System.Reflection;
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
                // The entry point has no Harmony types. Load the pinned Mono
                // dependency before JIT-compiling any method that uses Harmony.
                bool loaded = false;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    AssemblyName name = assembly.GetName();
                    if (name.Name == "0Harmony" && name.Version == new Version(2, 4, 2, 0)) loaded = true;
                }
                if (!loaded)
                {
                    using (Stream resource = typeof(ChoiceFeature).Assembly.GetManifestResourceStream("SephiriaOne.Dependencies.0Harmony.dll"))
                    using (var bytes = new MemoryStream())
                    {
                        if (resource == null) throw new InvalidOperationException("Embedded Harmony dependency is missing.");
                        resource.CopyTo(bytes);
                        Assembly.Load(bytes.ToArray());
                    }
                }
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
