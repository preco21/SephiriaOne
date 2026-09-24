using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private readonly struct FountainObservation
        {
            public GridInventory Inventory { get; }
            public int Points { get; }
            public int Contribution { get; }
            public FountainObservation(GridInventory inventory, int contribution)
            {
                Inventory = inventory;
                Points = inventory.dimensionPocket;
                Contribution = contribution;
            }
            public bool Matches(FountainObservation other) => ReferenceEquals(Inventory, other.Inventory) &&
                Points == other.Points && Contribution == other.Contribution;
        }

        private static readonly Dictionary<uint, FountainObservation> fountainState = new Dictionary<uint, FountainObservation>();
        private static readonly HashSet<uint> fountainPlayers = new HashSet<uint>();

        private static void ObserveFountain(PlayerAvatar player, bool detectChanges = true)
        {
            player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution);
            if (!fountainPlayers.Contains(player.netId) && contribution == 0)
            {
                fountainState.Remove(player.netId);
                return;
            }
            var current = new FountainObservation(player.Inventory, contribution);
            if (detectChanges && (!fountainState.TryGetValue(player.netId, out FountainObservation previous) || !previous.Matches(current)))
                restoreFountainLimit = true;
            fountainState[player.netId] = current;
        }
    }
}
