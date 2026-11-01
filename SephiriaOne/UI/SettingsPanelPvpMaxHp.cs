using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyMaxHpBaseline, friendlyMaxHpSoft, friendlyMaxHpHard;
        private bool friendlyMaxHpDraft;
        private Button friendlyMaxHpApply, friendlyMaxHpReset;
        private TMP_Text friendlyMaxHpAvailability;

        private void BuildPvpMaxHpEditor(RectTransform content)
        {
            AddCheckbox("Max HP normalization", 0, 0, 273, 25, s => s.FriendlyFire.MaxHp.Enabled,
                s => s.FriendlyFireAvailable && s.MaxHpAvailable, "/one friendlyfire max-hp", parent: content);
            widgets.Text(content, "MaxHpBaseline", "Baseline (HP)", 0, 34, 85, 18, 11);
            widgets.Text(content, "MaxHpSoft", "Soft +HP", 94, 34, 85, 18, 11);
            widgets.Text(content, "MaxHpHard", "Hard +HP", 188, 34, 85, 18, 11);
            friendlyMaxHpBaseline = widgets.Input(content, 0, 55, 85, EditFriendlyMaxHp);
            friendlyMaxHpSoft = widgets.Input(content, 94, 55, 85, EditFriendlyMaxHp);
            friendlyMaxHpHard = widgets.Input(content, 188, 55, 85, EditFriendlyMaxHp);
            friendlyMaxHpApply = widgets.Button(content, "Apply", 0, 91, 132, 25, ApplyFriendlyMaxHp);
            friendlyMaxHpReset = widgets.Button(content, "Reset", 140, 91, 133, 25, ResetFriendlyMaxHp);
            widgets.Text(content, "MaxHpHint", "Baseline + bonus HP. Requires PvP balancing.\nDefaults: 150 unchanged; 200 → 187.5; final cap 200.\nPreserves current HP %. Also affects participants' PvE.",
                0, 126, 273, 80, 10);
            friendlyMaxHpAvailability = widgets.Text(content, "MaxHpAvailability", "", 0, 213, 273, 40, 10);
            friendlyMaxHpAvailability.color = new Color32(255, 200, 122, 255);
        }

        private void RefreshPvpMaxHp(SettingsSnapshot snapshot)
        {
            if (!friendlyMaxHpBaseline || !friendlyMaxHpSoft || !friendlyMaxHpHard) return;
            bool available = snapshot.FriendlyFireAvailable && snapshot.MaxHpAvailable;
            friendlyMaxHpBaseline.interactable = friendlyMaxHpSoft.interactable = friendlyMaxHpHard.interactable =
                friendlyMaxHpApply.interactable = snapshot.CanMutate && available;
            // Compatibility failure must not prevent disabling or resetting policy.
            friendlyMaxHpReset.interactable = snapshot.CanMutate;
            friendlyMaxHpAvailability.text = !snapshot.MaxHpAvailable ?
                L.T("Max HP normalization unavailable; off/reset remain available. See Player.log.") : "";
            if (friendlyMaxHpDraft) return;
            friendlyMaxHpBaseline.SetTextWithoutNotify(snapshot.FriendlyFire.MaxHp.BaselineNumber);
            friendlyMaxHpSoft.SetTextWithoutNotify(snapshot.FriendlyFire.MaxHp.SoftNumber);
            friendlyMaxHpHard.SetTextWithoutNotify(snapshot.FriendlyFire.MaxHp.HardNumber);
        }

        private void EditFriendlyMaxHp(string text) => friendlyMaxHpDraft = true;
        private void ApplyFriendlyMaxHp() => Execute("/one friendlyfire max-hp limits " +
            friendlyMaxHpBaseline.text.Trim() + " " + friendlyMaxHpSoft.text.Trim() + " " + friendlyMaxHpHard.text.Trim(),
            true, clearInputOnSuccess: ClearFriendlyMaxHp);
        private void ResetFriendlyMaxHp() => Execute("/one friendlyfire max-hp reset", true,
            clearInputOnSuccess: ClearFriendlyMaxHp);
        private void ClearFriendlyMaxHp()
        {
            friendlyMaxHpDraft = false;
            if (friendlyMaxHpBaseline) friendlyMaxHpBaseline.SetTextWithoutNotify("");
            if (friendlyMaxHpSoft) friendlyMaxHpSoft.SetTextWithoutNotify("");
            if (friendlyMaxHpHard) friendlyMaxHpHard.SetTextWithoutNotify("");
        }
    }
}
