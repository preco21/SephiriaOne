using System;
using Mirror;

namespace SephiriaOne
{
    internal static class LobbyRegenRuntime
    {
        internal static bool Suppress(UnitAvatar unit)
        {
            if (!LobbyRegenFeature.Available || !FriendlyFireFeature.Available || !NetworkServer.active || !(unit is PlayerAvatar player) ||
                !player || !player.isServer || player.IsDead) return false;
            var settings = SessionSettings.FriendlyFireForHit;
            if (!settings.Enabled || !settings.BlockLobbyRegen) return false;
            var dungeon = DungeonManager.Instance;
            var race = player.Race;
            // Use this player's actual generated stage, not a global run flag or
            // safe-floor flag (which also covers non-lobby dungeon locations).
            if (!dungeon || !dungeon.isServer || !race || !race.lobbyStage || string.IsNullOrEmpty(player.currentFloorGuid) ||
                !dungeon.generatedFloors.TryGetValue(player.currentFloorGuid, out var floor) || floor == null ||
                !string.Equals(floor.stageName, race.lobbyStage.name, StringComparison.Ordinal)) return false;
            return PvpParticipation.IsActive(player);
        }

        internal static void HealPlace(UnitAvatar unit, float amount, bool allowOverHeal, bool ignorePenalty)
        { if (!Suppress(unit)) unit.Heal(amount, allowOverHeal, ignorePenalty); }
        internal static void HealPassive(UnitAvatar unit, float amount)
        { if (!Suppress(unit)) unit.Heal(amount); }
        internal static void HealMpPassive(UnitAvatar unit, int amount)
        { if (!Suppress(unit)) unit.HealMp(amount); }
    }
}
