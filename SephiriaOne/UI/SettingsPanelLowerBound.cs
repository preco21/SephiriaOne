using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyLowerReference, friendlyLowerRefill;
        private bool friendlyLowerReferenceDraft, friendlyLowerRefillDraft;
        private Button friendlyLowerReferenceReset, friendlyLowerRefillReset, friendlyLowerReset;

        private void BuildPvpLowerBound(RectTransform content)
        {
            AddCheckbox("Weak-hit assistance", 0, 0, 273, 25, s => s.FriendlyFire.LowerBound.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire lower-bound", parent: content);
            widgets.Text(content, "LowerReference", "Reference (% opponent max HP)", 0, 32, 273, 20, 11);
            friendlyLowerReference = widgets.Input(content, 0, 56, 90, _ => friendlyLowerReferenceDraft = true);
            changeButtons.Add(widgets.Button(content, "Apply", 98, 56, 77, 25, ApplyFriendlyLowerReference));
            friendlyLowerReferenceReset = widgets.Button(content, "Reset", 183, 56, 90, 25, ResetFriendlyLowerReference);
            widgets.Text(content, "LowerRefill", "Bonus refill (% max HP/s)", 0, 88, 273, 20, 11);
            friendlyLowerRefill = widgets.Input(content, 0, 112, 90, _ => friendlyLowerRefillDraft = true);
            changeButtons.Add(widgets.Button(content, "Apply", 98, 112, 77, 25, ApplyFriendlyLowerRefill));
            friendlyLowerRefillReset = widgets.Button(content, "Reset", 183, 112, 90, 25, ResetFriendlyLowerRefill);
            widgets.Text(content, "LowerHint", "Weak hits use one small bonus pool per opponent across all sources. Role reductions, shields and the main allowance still apply.",
                0, 144, 273, 58, 10);
            friendlyLowerReset = widgets.Button(content, "Reset assistance", 0, 208, 273, 25, ResetFriendlyLowerBound);
        }

        private void RefreshPvpLowerBound(SettingsSnapshot snapshot)
        {
            if (!friendlyLowerReference || !friendlyLowerRefill) return;
            bool available = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            friendlyLowerReference.interactable = friendlyLowerRefill.interactable = available;
            friendlyLowerReferenceReset.interactable = friendlyLowerRefillReset.interactable = friendlyLowerReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                (snapshot.FaultedFeature == "combat" || snapshot.FaultedFeature == "inheritance"));
            if (!friendlyLowerReferenceDraft) friendlyLowerReference.SetTextWithoutNotify(snapshot.FriendlyFire.LowerBound.ReferenceNumber);
            if (!friendlyLowerRefillDraft) friendlyLowerRefill.SetTextWithoutNotify(snapshot.FriendlyFire.LowerBound.RefillNumber);
        }

        private void ApplyFriendlyLowerReference() => Execute("/one friendlyfire lower-bound reference " + friendlyLowerReference.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyLowerReference);
        private void ApplyFriendlyLowerRefill() => Execute("/one friendlyfire lower-bound refill " + friendlyLowerRefill.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyLowerRefill);
        private void ResetFriendlyLowerReference() => Execute("/one friendlyfire lower-bound reference reset", true,
            clearInputOnSuccess: ClearFriendlyLowerReference);
        private void ResetFriendlyLowerRefill() => Execute("/one friendlyfire lower-bound refill reset", true,
            clearInputOnSuccess: ClearFriendlyLowerRefill);
        private void ResetFriendlyLowerBound() => Execute("/one friendlyfire lower-bound reset", true,
            clearInputOnSuccess: ClearFriendlyLowerBound);
        private void ClearFriendlyLowerReference() { friendlyLowerReferenceDraft = false; if (friendlyLowerReference) friendlyLowerReference.SetTextWithoutNotify(""); }
        private void ClearFriendlyLowerRefill() { friendlyLowerRefillDraft = false; if (friendlyLowerRefill) friendlyLowerRefill.SetTextWithoutNotify(""); }
        private void ClearFriendlyLowerBound() { ClearFriendlyLowerReference(); ClearFriendlyLowerRefill(); }
    }
}
