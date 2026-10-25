using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button tournamentStart, tournamentStop, tournamentApply, tournamentResetKda;
        private void BuildTournamentEditor()
        {
            widgets.Text(pageRoot, "TournamentTitle", "Tournament", 16, 0, 273, 24, 14);
            widgets.Text(pageRoot, "TournamentUnits", "Round duration in seconds (10–3600)", 16, 29, 273, 21, 10);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            tournamentApply = widgets.Button(pageRoot, "Apply", 120, 56, 169, 25,
                () => Execute("/one tournament duration " + draft.Text, true));
            tournamentStart = widgets.Button(pageRoot, "Start match", 16, 94, 132, 28, () => Execute("/one tournament start", true));
            tournamentStop = widgets.Button(pageRoot, "Stop match", 156, 94, 133, 28, () => Execute("/one tournament stop", true));
            tournamentResetKda = widgets.Button(pageRoot, "Reset K/D/A", 16, 130, 273, 28,
                () => Execute("/one tournament resetkda", true));
            BuildParticipantsEditor();
        }
        private void RefreshTournament(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            tournamentStart.interactable = host && CombatMatchRuntime.CanStartTournament;
            tournamentStop.interactable = host && CombatMatchRuntime.IsRunning && CombatMatchRuntime.IsTournament;
            tournamentResetKda.interactable = host && SessionSettings.CanResetFriendlyFireScores;
            tournamentApply.interactable = amount.interactable = host;
            availability.text = CombatMatchRuntime.IsTournament ? CombatMatchRuntime.DescribeTournament() : "";
            availability.color = PanelWidgets.Muted;
            if (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available)
                availability.text = L.T("Tournament compatibility checks failed. Stop remains available; see Player.log.");
            RefreshParticipants(snapshot);
        }
    }
}
