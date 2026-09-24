using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed class MultiplayerNameColor
    {
        private readonly NetworkNameState state = new NetworkNameState();
        private readonly ReconciliationCoordinator<MultiplayerNameColor> coordinator = new ReconciliationCoordinator<MultiplayerNameColor>();
        private PlayerAvatar player;
        private string plainName;
        private bool restoreRequired;
        private bool multiplayer;
        private bool profileAvailable;
        public string Status { get; private set; } = "waiting for owned avatar";

        public MultiplayerNameColor()
        {
            coordinator.Register(new ReconciliationRule<MultiplayerNameColor>("owned-name", SyncDomain.Identity | SyncDomain.Names,
                SyncDomain.None, ReconcileMode.OnChange, self => self.CanSend() && self.profileAvailable && !string.IsNullOrEmpty(self.player.playerNameSource),
                self => (self.player.playerNameSource, self.plainName, self.multiplayer, self.state.RetryToken(Time.unscaledTime)),
                self => self.Publish()));
        }

        public void Update(PlayerSpawner spawner)
        {
            PlayerAvatar avatar = spawner.PlayerAvatar;
            if (player != avatar) { Restore(); player = avatar; }
            string profileName = SaveManager.Current?.GetString("PlayerName", "");
            profileAvailable = !string.IsNullOrEmpty(profileName);
            if (profileAvailable) plainName = NetworkNameState.Plain(profileName);
            multiplayer = PlayerSpawner.MultiplayerList.Count > 1;
            coordinator.Reconcile(this);
            foreach (var result in coordinator.Describe(this))
                if (result.State != ReconcileState.Applied) Status = "native name " + result.State + ": " + result.Detail;
        }

        private ReconcileResult Publish()
        {
            string request = state.Next(player.playerNameSource, plainName, multiplayer, Time.unscaledTime);
            if (multiplayer) restoreRequired = true;
            if (request != null) player.SetPlayerName(request);
            string desired = multiplayer ? state.GradientName : plainName;
            if (player.playerNameSource == desired && request == null)
            {
                if (!multiplayer) restoreRequired = false;
                Status = "native local readback matches; peer rendering unverified";
                return ReconcileResult.Applied(Status);
            }
            Status = state.Exhausted ? "native name acknowledgment timed out after 3 attempts; peer rendering unverified" : "awaiting native name readback";
            return state.Exhausted ? ReconcileResult.Suspended(Status) : ReconcileResult.Waiting(Status);
        }

        public void Restore()
        {
            if (restoreRequired && CanSend() && !string.IsNullOrEmpty(plainName)) player.SetPlayerName(plainName);
            player = null;
            plainName = null;
            restoreRequired = false;
            profileAvailable = false;
            Status = "waiting for owned avatar";
            state.Reset();
            coordinator.Clear();
        }

        private bool CanSend() => player && player.isOwned && player.isClient && player.netId != 0 && NetworkClient.active && NetworkClient.ready;
    }
}
