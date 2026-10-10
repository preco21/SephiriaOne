using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button deathmatchStart, deathmatchStop, deathmatchApply;
        private void BuildDeathmatchEditor()
        {
            widgets.Text(pageRoot, "DeathmatchTitle", "Deathmatch", 16, 0, 273, 24, 14);
            widgets.Text(pageRoot, "DeathmatchUnits", "Duration in seconds (10–3600)", 16, 29, 273, 21, 10);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            deathmatchApply = widgets.Button(pageRoot, "Apply", 120, 56, 169, 25,
                () => Execute("/one deathmatch duration " + draft.Text, true));
            deathmatchStart = widgets.Button(pageRoot, "Start match", 16, 94, 132, 28, () => Execute("/one deathmatch start", true));
            deathmatchStop = widgets.Button(pageRoot, "Stop match", 156, 94, 133, 28, () => Execute("/one deathmatch stop", true));
            widgets.Text(pageRoot, "DeathmatchHelp", "Changing friendly fire stops the match.", 16, 128, 273, 34, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }
        private void RefreshDeathmatch(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            deathmatchStart.interactable = host && DeathmatchRuntime.CanStart;
            deathmatchStop.interactable = host && DeathmatchRuntime.IsRunning;
            deathmatchApply.interactable = amount.interactable = host;
            availability.text = L.T("Deathmatch · Temporary session");
            availability.color = PanelWidgets.Muted;
            readout.SetText(DeathmatchRuntime.Describe() + "\n\n" +
                (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available ?
                    L.T("Deathmatch compatibility checks failed. Stop remains available; see Player.log.") + "\n\n" : "") +
                L.T("Start: 3s countdown. Respawn: 3s, full HP. Safe areas stay protected.") + "\n\n" +
                L.T("End: friendly fire off, pending players revived, top 5 announced. Rank: kills → fewer deaths → assists.") + "\n\n" +
                L.T("Duration changes apply next match. Presets save duration only."));
        }
    }
}
