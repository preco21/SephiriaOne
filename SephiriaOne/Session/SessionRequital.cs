using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static RequitalSettings Requital => EnsureResourceScope() ? policy.Requital : default;

        internal static bool TryExecuteRequital(RequitalCommand command, out string message)
        {
            message = L.T("Only the host can change Requital Slate settings.");
            if (!NetworkServer.active) return false;
            if (!EnsureResourceScope())
            { message = L.T("Enter a hosted session first."); return false; }
            var next = policy.Requital.Apply(command);
            bool reduction = !next.Automatic || next.Count == 0 ||
                policy.Requital.Automatic && next.Count <= policy.Requital.Count;
            if (!reduction && !RequitalRuntime.Available)
            { message = L.T("Requital compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("slates", reduction, out HostCommandContext context, out message)) return false;
            if (!Commit("slates", context.CreateBatch(), () => policy.RecordRequital(next), out message)) return false;
            RequitalRuntime.Tick();
            message = DescribeRequital(next);
            return true;
        }

        internal static string DescribeRequital(RequitalSettings settings) =>
            L.F("Requital Slates: auto {0}, {1} per player. Starting loadout only; Give now adds one-time copies.",
                L.T(settings.Automatic ? "on" : "off"), settings.Count);
    }
}
