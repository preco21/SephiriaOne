using System.Runtime.CompilerServices;

namespace SephiriaOne
{
    // Combat fixtures model the participant service boundary. Native connection
    // identity/readiness is exercised by the separate shared runtime suite.
    internal static class FriendlyFireParticipants
    {
        internal sealed class Receipt { internal PlayerAvatar Player; internal bool Current = true; internal long Scope; }
        private static ConditionalWeakTable<PlayerAvatar, Receipt> receipts = new();
        private static readonly HashSet<PlayerAvatar> selected = new();
        internal static long Epoch { get; private set; }
        private static long scope;
        internal static Receipt Capture(PlayerAvatar player) => player ? receipts.GetValue(player, p => new() { Player = p, Scope = scope }) : null;
        internal static bool IsCurrent(Receipt receipt) => receipt != null && receipt.Current && receipt.Scope == scope && receipt.Player;
        internal static bool Includes(PlayerAvatar player) => IsCurrent(Capture(player)) && (selected.Count == 0 || selected.Contains(player));
        internal static void Select(params PlayerAvatar[] players) { selected.Clear(); foreach (var player in players) selected.Add(player); Epoch++; }
        internal static void Depart(PlayerAvatar player) { Capture(player).Current = false; selected.Remove(player); Epoch++; }
        internal static void Clear() { receipts = new(); selected.Clear(); scope++; Epoch++; }
    }
    internal static class FriendlyFireParticipantHooks { internal static void Install(HarmonyLib.Harmony harmony) { } }
}
