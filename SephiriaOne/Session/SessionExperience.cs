using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static ExperienceSettings ExperienceForUse => EnsureResourceScope() ? policy.Experience : default;
        internal static bool TryExecuteExperience(ExperienceCommand command, out string message)
        {
            message = L.T("Only the host can change XP settings.");
            if (!NetworkServer.active) return false;
            if (!EnsureResourceScope()) { message = L.T("Enter a hosted session first."); return false; }
            var next = policy.Experience.Apply(command);
            bool recovery = command.Operation == ExperienceOperation.Reset || !command.Enabled || !next.Block;
            if (!recovery && !ExperienceFeature.Available)
            { message = L.T("XP compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("xp", recovery, out HostCommandContext context, out message)) return false;
            if (!Commit("xp", context.CreateBatch(), () => policy.RecordExperience(next), out message)) return false;
            message = DescribeExperience(next);
            return true;
        }
        internal static string DescribeExperience(ExperienceSettings settings) =>
            L.F("Block XP: {0}. Wing-Eared Rabbit exception: {1}. Applies immediately to new XP; existing levels and XP stay unchanged.",
                L.T(settings.Block ? "on" : "off"), L.T(settings.RabbitExempt ? "on" : "off"));
    }
}
