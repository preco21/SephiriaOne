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
        private string profileSource, diagnosticDetail, diagnosticText;
        private ReconcileState diagnosticState;
        private bool restoreRequired;
        private bool multiplayer;
        private bool profileAvailable;
        private string status = "waiting for owned avatar";
        private string translatedSource, translatedStatus;
        private int translatedRevision = -1;
        public string Status
        {
            get
            {
                if (translatedSource == status && translatedRevision == L.Revision) return translatedStatus;
                translatedSource = status;
                translatedRevision = L.Revision;
                translatedStatus = ReferenceEquals(status, diagnosticText)
                    ? L.F("native name {0}: {1}", L.T(diagnosticState.ToString()), TranslateDetail(diagnosticDetail))
                    : TranslateDetail(status);
                return translatedStatus;
            }
        }

        public MultiplayerNameColor()
        {
            coordinator.Register(ReconciliationRule<MultiplayerNameColor>.ObserveValue("owned-name", SyncDomain.Identity | SyncDomain.Names,
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
            if (profileAvailable && profileSource != profileName)
            { profileSource = profileName; plainName = NetworkNameState.Plain(profileName); }
            multiplayer = PlayerSpawner.MultiplayerList.Count > 1;
            coordinator.Reconcile(this);
            if (coordinator.TryGetResult(this, "owned-name", out var result) && result.State != ReconcileState.Applied)
            {
                if (diagnosticText == null || diagnosticState != result.State || diagnosticDetail != result.Detail)
                {
                    diagnosticState = result.State; diagnosticDetail = result.Detail;
                    diagnosticText = "native name " + result.State + ": " + result.Detail;
                }
                status = diagnosticText;
            }
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
                status = "native local readback matches; peer rendering unverified";
                return ReconcileResult.Applied(status);
            }
            status = state.Exhausted ? "native name acknowledgment timed out after 3 attempts; peer rendering unverified" : "awaiting native name readback";
            return state.Exhausted ? ReconcileResult.Suspended(status) : ReconcileResult.Waiting(status);
        }

        public void Restore()
        {
            if (restoreRequired && CanSend() && !string.IsNullOrEmpty(plainName)) player.SetPlayerName(plainName);
            player = null;
            plainName = null;
            profileSource = diagnosticDetail = diagnosticText = null;
            restoreRequired = false;
            profileAvailable = false;
            status = "waiting for owned avatar";
            state.Reset();
            coordinator.Clear();
        }

        // Reconciliation retains raw details. Translate only owned messages at the
        // presentation boundary, leaving arbitrary native exception text intact.
        private static string TranslateDetail(string detail)
        {
            switch (detail)
            {
                case "waiting for owned avatar": return L.T("waiting for owned avatar");
                case "native local readback matches; peer rendering unverified":
                    return L.T("native local readback matches; peer rendering unverified");
                case "native name acknowledgment timed out after 3 attempts; peer rendering unverified":
                    return L.T("native name acknowledgment timed out after 3 attempts; peer rendering unverified");
                case "awaiting native name readback": return L.T("awaiting native name readback");
                case "Not observed yet.": return L.T("Not observed yet.");
                case "Required state or authority is not ready.": return L.T("Required state or authority is not ready.");
                default: return detail;
            }
        }

        private bool CanSend() => player && player.isOwned && player.isClient && player.netId != 0 && NetworkClient.active && NetworkClient.ready;
    }
}
