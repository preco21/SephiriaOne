using System.Globalization;
using TMPro;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button playTagStart, playTagStop, playTagApply, playTagResetDuration, playTagResetKda;
        private Button playTagRoundsApply, playTagRoundsReset;
        private Button playTagRefillApply, playTagIncomingApply;
        private TMP_InputField playTagRounds, playTagRefill, playTagIncoming;
        private bool playTagDurationDraft, playTagRoundsDraft, playTagRefillDraft, playTagIncomingDraft;
        private void BuildPlayTagEditor()
        {
            widgets.Text(pageRoot, "PlayTagFuse", "Fuse (s)", 16, 0, 72, 25, 11);
            amount = widgets.Input(pageRoot, 94, 0, 51, EditPlayTagDuration);
            playTagApply = widgets.Button(pageRoot, "Apply", 153, 0, 62, 25, ApplyPlayTagDuration);
            playTagResetDuration = widgets.Button(pageRoot, "Reset", 223, 0, 66, 25,
                () => Execute("/one tag duration reset", true, clearInputOnSuccess: ClearPlayTagDuration));
            widgets.Text(pageRoot, "PlayTagRounds", "Rounds", 16, 27, 72, 25, 11);
            playTagRounds = widgets.Input(pageRoot, 94, 27, 51, EditPlayTagRounds);
            playTagRoundsApply = widgets.Button(pageRoot, "Apply", 153, 27, 62, 25, ApplyPlayTagRounds);
            playTagRoundsReset = widgets.Button(pageRoot, "Reset", 223, 27, 66, 25,
                () => Execute("/one tag rounds reset", true, clearInputOnSuccess: ClearPlayTagRounds));
            playTagStart = widgets.Button(pageRoot, "Start match", 16, 54, 132, 25, () => Execute("/one tag start", true));
            playTagStop = widgets.Button(pageRoot, "Stop match", 156, 54, 133, 25, () => Execute("/one tag stop", true));
            playTagResetKda = widgets.Button(pageRoot, "Reset K/D/A", 16, 81, 273, 25,
                () => Execute("/one tag resetkda", true));
            AddCheckbox("Refill (x)", 16, 108, 150, 25, s => SessionSettings.PlayTagChaser.RefillEnabled, s => true,
                "/one tag chaser-refill", s => s.HostActive && s.SessionIdentity != null);
            playTagRefill = widgets.Input(pageRoot, 174, 108, 56, EditPlayTagRefill);
            playTagRefillApply = widgets.Button(pageRoot, "Apply", 238, 108, 51, 25, ApplyPlayTagRefill);
            AddCheckbox("Incoming (%)", 16, 135, 150, 25, s => SessionSettings.PlayTagChaser.IncomingEnabled, s => true,
                "/one tag chaser-damage", s => s.HostActive && s.SessionIdentity != null);
            playTagIncoming = widgets.Input(pageRoot, 174, 135, 56, EditPlayTagIncoming);
            playTagIncomingApply = widgets.Button(pageRoot, "Apply", 238, 135, 51, 25, ApplyPlayTagIncoming);
            BuildParticipantsEditor();
        }
        private void RefreshPlayTag(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            playTagStart.interactable = host && CombatMatchRuntime.CanStartPlayTag;
            playTagStop.interactable = host && CombatMatchRuntime.IsPlayTag;
            playTagResetKda.interactable = host && SessionSettings.CanResetFriendlyFireScores;
            playTagApply.interactable = playTagResetDuration.interactable = amount.interactable = host;
            playTagRounds.interactable = playTagRoundsApply.interactable = playTagRoundsReset.interactable = host;
            playTagRefill.interactable = playTagIncoming.interactable = playTagRefillApply.interactable = playTagIncomingApply.interactable = host;
            if (!playTagDurationDraft) amount.SetTextWithoutNotify(SessionSettings.PlayTagDuration.ToString(CultureInfo.InvariantCulture));
            if (!playTagRoundsDraft) playTagRounds.SetTextWithoutNotify(SessionSettings.PlayTagRounds.ToString(CultureInfo.InvariantCulture));
            var chaser = SessionSettings.PlayTagChaser;
            if (!playTagRefillDraft) playTagRefill.SetTextWithoutNotify(chaser.RefillNumber);
            if (!playTagIncomingDraft) playTagIncoming.SetTextWithoutNotify(chaser.IncomingNumber);
            availability.text = CombatMatchRuntime.DescribePlayTag();
            availability.color = PanelWidgets.Muted;
            if (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available)
                availability.text = L.T("Play Tag compatibility checks failed. Stop remains available; see Player.log.");
            RefreshParticipants(snapshot);
        }
        private void EditPlayTagDuration(string text) => playTagDurationDraft = true;
        private void EditPlayTagRounds(string text) => playTagRoundsDraft = true;
        private void EditPlayTagRefill(string text) => playTagRefillDraft = true;
        private void EditPlayTagIncoming(string text) => playTagIncomingDraft = true;
        private void ApplyPlayTagDuration() => Execute("/one tag duration " + amount.text.Trim(), true,
            clearInputOnSuccess: ClearPlayTagDuration);
        private void ApplyPlayTagRounds() => Execute("/one tag rounds " + playTagRounds.text.Trim(), true,
            clearInputOnSuccess: ClearPlayTagRounds);
        private void ApplyPlayTagRefill() => Execute("/one tag chaser-refill " + playTagRefill.text.Trim(), true,
            clearInputOnSuccess: ClearPlayTagRefill);
        private void ApplyPlayTagIncoming() => Execute("/one tag chaser-damage " + playTagIncoming.text.Trim(), true,
            clearInputOnSuccess: ClearPlayTagIncoming);
        private void ClearPlayTagDuration() { playTagDurationDraft = false; if (amount) amount.SetTextWithoutNotify(""); }
        private void ClearPlayTagRounds() { playTagRoundsDraft = false; if (playTagRounds) playTagRounds.SetTextWithoutNotify(""); }
        private void ClearPlayTagRefill() { playTagRefillDraft = false; if (playTagRefill) playTagRefill.SetTextWithoutNotify(""); }
        private void ClearPlayTagIncoming() { playTagIncomingDraft = false; if (playTagIncoming) playTagIncoming.SetTextWithoutNotify(""); }
    }
}
