using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField requitalCount;
        private TMP_Dropdown slatePicker;
        private bool requitalCountDraft;
        private Button requitalApply, requitalGive, requitalReset;

        private void BuildItemEditor()
        {
            widgets.Text(pageRoot, "ItemsTitle", "Unlock given items", 16, 0, 275, 24, 14);
            AddCheckbox("Unlock given items", 16, 32, 174, 25, s => s.ItemUnlock, s => s.ItemRestrictionsAvailable, "/one items unlock");
            resetAll = widgets.Button(pageRoot, "Reset", 200, 32, 89, 25, () => Execute("/one items reset", true));
            widgets.Text(pageRoot, "RequitalTitle", "Starting Slates", 16, 64, 110, 23, 12);
            AddCheckbox("Auto starting Slates", 16, 90, 174, 25, s => s.Requital.Automatic,
                s => s.RequitalAvailable, "/one slates auto");
            requitalReset = widgets.Button(pageRoot, "Reset", 200, 90, 89, 25, () => Execute("/one slates reset", true));
            slatePicker = widgets.Dropdown(pageRoot, 132, 64, 157, 25,
                new List<string> { L.T("Requital"), L.T("Heaven") }, index =>
                    Execute("/one slates type " + (index == 1 ? "heaven" : "requital"), true));
            requitalCount = widgets.Input(pageRoot, 16, 125, 60, _ => requitalCountDraft = true);
            requitalApply = widgets.Button(pageRoot, "Apply", 82, 125, 74, 25, () =>
                Execute("/one slates count " + requitalCount.text.Trim(), true, clearInputOnSuccess: ClearRequitalCount));
            requitalGive = widgets.Button(pageRoot, "Give now", 162, 125, 127, 25, () => Execute("/one slates give", true));
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private void RefreshRequital(SettingsSnapshot snapshot)
        {
            if (!requitalCount) return;
            // Inventory gifts have their own compatibility/readiness guard.
            // A failed restriction-unlock patch must not disable Slate controls.
            requitalCount.interactable = requitalApply.interactable = snapshot.CanMutate;
            requitalReset.interactable = snapshot.CanMutate;
            slatePicker.interactable = snapshot.CanMutate;
            slatePicker.SetValueWithoutNotify((int)snapshot.Requital.Kind);
            requitalGive.interactable = snapshot.HostActive && snapshot.SessionIdentity != null && RequitalRuntime.CanExecute;
            if (!requitalCountDraft) requitalCount.SetTextWithoutNotify(snapshot.Requital.Count.ToString(CultureInfo.InvariantCulture));
        }

        private void ClearRequitalCount()
        {
            requitalCountDraft = false;
            if (requitalCount) requitalCount.SetTextWithoutNotify("");
        }

        private static string ItemValues(SettingsSnapshot snapshot) => SessionSettings.DescribeItemUnlock(snapshot.ItemUnlock) +
            "\n\n" + L.T("Allows sharing, selling and Fountain storage for given items. Fountain items already allow selling.") +
            "\n\n" + L.T("Intrinsic no-drop rules, including costume curse tablets, still apply.") +
            "\n\n" + L.T("Off/reset restores restrictions; completed trades stay completed.") +
            "\n\n" + SessionSettings.DescribeRequital(snapshot.Requital) +
            "\n" + L.T("In-run auto additions start next run. Changing type or reducing count retracts old automatic copies. Give now is not retracted.") +
            (snapshot.RequitalAvailable ? "" : "\n" + L.T("Selected Slate compatibility checks failed. Off/reset remain available; see Player.log."));
    }
}
