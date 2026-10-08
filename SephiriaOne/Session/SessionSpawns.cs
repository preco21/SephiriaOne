using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static EventSpawnSettings EventSpawnsForGeneration => EnsureResourceScope() ? policy.EventSpawns : default;

        internal static bool TryExecuteEventSpawn(EventSpawnSettings settings, out string message)
        {
            message = L.T("Only the host can change random event rates.");
            if (!NetworkServer.active) return false;
            if (settings.HasChanges && !EventSpawnFeature.Available)
            { message = L.T("Random event compatibility checks failed. Reset remains available; see Player.log."); return false; }
            if (!PrepareCommand("events", !settings.HasChanges, out HostCommandContext context, out message)) return false;
            if (!Commit("events", context.CreateBatch(), () => policy.RecordEventSpawns(settings), out message)) return false;
            message = DescribeEventSpawns(policy.EventSpawns);
            return true;
        }
        internal static string DescribeEventSpawns(EventSpawnSettings settings) => (!settings.HasChanges ?
            L.T("Random event room chance: native (no override).") :
            L.F("Random event room chance: native x{0}; each probability capped at 100%, at most two event rooms.", settings.Number)) +
            " " + L.T("Applies when new chapter floor data is generated. Already-generated floors keep their encounters.");

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
