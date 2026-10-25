using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_Text updateLabel, updateNotice;
        private Toggle updateAutomatic;
        private Button updateInstall, updateCheck;

        private void BuildUpdatesEditor()
        {
            updateCheck = widgets.Button(pageRoot, "Check for updates", 16, 0, 180, 25, () => Execute("/one update check", false));
            updateInstall = widgets.Button(pageRoot, "Update", 207, 0, 180, 25, () => Execute("/one update install", false));
            widgets.Button(pageRoot, "Open releases", 398, 0, 186, 25,
                () => Application.OpenURL("https://github.com/preco21/SephiriaOne/releases"));
            updateAutomatic = widgets.Checkbox(pageRoot, "Automatic checks", 16, 32, 371, 23);
            updateAutomatic.onValueChanged.AddListener(value => {
                Execute("/one update auto " + (value ? "on" : "off"), false);
                var state = UpdateFeature.Snapshot;
                updateAutomatic.SetIsOnWithoutNotify(state != null && state.Automatic);
            });
            readout = widgets.Scroll(pageRoot, 16, 62, 568, PageHeight - 62);
        }

        private void RefreshUpdateHeader()
        {
            var state = UpdateFeature.Snapshot;
            bool notice = state != null && (state.CanInstall || state.Phase == UpdatePhase.RestartRequired || state.Busy);
            updateLabel.text = state != null && state.CanInstall ? L.T("Update available") : L.T("Updates");
            // Exceptional update warnings remain visible on every page. Ordinary
            // host/ready/count labels no longer occupy the title bar.
            updateNotice.text = state != null && state.Phase == UpdatePhase.Installing ? L.T("Installing update · Keep the game open") :
                state != null && state.Phase == UpdatePhase.RestartRequired ? L.T("Update installed · Restart required") :
                state != null && state.CanInstall ? L.F("Update {0} available", state.Candidate.Version) : "";
            updateLabel.color = notice ? (Color)new Color32(255, 200, 122, 255) :
                page == 12 ? PanelWidgets.Ink : PanelWidgets.Muted;
        }

        private void RefreshUpdates()
        {
            var state = UpdateFeature.Snapshot;
            availability.text = state != null && state.Phase == UpdatePhase.Installing ? L.T("Installing update · Keep the game open") :
                state != null && state.Phase == UpdatePhase.RestartRequired ? L.T("Update installed · Restart required") : "";
            availability.color = PanelWidgets.Muted;
            updateCheck.interactable = state != null && !state.Busy && state.Phase != UpdatePhase.RestartRequired;
            updateInstall.interactable = state != null && state.CanInstall;
            updateAutomatic.interactable = state != null && !state.Busy;
            updateAutomatic.SetIsOnWithoutNotify(state != null && state.Automatic);
            readout.SetText(UpdateText.Status(state));
        }
    }
}
