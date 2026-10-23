using System.Text;
using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Scope installation precedes preset refresh; do not refresh from this getter.
        internal static ProjectileLimitSettings ProjectilesForUse => EnsureResourceScope() ? policy.Projectiles : default;

        internal static bool TryExecuteProjectiles(ProjectileLimitCommand command, out string message)
        {
            message = L.T("Only the host can change projectile limits.");
            if (!NetworkServer.active) return false;
            if (!EnsureResourceScope()) { message = L.T("Enter a hosted session first."); return false; }
            bool reset = command.Operation == ProjectileLimitOperation.Reset;
            bool recovery = reset || command.Operation == ProjectileLimitOperation.RateReset ||
                ((command.Operation == ProjectileLimitOperation.Toggle || command.Operation == ProjectileLimitOperation.RateToggle) && !command.Enabled);
            if (!recovery && !ProjectileLimitFeature.Available)
            { message = L.T("Projectile compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("projectiles", recovery, out HostCommandContext context, out message, reset)) return false;
            var next = command.Apply(policy.Projectiles);
            if (!Commit("projectiles", context.CreateBatch(), () => policy.RecordProjectiles(next), out message)) return false;
            ProjectileLimitRuntime.Refresh();
            message = DescribeProjectiles(next);
            return true;
        }

        internal static string DescribeProjectiles(ProjectileLimitSettings settings) =>
            L.F("Secondary projectiles: {0} · total/player {1} · default/source {2}.",
                L.T(settings.Enabled ? "on" : "off"), settings.PlayerLimit, settings.DefaultSourceLimit) + "\n" +
            L.F("Spawn rate: {0} · {1}/player/s · {2}/source/s · stores 1 second, refills continuously.",
                L.T(settings.RateEnabled ? "on" : "off"), settings.PlayerRate, settings.SourceRate);

        internal static string DescribeProjectileSources(ProjectileLimitSettings settings)
        {
            var text = new StringBuilder(DescribeProjectiles(settings));
            foreach (var source in ProjectileLimitCatalog.All)
            {
                text.Append('\n').Append(L.T(source.Label)).Append(" [").Append(source.Id).Append("]: ")
                    .Append(settings.GetSourceLimit(source.Id));
                if (!settings.SourceLimits.ContainsKey(source.Id)) text.Append(' ').Append(L.T("(default)"));
            }
            return text.ToString();
        }
    }
}
