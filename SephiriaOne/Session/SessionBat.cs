using System;
using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static bool BatReductionForUse => EnsureResourceScope() && BatCostumeFeature.Available && policy.BatHpSteal;
        internal static bool TryExecuteBat(bool reduce, out string message)
        {
            message = L.T("Only the host can change Wingless Bat settings.");
            if (!NetworkServer.active) return false;
            if (reduce && !BatCostumeFeature.Available)
            { message = L.T("Bat costume compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("bat", !reduce, out HostCommandContext context, out message)) return false;
            var batch = context.CreateBatch();
            if (BatCostumeFeature.Available)
                foreach (PlayerAvatar player in context.Players) BatCostumeRuntime.Append(batch, player, reduce);
            if (!Commit("bat", batch, () => policy.RecordBat(reduce), out message)) return false;
            message = DescribeBat(policy.BatHpSteal);
            return true;
        }
        internal static string DescribeBat(bool reduce) => reduce ?
            L.T("Wingless Bat costume HP steal: 1 (reduction on; native 5). Other HP-steal sources are unchanged.") :
            L.T("Wingless Bat costume HP steal: native 5 (reduction off).");
        // Each native pair stays consistent even after a setter failure. Restore
        // owned instances directly; obsolete session journals cannot be replayed
        // across authority/session changes and must not block scope teardown.
        internal static void RestoreBat()
        {
            BatCostumeRuntime.Clear();
            // All Bat-owned native state is now restored, including a partially
            // completed multi-player command. Retire only this family's journal
            // so unrelated shutdown services can run; never dismiss another
            // feature's or inheritance's incomplete writes.
            if (failedFeature == "bat")
            {
                failedBatch = null; failedFeature = null; failedReason = null; recovered = null;
            }
            policy.RecordBat(false);
        }
        internal static void BeforeBatShutdown()
        {
            if (failedBatch != null && failedFeature != "bat")
                throw new InvalidOperationException(L.F("Resolve the faulted {0} write before unloading the addon. Inspect /one status.", failedFeature));
        }
    }
}
