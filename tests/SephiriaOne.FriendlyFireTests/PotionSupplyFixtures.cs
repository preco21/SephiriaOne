namespace SephiriaOne
{
    // The shared runtime suite exercises actual inventory reconciliation and
    // native revival completion. Combat-only fixtures do not own inventories.
    internal static class PvpPotionSupplies
    {
        internal static long BeginRevive(FriendlyFireParticipants.Receipt receipt) => -1;
        internal static void Revived(FriendlyFireParticipants.Receipt receipt, long revision, long match) { }
    }
}
