using TMPro;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyRefill;
        private bool friendlyRefillDraft;
        private TMP_InputField friendlyGuardMp;
        private bool friendlyGuardMpDraft;
        private TMP_InputField friendlyAmplificationSoft, friendlyAmplificationHard;
        private bool friendlyAmplificationDraft;

        private void BuildPvpBalanceEditor()
        {
            var content = PanelWidgets.Rect(pageRoot, "BalanceControls", 16, 0, 273, PageHeight);
            AddCheckbox("PvP balancing", 0, 0, 273, 25, s => s.FriendlyFire.Balance,
                s => s.FriendlyFireAvailable, "/one friendlyfire balance", parent: content);
            AddCheckbox("Hit-frequency scaling", 0, 34, 273, 25, s => s.FriendlyFire.HitFrequency,
                s => s.FriendlyFireAvailable, "/one friendlyfire hit-frequency", parent: content);
            widgets.Text(content, "FriendlyRefill", "PvP refill (%/s)", 0, 70, 116, 25, 12);
            friendlyRefill = widgets.Input(content, 120, 70, 68, EditFriendlyRefill);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 70, 77, 25, ApplyFriendlyRefill));
            widgets.Text(content, "GuardMp", "Shield MP min (%)", 0, 106, 116, 25, 12);
            friendlyGuardMp = widgets.Input(content, 120, 106, 68, EditFriendlyGuardMp);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 106, 77, 25, ApplyFriendlyGuardMp));
            AddCheckbox("All Damage Amplified limit", 0, 145, 273, 25, s => s.FriendlyFire.Amplification.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire amplification", parent: content);
            widgets.Text(content, "AmplificationSoft", "Soft bonus (%)", 0, 180, 90, 18, 11);
            widgets.Text(content, "AmplificationHard", "Hard bonus (%)", 98, 180, 90, 18, 11);
            friendlyAmplificationSoft = widgets.Input(content, 0, 201, 90, EditFriendlyAmplification);
            friendlyAmplificationHard = widgets.Input(content, 98, 201, 90, EditFriendlyAmplification);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 201, 77, 25, ApplyFriendlyAmplification));
            widgets.Text(content, "AmplificationHint", "Bonus %, not C-menu total.", 0, 232, 273, 18, 11);
            BuildPvpDotEditor();
        }

        private void RefreshPvpBalance(SettingsSnapshot snapshot)
        {
            if (!friendlyRefill || !friendlyGuardMp) return;
            friendlyRefill.interactable = friendlyGuardMp.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyRefillDraft) friendlyRefill.SetTextWithoutNotify(snapshot.FriendlyFire.RefillNumber);
            if (!friendlyGuardMpDraft) friendlyGuardMp.SetTextWithoutNotify(snapshot.FriendlyFire.GuardMpNumber);
            friendlyAmplificationSoft.interactable = friendlyAmplificationHard.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyAmplificationDraft)
            {
                friendlyAmplificationSoft.SetTextWithoutNotify(snapshot.FriendlyFire.Amplification.SoftNumber);
                friendlyAmplificationHard.SetTextWithoutNotify(snapshot.FriendlyFire.Amplification.HardNumber);
            }
        }

        private void EditFriendlyGuardMp(string text) => friendlyGuardMpDraft = true;
        private void EditFriendlyRefill(string text) => friendlyRefillDraft = true;
        private void ApplyFriendlyRefill() => Execute("/one friendlyfire refill " + friendlyRefill.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyRefill);
        private void ClearFriendlyRefill() { friendlyRefillDraft = false; if (friendlyRefill) friendlyRefill.SetTextWithoutNotify(""); }
        private void ApplyFriendlyGuardMp() => Execute("/one friendlyfire guard-mp " + friendlyGuardMp.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyGuardMp);
        private void ClearFriendlyGuardMp() { friendlyGuardMpDraft = false; if (friendlyGuardMp) friendlyGuardMp.SetTextWithoutNotify(""); }
        private void EditFriendlyAmplification(string text) => friendlyAmplificationDraft = true;
        private void ApplyFriendlyAmplification() => Execute("/one friendlyfire amplification limits " +
            friendlyAmplificationSoft.text.Trim() + " " + friendlyAmplificationHard.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyAmplification);
        private void ClearFriendlyAmplification()
        {
            friendlyAmplificationDraft = false;
            if (friendlyAmplificationSoft) friendlyAmplificationSoft.SetTextWithoutNotify("");
            if (friendlyAmplificationHard) friendlyAmplificationHard.SetTextWithoutNotify("");
        }
    }
}
