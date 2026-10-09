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
            deathmatchApply = widgets.Button(pageRoot, "Apply duration", 120, 56, 169, 25,
                () => Execute("/one deathmatch duration " + draft.Text, true));
            deathmatchStart = widgets.Button(pageRoot, "Start deathmatch", 16, 94, 132, 28, () => Execute("/one deathmatch start", true));
            deathmatchStop = widgets.Button(pageRoot, "Stop deathmatch", 156, 94, 133, 28, () => Execute("/one deathmatch stop", true));
            widgets.Text(pageRoot, "DeathmatchHelp", "Changing the friendly-fire toggle stops the match immediately.", 16, 128, 273, 34, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }
        private void RefreshDeathmatch(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            deathmatchStart.interactable = host && DeathmatchRuntime.CanStart;
            deathmatchStop.interactable = host && DeathmatchRuntime.IsRunning;
            deathmatchApply.interactable = amount.interactable = host;
            availability.text = L.T("Temporary host deathmatch. Unmodified guests participate automatically.");
            availability.color = PanelWidgets.Muted;
            readout.SetText(DeathmatchRuntime.Describe() + "\n\n" +
                (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available ?
                    L.T("Deathmatch compatibility checks failed. Stop remains available; see Player.log.") + "\n\n" : "") +
                L.T("Three-second opening countdown and three-second respawns at full HP. Native safe-area protection remains active.") + "\n\n" +
                L.T("Match end turns friendly fire off, revives pending players and announces the top five. Ranking: kills, fewer deaths, then assists.") + "\n\n" +
                L.T("Duration changes affect the next match. Save in Presets to keep the duration; match progress and scores are never saved."));
        }
    }
}
