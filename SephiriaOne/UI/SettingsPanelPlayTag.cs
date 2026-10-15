using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button playTagStart, playTagStop, playTagApply, playTagResetDuration, playTagResetKda;
        private void BuildPlayTagEditor()
        {
            widgets.Text(pageRoot, "PlayTagTitle", "Play Tag", 16, 0, 273, 24, 14);
            widgets.Text(pageRoot, "PlayTagUnits", "Initial fuse in seconds (3–3600)", 16, 29, 273, 21, 10);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            playTagApply = widgets.Button(pageRoot, "Apply", 120, 56, 80, 25,
                () => Execute("/one tag duration " + draft.Text, true));
            playTagResetDuration = widgets.Button(pageRoot, "Reset", 208, 56, 81, 25,
                () => Execute("/one tag duration reset", true));
            playTagStart = widgets.Button(pageRoot, "Start match", 16, 94, 132, 28, () => Execute("/one tag start", true));
            playTagStop = widgets.Button(pageRoot, "Stop match", 156, 94, 133, 28, () => Execute("/one tag stop", true));
            playTagResetKda = widgets.Button(pageRoot, "Reset K/D/A", 16, 130, 273, 28,
                () => Execute("/one tag resetkda", true));
            BuildParticipantsEditor();
        }
        private void RefreshPlayTag(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            playTagStart.interactable = host && CombatMatchRuntime.CanStartPlayTag;
            playTagStop.interactable = host && CombatMatchRuntime.IsPlayTag;
            playTagResetKda.interactable = host && SessionSettings.CanResetFriendlyFireScores;
            playTagApply.interactable = playTagResetDuration.interactable = amount.interactable = host;
            availability.text = CombatMatchRuntime.DescribePlayTag();
            availability.color = PanelWidgets.Muted;
            if (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available)
                availability.text = L.T("Play Tag compatibility checks failed. Stop remains available; see Player.log.");
            RefreshParticipants(snapshot);
        }
    }
}
