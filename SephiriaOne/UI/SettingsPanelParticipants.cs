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

        // The same list controls both combat and deathmatch. Rows are pooled for
        // the page's lifetime; refresh replaces data, never listeners or objects.
        private void BuildParticipantsEditor()
        {
            participantSummary = widgets.Text(pageRoot, "Participants", "", 312, 0, 272, 22, 11);
            selectAllPlayers = widgets.Button(pageRoot, "Select All", 312, 26, 132, 25,
                () => Execute("/one friendlyfire players all", true));
            resetPlayerSelection = widgets.Button(pageRoot, "Reset selection", 452, 26, 132, 25,
                () => Execute("/one friendlyfire players reset", true));
            var container = PanelWidgets.Rect(pageRoot, "PlayerSelection", 312, 57, 272, 105);
            container.gameObject.AddComponent<Image>().color = new Color32(17, 27, 40, 255);
            var scroll = container.gameObject.AddComponent<ScrollRect>();
            var viewport = PanelWidgets.Rect(container, "Viewport", 3, 3, 266, 99);
            viewport.gameObject.AddComponent<RectMask2D>();
            participantContent = PanelWidgets.Rect(viewport, "Players", 0, 0, 266, 99);
            scroll.viewport = viewport; scroll.content = participantContent; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 26;
        }

        private void RefreshParticipants(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            participantSummary.text = snapshot.FriendlyFireSelectedCount == 0 ? L.T("Everyone · no selection") :
                L.F("Selected players only: {0}", snapshot.FriendlyFireSelectedCount);
            selectAllPlayers.interactable = host && snapshot.FriendlyFireAvailable && snapshot.FriendlyFirePlayers.Count > 0;
            resetPlayerSelection.interactable = host;
            for (int i = 0; i < snapshot.FriendlyFirePlayers.Count; i++)
            {
                if (i == participantRows.Count)
                {
                    var toggle = widgets.Checkbox(participantContent, "", 0, i * 28, 266, 25);
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
                current.Label.text = data.Id + ": " + data.Name;
                current.Toggle.SetIsOnWithoutNotify(data.Selected);
                current.Toggle.interactable = host && (snapshot.FriendlyFireAvailable || data.Selected);
            }
            for (int i = snapshot.FriendlyFirePlayers.Count; i < participantRows.Count; i++)
            { participantRows[i].Player = null; participantRows[i].Toggle.gameObject.SetActive(false); }
            participantContent.sizeDelta = new Vector2(266, Mathf.Max(99, snapshot.FriendlyFirePlayers.Count * 28));
        }
    }
}
