using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed class MultiplayerNameColor
    {
        private readonly NetworkNameState state = new NetworkNameState();
        private PlayerAvatar player;
        private string plainName;
        private bool restoreRequired;
        private bool loggedSynchronization;

        public void Update(PlayerSpawner spawner)
        {
            PlayerAvatar avatar = spawner.PlayerAvatar;
            if (player != avatar)
            {
                Restore();
                player = avatar;
            }

            if (!CanSend() || SaveManager.Current == null || string.IsNullOrEmpty(player.playerNameSource))
            {
                return;
            }

            // This is the same profile key used by PlayerLocalDataStorage on join.
            // Never write the formatted name back to the user's profile.
            string profileName = SaveManager.Current.GetString("PlayerName", "");
            if (string.IsNullOrEmpty(profileName))
            {
                return;
            }

            plainName = NetworkNameState.Plain(profileName);
            bool multiplayer = PlayerSpawner.MultiplayerList.Count > 1;
            string observedName = player.playerNameSource;
            string request = state.Next(observedName, plainName, multiplayer);

            if (multiplayer)
            {
                restoreRequired = true;
                if (!loggedSynchronization && observedName == state.GradientName)
                {
                    loggedSynchronization = true;
                    Debug.Log("[SephiriaOne] Multiplayer name gradient synchronized (#408af1 -> #a8d7fa)");
                }
            }
            else
            {
                loggedSynchronization = false;
                if (request == null && observedName == plainName)
                {
                    restoreRequired = false;
                }
            }

            if (request != null)
            {
                // SetPlayerName uses the existing server setter for a host and
                // the game's authority-checked Command for a joining client.
                player.SetPlayerName(request);
            }
        }

        public void Restore()
        {
            if (restoreRequired && CanSend() && !string.IsNullOrEmpty(plainName))
            {
                // Queue even before a color acknowledgment, to cancel an in-flight
                // request in reliable command order when the addon unloads.
                player.SetPlayerName(plainName);
            }

            player = null;
            plainName = null;
            restoreRequired = false;
            loggedSynchronization = false;
            state.Reset();
        }

        private bool CanSend()
        {
            return player && player.isOwned && player.isClient && player.netId != 0 &&
                NetworkClient.active && NetworkClient.ready;
        }
    }
}
