using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private const int ArtifactRangeEditorHeight = 90;
        private TMP_InputField pvpArtifactRange;
        private Button pvpArtifactRangeReset;
        private bool pvpArtifactRangeDraft;
        private decimal pvpArtifactRangeLast = -1;

        private void BuildPvpArtifactRange(RectTransform content)
        {
            widgets.Text(content, "ArtifactRangeLabel", "Automatic artifact range (m)", 4, 0, 250, 20, 11);
            pvpArtifactRange = widgets.Input(content, 4, 24, 56, _ => pvpArtifactRangeDraft = true);
            changeButtons.Add(widgets.Button(content, "Apply", 66, 24, 62, 25, ApplyPvpArtifactRange));
            pvpArtifactRangeReset = widgets.Button(content, "Reset", 134, 24, 120, 25, ResetPvpArtifactRange);
            widgets.Text(content, "ArtifactRangeHint", "From owner; includes Dark Cloud. Balance on only.", 4, 54, 250, 30, 10);
        }

        private void RefreshPvpArtifactRange(SettingsSnapshot snapshot)
        {
            if (!pvpArtifactRange) return;
            pvpArtifactRange.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            pvpArtifactRangeReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                (snapshot.FaultedFeature == "combat" || snapshot.FaultedFeature == "inheritance"));
            var range = snapshot.FriendlyFire.ArtifactRange;
            if (pvpArtifactRangeDraft || pvpArtifactRangeLast == range.Meters) return;
            pvpArtifactRange.SetTextWithoutNotify(range.Number);
            pvpArtifactRangeLast = range.Meters;
        }

        private void ApplyPvpArtifactRange() => Execute("/one friendlyfire artifact-range " + pvpArtifactRange.text.Trim(), true,
            clearInputOnSuccess: ClearPvpArtifactRange);
        private void ResetPvpArtifactRange() => Execute("/one friendlyfire artifact-range reset", true,
            clearInputOnSuccess: ClearPvpArtifactRange);
        private void ClearPvpArtifactRange()
        {
            pvpArtifactRangeDraft = false; pvpArtifactRangeLast = -1;
            if (pvpArtifactRange) pvpArtifactRange.SetTextWithoutNotify("");
        }
    }
}
