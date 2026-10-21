using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private const int ArtifactRateEditorHeight = 112;
        private TMP_InputField pvpArtifactInterval;
        private Button pvpArtifactReset;
        private bool pvpArtifactDraft;
        private float pvpArtifactLast = -1;

        private void BuildPvpArtifactRate(RectTransform content)
        {
            AddCheckbox("Autonomous artifact hit limit", 4, 0, 250, 25, s => s.FriendlyFire.ArtifactRate.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire artifact-rate", parent: content);
            widgets.Text(content, "ArtifactIntervalLabel", "Minimum hit interval (s)", 4, 28, 250, 18, 11);
            pvpArtifactInterval = widgets.Input(content, 4, 50, 56, _ => pvpArtifactDraft = true);
            changeButtons.Add(widgets.Button(content, "Apply", 66, 50, 62, 25, ApplyPvpArtifactRate));
            pvpArtifactReset = widgets.Button(content, "Reset", 134, 50, 120, 25, ResetPvpArtifactRate);
            widgets.Text(content, "ArtifactIntervalHint", "Per artifact/opponent; Dark Cloud uses longer limit.", 4, 80, 250, 26, 10);
        }

        private void RefreshPvpArtifactRate(SettingsSnapshot snapshot)
        {
            if (!pvpArtifactInterval) return;
            pvpArtifactInterval.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            pvpArtifactReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                (snapshot.FaultedFeature == "combat" || snapshot.FaultedFeature == "inheritance"));
            float interval = snapshot.FriendlyFire.ArtifactRate.Interval;
            if (pvpArtifactDraft || pvpArtifactLast == interval) return;
            pvpArtifactInterval.SetTextWithoutNotify(snapshot.FriendlyFire.ArtifactRate.Number);
            pvpArtifactLast = interval;
        }
        private void ApplyPvpArtifactRate() => Execute("/one friendlyfire artifact-rate interval " + pvpArtifactInterval.text.Trim(), true,
            clearInputOnSuccess: ClearPvpArtifactRate);
        private void ResetPvpArtifactRate() => Execute("/one friendlyfire artifact-rate reset", true,
            clearInputOnSuccess: ClearPvpArtifactRate);
        private void ClearPvpArtifactRate()
        {
            pvpArtifactDraft = false; pvpArtifactLast = -1;
            if (pvpArtifactInterval) pvpArtifactInterval.SetTextWithoutNotify("");
        }
    }
}
