using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private sealed class DotIntervalEditor
        {
            internal PvpDotEffect Effect;
            internal TMP_InputField Input;
            internal bool Draft;
            internal int LastHundredths = -1;
        }
        private readonly List<DotIntervalEditor> pvpDotEditors = new List<DotIntervalEditor>(PvpDotSettings.Count);
        private TMP_InputField pvpDotDuration;
        private Button pvpDotDurationReset;
        private bool pvpDotDurationDraft;
        private float pvpDotDurationLast = -1;

        private void BuildPvpDotEditor()
        {
            widgets.Text(pageRoot, "DotHeading", "Artifacts / Debuffs", 312, 0, 272, 24, 13);
            const float height = PageHeight - 32;
            const int thornsHeight = 34;
            const int durationOffset = thornsHeight + ArtifactRateEditorHeight;
            const int durationHeight = durationOffset + 80;
            var content = widgets.ScrollSection(pageRoot, "DotIntervals", 312, 32, 258, height,
                durationHeight + PvpDotSettings.Count * 62);
            AddCheckbox("Suppress Thorns", 4, 0, 250, 25, s => s.FriendlyFire.SuppressThorns,
                s => s.FriendlyFireAvailable, "/one friendlyfire suppress-thorns", parent: content);
            BuildPvpArtifactRate(PanelWidgets.Rect(content, "ArtifactControls", 0, thornsHeight, 258, ArtifactRateEditorHeight));
            widgets.Text(content, "DotDurationLabel", "Burn-family max duration (s)", 4, durationOffset, 250, 20, 12);
            pvpDotDuration = widgets.Input(content, 4, durationOffset + 24, 56, _ => pvpDotDurationDraft = true);
            changeButtons.Add(widgets.Button(content, "Apply", 66, durationOffset + 24, 62, 25, ApplyPvpDotDuration));
            pvpDotDurationReset = widgets.Button(content, "Reset", 134, durationOffset + 24, 120, 25, ResetPvpDotDuration);
            widgets.Text(content, "DotDurationHint", "Burn / Plasma Burn / Blue Flame", 4, durationOffset + 54, 250, 18, 10);
            for (int i = 0; i < PvpDotSettings.Count; i++)
            {
                var row = new DotIntervalEditor { Effect = (PvpDotEffect)i };
                int y = durationHeight + i * 62;
                widgets.Text(content, "DotLabel" + i, PvpDotSettings.Label(row.Effect), 4, y, 250, 20, 12);
                row.Input = widgets.Input(content, 4, y + 24, 56, _ => row.Draft = true);
                changeButtons.Add(widgets.Button(content, "Apply", 66, y + 24, 62, 25, () => ApplyPvpDot(row)));
                if (PvpDotSettings.SupportsSchedule(row.Effect))
                    AddCheckbox("Keep schedule", 134, y + 24, 120, 25,
                        s => s.FriendlyFire.DotIntervals.PreserveSchedule(row.Effect), s => s.FriendlyFireAvailable,
                        "/one friendlyfire dot " + PvpDotSettings.Key(row.Effect) + " preserve", parent: content);
                pvpDotEditors.Add(row);
            }
        }

        private void RefreshPvpDots(SettingsSnapshot snapshot)
        {
            RefreshPvpArtifactRate(snapshot);
            if (pvpDotDuration)
            {
                pvpDotDuration.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
                pvpDotDurationReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                    (snapshot.FaultedFeature == "combat" || snapshot.FaultedFeature == "inheritance"));
                float duration = snapshot.FriendlyFire.DotMaxDuration;
                if (!pvpDotDurationDraft && pvpDotDurationLast != duration)
                {
                    pvpDotDuration.SetTextWithoutNotify(snapshot.FriendlyFire.DotMaxDurationNumber);
                    pvpDotDurationLast = duration;
                }
            }
            foreach (var row in pvpDotEditors)
            {
                if (!row.Input) continue;
                row.Input.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
                int value = snapshot.FriendlyFire.DotIntervals.Hundredths(row.Effect);
                if (row.Draft || row.LastHundredths == value) continue;
                row.Input.SetTextWithoutNotify(snapshot.FriendlyFire.DotIntervals.Number(row.Effect));
                row.LastHundredths = value;
            }
        }
        private void ApplyPvpDot(DotIntervalEditor row) => Execute(
            "/one friendlyfire dot " + PvpDotSettings.Key(row.Effect) + " " + row.Input.text.Trim(), true,
            clearInputOnSuccess: () => ClearPvpDot(row));
        private static void ClearPvpDot(DotIntervalEditor row)
        {
            row.Draft = false; row.LastHundredths = -1;
            if (row.Input) row.Input.SetTextWithoutNotify("");
        }
        private void ApplyPvpDotDuration() => Execute("/one friendlyfire dot-duration " + pvpDotDuration.text.Trim(), true,
            clearInputOnSuccess: ClearPvpDotDuration);
        private void ResetPvpDotDuration() => Execute("/one friendlyfire dot-duration reset", true,
            clearInputOnSuccess: ClearPvpDotDuration);
        private void ClearPvpDotDuration()
        {
            pvpDotDurationDraft = false; pvpDotDurationLast = -1;
            if (pvpDotDuration) pvpDotDuration.SetTextWithoutNotify("");
        }
        private void ClearPvpDots() { ClearPvpArtifactRate(); ClearPvpDotDuration(); foreach (var row in pvpDotEditors) ClearPvpDot(row); }
    }
}
