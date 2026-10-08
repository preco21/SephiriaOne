using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        public static bool TryExecuteItemUnlock(bool unlock, out string message)
        {
            message = L.T("Only the host can change given-item restrictions.");
            if (!NetworkServer.active) return false;
            if (unlock && !ItemRestrictionFeature.Available)
            { message = L.T("Item restriction compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("items", !unlock, out HostCommandContext context, out message)) return false;
            var batch = context.CreateBatch();
            // A failed write can change its diagnostic, but must still compare as
            // pending so the shared recovery journal can retry the same operation.
            batch.Add("given-item unlock", () => ItemRestrictionFeature.Enabled == unlock && ItemRestrictionFeature.Fault == null,
                _ => ItemRestrictionFeature.SetEnabled(unlock), true);
            if (!Commit("items", batch, () => policy.RecordItemUnlock(unlock), out message)) return false;
            message = DescribeItemUnlock(policy.ItemUnlock);
            return true;
        }

        internal static string DescribeItemUnlock(bool unlock) =>
            L.F("Given-item trade/drop/sell unlock: {0}.", L.T(unlock ? "on" : "off"));
    }
}
