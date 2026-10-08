using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static JarSpawnSettings JarSpawnsForGeneration => EnsureResourceScope() ? policy.JarSpawns : default;

        internal static bool TryExecuteJarSpawn(JarSpawnSettings settings, out string message)
        {
            message = L.T("Only the host can change Mystic Jar spawn rates.");
            if (!NetworkServer.active) return false;
            if (settings.HasChanges && !JarSpawnFeature.Available)
            { message = L.T("Mystic Jar compatibility checks failed. Reset remains available; see Player.log."); return false; }
            if (!PrepareCommand("jars", !settings.HasChanges, out HostCommandContext context, out message)) return false;
            if (!Commit("jars", context.CreateBatch(), () => policy.RecordJarSpawns(settings), out message)) return false;
            message = DescribeJarSpawns(policy.JarSpawns);
            return true;
        }
        internal static string DescribeJarSpawns(JarSpawnSettings settings) => !settings.HasChanges ?
            L.T("Mystic Jar spawn chance: native (no override).") : settings.Mode == JarSpawnMode.Chance ?
            L.F("Mystic Jar spawn chance: {0}% at each eligible random location.", settings.Number) :
            L.F("Mystic Jar spawn chance: native x{0}, capped at 100% per eligible random location.", settings.Number);
    }
}
