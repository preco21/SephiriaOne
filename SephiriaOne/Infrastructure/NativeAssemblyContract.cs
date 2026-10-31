using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace SephiriaOne
{
    internal static class NativeAssemblyContract
    {
        internal static bool MatchesPresentationPair(Assembly game, Assembly mirror)
        {
            // A presentation carrier is safe only with the audited game AND
            // network implementation. Never accept a mixture of known builds.
            string gameHash = Hash(game), mirrorHash = Hash(mirror);
            return (gameHash == "C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510" &&
                    mirrorHash == "C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440") ||
                   (gameHash == "383A0D5EA9218B797D6EACD1F67964A73606BA50572B7AF2D10AC5357B621D01" &&
                    mirrorHash == "E0BB5E72F9BC63DD13CD344DE0AA4DA07900BA15C60B5C0DB283E6DBDF02906E");
        }

        internal static bool Matches(Assembly assembly, string expected)
            => Hash(assembly) == expected;

        private static string Hash(Assembly assembly)
        {
            // Some stock-client presentation adapters require a complete audited
            // code boundary, beyond the small method signatures used by hooks.
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
