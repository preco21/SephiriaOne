using System;
using System.IO;
using System.Reflection;

namespace SephiriaOne
{
    internal static class HarmonyRuntime
    {
        // Keep callers free of Harmony types until the embedded Mono build is loaded.
        public static void EnsureLoaded()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName name = assembly.GetName();
                if (name.Name == "0Harmony" && name.Version == new Version(2, 4, 2, 0)) return;
            }
            using (Stream resource = typeof(HarmonyRuntime).Assembly.GetManifestResourceStream("SephiriaOne.Dependencies.0Harmony.dll"))
            using (var bytes = new MemoryStream())
            {
                if (resource == null) throw new InvalidOperationException("Embedded Harmony dependency is missing.");
                resource.CopyTo(bytes);
                Assembly.Load(bytes.ToArray());
            }
        }
    }
}
