using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace SephiriaOne
{
    internal static class NativeAssemblyContract
    {
        internal static bool Matches(Assembly assembly, string expected)
        {
            // Some stock-client presentation adapters require a complete audited
            // code boundary, beyond the small method signatures used by hooks.
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == expected;
        }
    }
}
