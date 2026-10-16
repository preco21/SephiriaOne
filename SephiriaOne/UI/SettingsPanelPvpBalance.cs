using TMPro;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyGuardMp;
        private bool friendlyGuardMpDraft;

        private void BuildPvpBalanceEditor()
        {
            AddCheckbox("PvP balancing", 16, 80, 130, 21, s => s.FriendlyFire.Balance,
                s => s.FriendlyFireAvailable, "/one friendlyfire balance");
            AddCheckbox("Hit-frequency scaling", 152, 80, 137, 21, s => s.FriendlyFire.HitFrequency,
                s => s.FriendlyFireAvailable, "/one friendlyfire hit-frequency");
            widgets.Text(pageRoot, "FriendlyRefill", "PvP refill (%/s)", 16, 106, 116, 25, 11);
            friendlyRefill = widgets.Input(pageRoot, 136, 106, 62, EditFriendlyRefill);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 204, 106, 85, 25, ApplyFriendlyRefill));
            widgets.Text(pageRoot, "GuardMp", "Guard MP (%)", 16, 137, 116, 25, 11);
            friendlyGuardMp = widgets.Input(pageRoot, 136, 137, 62, EditFriendlyGuardMp);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 204, 137, 85, 25, ApplyFriendlyGuardMp));
        }

        private void RefreshPvpBalance(SettingsSnapshot snapshot)
        {
            if (!friendlyRefill || !friendlyGuardMp) return;
            friendlyRefill.interactable = friendlyGuardMp.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyRefillDraft) friendlyRefill.SetTextWithoutNotify(snapshot.FriendlyFire.RefillNumber);
            if (!friendlyGuardMpDraft) friendlyGuardMp.SetTextWithoutNotify(snapshot.FriendlyFire.GuardMpNumber);
        }

        private void EditFriendlyGuardMp(string text) => friendlyGuardMpDraft = true;
        private void ApplyFriendlyGuardMp() => Execute("/one friendlyfire guard-mp " + friendlyGuardMp.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyGuardMp);
        private void ClearFriendlyGuardMp() { friendlyGuardMpDraft = false; if (friendlyGuardMp) friendlyGuardMp.SetTextWithoutNotify(""); }
    }
}
