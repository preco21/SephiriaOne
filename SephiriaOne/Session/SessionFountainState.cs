using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private static readonly HashSet<PlayerAvatar> fountainPlayers =
            new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);

        private static bool IsFountainEnrolled(PlayerAvatar player) => fountainPlayers.Contains(player) ||
            (player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution) && contribution != 0);

        private static object CaptureFountain(PlayerAvatar player)
        {
            bool enrolled = IsFountainEnrolled(player);
            player.customStats.TryGetValue(FountainPoints.ContributionKey, out int contribution);
            return (enrolled, enrolled ? player.Inventory : null,
                enrolled ? player.Inventory.dimensionPocket : 0, enrolled ? contribution : 0);
        }
    }
}
