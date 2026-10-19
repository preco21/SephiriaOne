using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal struct FriendlyFireSettings { public bool Enabled, Balance; }
    internal static class FriendlyFireFeature { internal static bool Available = true; }
    internal static class FriendlyFireParticipants
    {
        internal static readonly HashSet<PlayerAvatar> Selected = new();
        internal static bool Includes(PlayerAvatar player, bool requireReady = true) =>
            (Selected.Count == 0 || Selected.Contains(player)) && HostStateAdapter.IsReady(player.spawner);
    }
    internal static class CombatMatchRuntime
    {
        internal static Func<PlayerAvatar, bool> ParticipantRule;
        internal static bool AllowsParticipant(PlayerAvatar player) => ParticipantRule?.Invoke(player) ?? true;
    }
}
