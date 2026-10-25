using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private sealed class ParticipantRow
        {
            internal Toggle Toggle;
            internal TMP_Text Label;
            internal FriendlyFirePlayerSnapshot Player;
        }
        private readonly List<ParticipantRow> participantRows = new List<ParticipantRow>();
        private RectTransform participantContent;
        private TMP_Text participantSummary;
        private Button selectAllPlayers, resetPlayerSelection;

        // Combat and all PvP match modes share this list. Rows are pooled for
        // the page's lifetime; refresh replaces data, never listeners or objects.
        private void BuildParticipantsEditor()
        {
            participantSummary = widgets.Text(pageRoot, "Participants", "", 312, 0, 272, 22, 11);
            selectAllPlayers = widgets.Button(pageRoot, "Select All", 312, 26, 132, 25,
                () => Execute("/one friendlyfire players all", true));
            resetPlayerSelection = widgets.Button(pageRoot, "Reset selection", 452, 26, 132, 25,
                () => Execute("/one friendlyfire players reset", true));
            participantContent = widgets.ScrollSection(pageRoot, "PlayerSelection", 312, 57, 246,
                PageHeight - 57, PageHeight - 69, 26);
        }

        private void RefreshParticipants(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            participantSummary.text = snapshot.FriendlyFireSelectedCount == 0 ? L.T("Participants") :
                L.F("Selected players only: {0}", snapshot.FriendlyFireSelectedCount);
            selectAllPlayers.interactable = host && snapshot.FriendlyFireAvailable && snapshot.FriendlyFirePlayers.Count > 0;
            resetPlayerSelection.interactable = host;
            for (int i = 0; i < snapshot.FriendlyFirePlayers.Count; i++)
            {
                if (i == participantRows.Count)
                {
                    var toggle = widgets.Checkbox(participantContent, "", 0, i * 28, 246, 25);
                    var row = new ParticipantRow { Toggle = toggle, Label = toggle.GetComponentInChildren<TMP_Text>() };
                    participantRows.Add(row);
                    toggle.onValueChanged.AddListener(value =>
                    {
                        var player = row.Player;
                        if (player == null) return;
                        Execute("/one friendlyfire player " + player.Id + (value ? " on" : " off"), true, player.Identity);
                    });
                }
                var current = participantRows[i]; var data = snapshot.FriendlyFirePlayers[i];
                current.Player = data; current.Toggle.gameObject.SetActive(true);
                string faction = CombatMatchRuntime.ZombieFactionMarker(data.Identity as FriendlyFireParticipants.Receipt);
                current.Label.text = (faction.Length == 0 ? "" : faction + " ") + data.Id + ": " + data.Name;
                current.Toggle.SetIsOnWithoutNotify(data.Selected);
                current.Toggle.interactable = host && (snapshot.FriendlyFireAvailable || data.Selected);
            }
            for (int i = snapshot.FriendlyFirePlayers.Count; i < participantRows.Count; i++)
            { participantRows[i].Player = null; participantRows[i].Toggle.gameObject.SetActive(false); }
            participantContent.sizeDelta = new Vector2(246, Mathf.Max(PageHeight - 69, snapshot.FriendlyFirePlayers.Count * 28));
        }
    }
}
