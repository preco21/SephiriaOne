using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Resolve saved/session intent at the native grant boundary, not the next
        // maintenance frame. No inventory effects are replayed by settings UI.
        internal static bool CollinForUse => EnsureResourceScope() && policy.CollinStartingArtifact;
        internal static bool TryExecuteCollin(bool grant, out string message)
        {
            message = L.T("Only the host can change Collin starting-artifact settings.");
            if (!NetworkServer.active) return false;
            if (grant && !CollinFeature.Available)
            { message = L.T("Collin compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("collin", !grant, out HostCommandContext context, out message)) return false;
            if (!Commit("collin", context.CreateBatch(), () => policy.RecordCollin(grant), out message)) return false;
            message = DescribeCollin(policy.CollinStartingArtifact);
            return true;
        }
        internal static string DescribeCollin(bool grant) => L.F("Collin starting artifact for Mole, Farmer Squirrel and Turtle: {0}. Applies at the next costume equip or fresh-run restock; current run inventory stays unchanged.", L.T(grant ? "on" : "off"));
    }
}
