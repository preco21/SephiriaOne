using System.Text;
using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // These are temporary host permissions, not native stat writes. Loading
        // or a fault in an unrelated stat feature must not prevent revocation.
        private static bool SelectionScope(out string message)
        {
            message = L.T("Only the host can change friendly fire.");
            if (!NetworkServer.active) return false;
            message = L.T("Host session data is not ready. Enter town or a run first.");
            return EnsureResourceScope();
        }

        internal static bool TrySelectFriendlyFirePlayer(uint id, bool selected, object expected, out string message)
        {
            if (!SelectionScope(out message)) return false;
            if (selected && !FriendlyFireFeature.Available)
            { message = L.T("Friendly-fire compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            PlayerAvatar player = null;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
                if (spawner && spawner.PlayerAvatar && spawner.PlayerAvatar.netId == id) { player = spawner.PlayerAvatar; break; }
            var receipt = FriendlyFireParticipants.Capture(player);
            message = L.T("Player connection changed. Refresh the player list and try again.");
            if (receipt == null || expected != null && !ReferenceEquals(receipt, expected)) return false;
            DeathmatchRuntime.Stop(true, true);
            // Recovery can run arbitrary native callbacks: reject a replacement
            // connection even if the network ID or UI row has been reused.
            if (!FriendlyFireParticipants.Set(receipt, selected)) return false;
            DeathmatchChanged();
            message = L.F("Player {0}: {1}. {2}", CombatNames.Player(player), L.T(selected ? "selected" : "not selected"), DescribeFriendlyFireSelection());
            return true;
        }

        internal static bool TrySelectAllFriendlyFirePlayers(out string message)
        {
            if (!SelectionScope(out message)) return false;
            if (!FriendlyFireFeature.Available)
            { message = L.T("Friendly-fire compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            var receipts = new List<FriendlyFireParticipants.Receipt>();
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                var receipt = spawner ? FriendlyFireParticipants.Capture(spawner.PlayerAvatar) : null;
                if (receipt != null && !receipts.Contains(receipt)) receipts.Add(receipt);
            }
            DeathmatchRuntime.Stop(true, true);
            message = L.T("Player connection changed. Refresh the player list and try again.");
            var current = new HashSet<FriendlyFireParticipants.Receipt>();
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                var receipt = spawner ? FriendlyFireParticipants.Capture(spawner.PlayerAvatar) : null;
                if (receipt == null) continue;
                if (!receipts.Contains(receipt)) return false;
                current.Add(receipt);
            }
            if (current.Count == 0 || current.Count != receipts.Count || !FriendlyFireParticipants.SetAll(receipts)) return false;
            DeathmatchChanged(); message = DescribeFriendlyFireSelection(); return true;
        }

        internal static bool TryResetFriendlyFireSelection(out string message)
        {
            if (!SelectionScope(out message)) return false;
            var currentDungeon = DungeonManager.Instance; var currentRun = SaveManager.CurrentRun; long currentGeneration = ResourceGeneration;
            DeathmatchRuntime.Stop(true, true);
            message = L.T("Session changed. Review the values and enter the action again.");
            if (!EnsureResourceScope() || !ReferenceEquals(currentDungeon, DungeonManager.Instance) ||
                !ReferenceEquals(currentRun, SaveManager.CurrentRun) || currentGeneration != ResourceGeneration) return false;
            FriendlyFireParticipants.ResetSelection(); DeathmatchChanged();
            message = L.T("Selection reset. All players participate while friendly fire or deathmatch is enabled.");
            return true;
        }

        internal static string DescribeFriendlyFireSelection() => FriendlyFireParticipants.SelectedCount == 0 ?
            L.T("Participants: everyone (no selection).") : L.F("Participants: {0} selected players only.", FriendlyFireParticipants.SelectedCount);

        internal static bool TryDescribeFriendlyFirePlayers(out string message)
        {
            if (!SelectionScope(out message)) return false;
            var text = new StringBuilder(DescribeFriendlyFireSelection());
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || FriendlyFireParticipants.Capture(spawner.PlayerAvatar) == null) continue;
                text.Append('\n').Append(L.F("{0}: {1} — {2}", spawner.PlayerAvatar.netId,
                    CombatNames.Player(spawner.PlayerAvatar), L.T(FriendlyFireParticipants.IsSelected(spawner.PlayerAvatar) ? "selected" : "not selected")));
            }
            text.Append('\n').Append(FriendlyFireSelectionCommand.Usage);
            message = text.ToString(); return true;
        }
    }
}
