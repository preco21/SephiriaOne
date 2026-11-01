using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyEvasionSoft, friendlyEvasionHard;
        private bool friendlyEvasionDraft;
        private Button friendlyEvasionApply, friendlyEvasionReset;

        private void BuildPvpEvasionEditor(RectTransform content)
        {
            AddCheckbox("Evasion chance limit", 0, 0, 273, 25, s => s.FriendlyFire.Evasion.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire evasion", mutable: CanEditPvpEvasion, parent: content);
            widgets.Text(content, "EvasionSoft", "Soft chance (%)", 0, 34, 132, 18, 11);
            widgets.Text(content, "EvasionHard", "Hard chance (%)", 140, 34, 133, 18, 11);
            friendlyEvasionSoft = widgets.Input(content, 0, 55, 132, EditFriendlyEvasion);
            friendlyEvasionHard = widgets.Input(content, 140, 55, 133, EditFriendlyEvasion);
            friendlyEvasionApply = widgets.Button(content, "Apply", 0, 91, 132, 25, ApplyFriendlyEvasion);
            friendlyEvasionReset = widgets.Button(content, "Reset", 140, 91, 133, 25, ResetFriendlyEvasion);
            widgets.Text(content, "EvasionHint", "Dodge chance, not raw Evasion. Anti-Evasion applies afterward. PvE and guaranteed item dodges unchanged.",
                0, 124, 273, 40, 10);
        }

        private void RefreshPvpEvasion(SettingsSnapshot snapshot)
        {
            if (!friendlyEvasionSoft || !friendlyEvasionHard) return;
            bool host = CanEditPvpEvasion(snapshot);
            friendlyEvasionSoft.interactable = friendlyEvasionHard.interactable = friendlyEvasionApply.interactable =
                host && snapshot.FriendlyFireAvailable;
            friendlyEvasionReset.interactable = host;
            if (friendlyEvasionDraft) return;
            friendlyEvasionSoft.SetTextWithoutNotify(snapshot.FriendlyFire.Evasion.SoftNumber);
            friendlyEvasionHard.SetTextWithoutNotify(snapshot.FriendlyFire.Evasion.HardNumber);
        }

        private static bool CanEditPvpEvasion(SettingsSnapshot snapshot) => snapshot.HostActive && snapshot.SessionIdentity != null;
        private void EditFriendlyEvasion(string text) => friendlyEvasionDraft = true;
        private void ApplyFriendlyEvasion() => Execute("/one friendlyfire evasion limits " +
            friendlyEvasionSoft.text.Trim() + " " + friendlyEvasionHard.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyEvasion);
        private void ResetFriendlyEvasion() => Execute("/one friendlyfire evasion reset", true,
            clearInputOnSuccess: ClearFriendlyEvasion);
        private void ClearFriendlyEvasion()
        {
            friendlyEvasionDraft = false;
            if (friendlyEvasionSoft) friendlyEvasionSoft.SetTextWithoutNotify("");
            if (friendlyEvasionHard) friendlyEvasionHard.SetTextWithoutNotify("");
        }
    }
}
