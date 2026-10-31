namespace SephiriaOne
{
    // Native presentation executes in the dedicated visual adapter fixtures;
    // this ledger verifies the real match owns its creation and cleanup.
    internal static class CombatRoleVisuals
    {
        internal static readonly HashSet<PlayerAvatar> Visible = new();
        internal static Action AfterUpdate;
        internal static void Update(PlayerAvatar player)
        {
            if (!player) return;
            if (CombatMatchRuntime.IsCombatRoleVisualCurrent(player, FriendlyFireParticipants.Capture(player), CombatMatchRuntime.MatchToken)) Visible.Add(player);
            else Visible.Remove(player);
            var callback = AfterUpdate; AfterUpdate = null; callback?.Invoke();
        }
        internal static void Clear() => Visible.Clear();
        internal static void Shutdown() => Clear();
    }
}
