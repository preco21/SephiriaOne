using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static bool MerchantSpawnsForUse => EnsureResourceScope() && policy.MerchantSpawns;
        internal static int MerchantSpawnChanceForUse => EnsureResourceScope() ? policy.MerchantSpawnChance : MerchantCommand.DefaultChance;

        public static bool TryExecuteMerchant(MerchantCommand command, out string message)
        {
            message = L.T("Only the host can change Wandering Merchant settings.");
            if (!NetworkServer.active) return false;
            if (!command.IsReset && !MerchantFeature.Available)
            { message = L.T("Merchant compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("merchant", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("merchant", context.CreateBatch(), () => policy.Record(command), out message)) return false;
            MerchantFeature.Refresh();
            message = DescribeMerchant(policy.MerchantSpawns, policy.MerchantSpawnChance);
            return true;
        }

        internal static string DescribeMerchant(bool enabled, int chance = MerchantCommand.DefaultChance) =>
            L.F("Extra Wandering Merchant: {0}; while enabled, one guaranteed per run on a randomly selected eligible floor, including the first. Other eligible floors have a {1}% chance before or after the guaranteed encounter (at most one per floor). Excludes boss-only floors, lobby, towns, and training. Added merchants are hostile with 1x normal HP, cannot talk, and have no negotiation/crime penalty; natural merchants are unchanged. Off stops future spawns; reset also restores 25%. Existing added merchants stay exempt until floor teardown.", L.T(enabled ? "on" : "off"), chance);
    }
}
