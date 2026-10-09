using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Same event-time scope resolution as potions. Never cache per-player
        // policy or rely on the joining connection's prior incarnation.
        internal static FriendlyFireSettings FriendlyFireForHit => EnsureResourceScope() ? policy.FriendlyFire : default;

        internal static bool TryExecuteFriendlyFire(FriendlyFireCommand command, out string message)
        {
            message = L.T("Only the host can change friendly fire.");
            if (!NetworkServer.active) return false;
            if (!command.IsReset && !FriendlyFireFeature.Available)
            { message = L.T("Friendly-fire compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("combat", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("combat", context.CreateBatch(), () =>
                { policy.Record(command); FriendlyFireKda.SetEnabled(policy.FriendlyFire.Enabled); }, out message)) return false;
            message = DescribeFriendlyFire(policy.FriendlyFire);
            return true;
        }

        internal static string DescribeFriendlyFire(FriendlyFireSettings settings) =>
            L.F("Friendly fire: {0}; allied damage: {1}%.", L.T(settings.Enabled ? "on" : "off"), settings.DamagePercent);
    }
}
