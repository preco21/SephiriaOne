using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyRefill;
        private bool friendlyRefillDraft;
        private TMP_InputField friendlyGuardMp;
        private bool friendlyGuardMpDraft;
        private TMP_InputField friendlyPerfectGuard;
        private bool friendlyPerfectGuardDraft;
        private TMP_InputField friendlyAmplificationSoft, friendlyAmplificationHard;
        private bool friendlyAmplificationDraft;
        private TMP_InputField friendlyCriticalDamageSoft, friendlyCriticalDamageHard;
        private bool friendlyCriticalDamageDraft;
        private readonly TMP_InputField[] friendlyElementSoft = new TMP_InputField[4];
        private readonly TMP_InputField[] friendlyElementHard = new TMP_InputField[4];
        private readonly bool[] friendlyElementDraft = new bool[4];

        private void BuildPvpBalanceEditor()
        {
            var content = widgets.ScrollSection(pageRoot, "BalanceScroll", 10, 0, 273, PageHeight, 1012);
            AddCheckbox("PvP balancing", 0, 0, 170, 25, s => s.FriendlyFire.Balance,
                s => s.FriendlyFireAvailable, "/one friendlyfire balance", parent: content);
            widgets.Button(content, "Rapid Hits", 178, 0, 95, 25, () => SelectPage(PvpRapidPage));
            widgets.Text(content, "GuardMp", "Shield MP min (%)", 0, 34, 116, 25, 12);
            friendlyGuardMp = widgets.Input(content, 120, 34, 68, EditFriendlyGuardMp);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 34, 77, 25, ApplyFriendlyGuardMp));
            AddCheckbox("Perfect Guard MP multiplier", 0, 70, 273, 25, s => s.FriendlyFire.PerfectGuard.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire perfect-guard", parent: content);
            widgets.Text(content, "PerfectGuard", "Native MP cost (x)", 0, 106, 116, 25, 11);
            friendlyPerfectGuard = widgets.Input(content, 120, 106, 68, EditFriendlyPerfectGuard);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 106, 77, 25, ApplyFriendlyPerfectGuard));
            widgets.Text(content, "PerfectGuardHint", "When on, shield MP minimum does not apply.", 0, 132, 273, 12, 9);
            AddCheckbox("All Damage Amplified limit", 0, 145, 273, 25, s => s.FriendlyFire.Amplification.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire amplification", parent: content);
            widgets.Text(content, "AmplificationSoft", "Soft bonus (%)", 0, 180, 90, 18, 11);
            widgets.Text(content, "AmplificationHard", "Hard bonus (%)", 98, 180, 90, 18, 11);
            friendlyAmplificationSoft = widgets.Input(content, 0, 201, 90, EditFriendlyAmplification);
            friendlyAmplificationHard = widgets.Input(content, 98, 201, 90, EditFriendlyAmplification);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 201, 77, 25, ApplyFriendlyAmplification));
            widgets.Text(content, "AmplificationHint", "Bonus %, not C-menu total.", 0, 232, 273, 18, 11);
            AddCheckbox("Critical Damage limit", 0, 264, 273, 25, s => s.FriendlyFire.CriticalDamage.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire critical-damage", parent: content);
            widgets.Text(content, "CriticalDamageSoft", "Soft bonus (%)", 0, 299, 90, 18, 11);
            widgets.Text(content, "CriticalDamageHard", "Hard bonus (%)", 98, 299, 90, 18, 11);
            friendlyCriticalDamageSoft = widgets.Input(content, 0, 320, 90, EditFriendlyCriticalDamage);
            friendlyCriticalDamageHard = widgets.Input(content, 98, 320, 90, EditFriendlyCriticalDamage);
            changeButtons.Add(widgets.Button(content, "Apply", 196, 320, 77, 25, ApplyFriendlyCriticalDamage));
            widgets.Text(content, "CriticalDamageHint", "Additional bonus; native +50% preserved.", 0, 351, 273, 18, 11);
            BuildPvpLowerBound(PanelWidgets.Rect(content, "LowerBoundControls", 0, 376, 273, 238));
            BuildPvpElementalEditor(content);
            BuildPvpDotEditor();
        }

        private void RefreshPvpBalance(SettingsSnapshot snapshot)
        {
            RefreshPvpLowerBound(snapshot);
            RefreshPvpElements(snapshot);
            if (!friendlyGuardMp || !friendlyPerfectGuard) return;
            friendlyGuardMp.interactable = friendlyPerfectGuard.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyGuardMpDraft) friendlyGuardMp.SetTextWithoutNotify(snapshot.FriendlyFire.GuardMpNumber);
            if (!friendlyPerfectGuardDraft) friendlyPerfectGuard.SetTextWithoutNotify(snapshot.FriendlyFire.PerfectGuard.Number);
            friendlyAmplificationSoft.interactable = friendlyAmplificationHard.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyAmplificationDraft)
            {
                friendlyAmplificationSoft.SetTextWithoutNotify(snapshot.FriendlyFire.Amplification.SoftNumber);
                friendlyAmplificationHard.SetTextWithoutNotify(snapshot.FriendlyFire.Amplification.HardNumber);
            }
            friendlyCriticalDamageSoft.interactable = friendlyCriticalDamageHard.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyCriticalDamageDraft)
            {
                friendlyCriticalDamageSoft.SetTextWithoutNotify(snapshot.FriendlyFire.CriticalDamage.SoftNumber);
                friendlyCriticalDamageHard.SetTextWithoutNotify(snapshot.FriendlyFire.CriticalDamage.HardNumber);
            }
        }

        private void BuildPvpElementalEditor(RectTransform content)
        {
            AddCheckbox("Elemental stat limits", 0, 622, 273, 25, s => s.FriendlyFire.Elements.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire elements", parent: content);
            widgets.Text(content, "ElementSoft", "Soft limit", 0, 653, 88, 18, 11);
            widgets.Text(content, "ElementHard", "Hard limit", 94, 653, 88, 18, 11);
            string[] labels = { "Physical", "Flame", "Ice", "Lightning" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float y = 677 + i * 63;
                widgets.Text(content, "Element" + i, labels[i], 0, y, 273, 18, 11);
                friendlyElementSoft[i] = widgets.Input(content, 0, y + 20, 88, text => friendlyElementDraft[index] = true);
                friendlyElementHard[i] = widgets.Input(content, 94, y + 20, 88, text => friendlyElementDraft[index] = true);
                changeButtons.Add(widgets.Button(content, "Apply", 188, y + 20, 40, 25, () => ApplyFriendlyElement(index)));
                changeButtons.Add(widgets.Button(content, "Reset", 233, y + 20, 40, 25,
                    () => Execute("/one friendlyfire elements " + ((PvpElement)index).ToString().ToLowerInvariant() + " reset", true,
                        clearInputOnSuccess: () => ClearFriendlyElement(index))));
            }
            widgets.Text(content, "ElementsHint", "Effective participant stats during balanced PvP; also affects their PvE damage. Saved stats unchanged.", 0, 933, 273, 48, 10);
        }

        private void RefreshPvpElements(SettingsSnapshot snapshot)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!friendlyElementSoft[i] || !friendlyElementHard[i]) continue;
                friendlyElementSoft[i].interactable = friendlyElementHard[i].interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
                if (friendlyElementDraft[i]) continue;
                var limit = snapshot.FriendlyFire.Elements.GetLimit((PvpElement)i);
                friendlyElementSoft[i].SetTextWithoutNotify(limit.SoftNumber);
                friendlyElementHard[i].SetTextWithoutNotify(limit.HardNumber);
            }
        }

        private void ApplyFriendlyElement(int index) => Execute("/one friendlyfire elements " +
            ((PvpElement)index).ToString().ToLowerInvariant() + " limits " + friendlyElementSoft[index].text.Trim() + " " +
            friendlyElementHard[index].text.Trim(), true, clearInputOnSuccess: () => ClearFriendlyElement(index));

        private void ClearFriendlyElement(int index)
        {
            friendlyElementDraft[index] = false;
            if (friendlyElementSoft[index]) friendlyElementSoft[index].SetTextWithoutNotify("");
            if (friendlyElementHard[index]) friendlyElementHard[index].SetTextWithoutNotify("");
        }

        private void ClearFriendlyElements()
        {
            for (int i = 0; i < 4; i++) ClearFriendlyElement(i);
        }

        private void EditFriendlyGuardMp(string text) => friendlyGuardMpDraft = true;
        private void EditFriendlyRefill(string text) => friendlyRefillDraft = true;
        private void ApplyFriendlyRefill() => Execute("/one friendlyfire refill " + friendlyRefill.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyRefill);
        private void ClearFriendlyRefill() { friendlyRefillDraft = false; if (friendlyRefill) friendlyRefill.SetTextWithoutNotify(""); }
        private void ApplyFriendlyGuardMp() => Execute("/one friendlyfire guard-mp " + friendlyGuardMp.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyGuardMp);
        private void ClearFriendlyGuardMp() { friendlyGuardMpDraft = false; if (friendlyGuardMp) friendlyGuardMp.SetTextWithoutNotify(""); }
        private void EditFriendlyPerfectGuard(string text) => friendlyPerfectGuardDraft = true;
        private void ApplyFriendlyPerfectGuard() => Execute("/one friendlyfire perfect-guard multiplier " + friendlyPerfectGuard.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyPerfectGuard);
        private void ClearFriendlyPerfectGuard() { friendlyPerfectGuardDraft = false; if (friendlyPerfectGuard) friendlyPerfectGuard.SetTextWithoutNotify(""); }
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
        private void EditFriendlyCriticalDamage(string text) => friendlyCriticalDamageDraft = true;
        private void ApplyFriendlyCriticalDamage() => Execute("/one friendlyfire critical-damage limits " +
            friendlyCriticalDamageSoft.text.Trim() + " " + friendlyCriticalDamageHard.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyCriticalDamage);
        private void ClearFriendlyCriticalDamage()
        {
            friendlyCriticalDamageDraft = false;
            if (friendlyCriticalDamageSoft) friendlyCriticalDamageSoft.SetTextWithoutNotify("");
            if (friendlyCriticalDamageHard) friendlyCriticalDamageHard.SetTextWithoutNotify("");
        }
    }
}
