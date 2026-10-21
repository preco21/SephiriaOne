using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField friendlyGuardMp;
        private bool friendlyGuardMpDraft;
        private TMP_InputField friendlyAmplificationSoft, friendlyAmplificationHard;
        private bool friendlyAmplificationDraft;

        private void BuildPvpBalanceEditor()
        {
            // Keep added controls inside the same compact editor as the DoT tab.
            var container = PanelWidgets.Rect(pageRoot, "BalanceSettings", 16, 80, 273, 82);
            container.gameObject.AddComponent<Image>().color = new Color32(17, 27, 40, 255);
            var scroll = container.gameObject.AddComponent<ScrollRect>();
            var viewport = PanelWidgets.Rect(container, "Viewport", 0, 0, 259, 82);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = PanelWidgets.Rect(viewport, "BalanceControls", 0, 0, 259, 200);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var track = PanelWidgets.Rect(container, "Scrollbar", 265, 0, 8, 82);
            track.gameObject.AddComponent<Image>().color = new Color32(39, 59, 82, 255);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var handle = PanelWidgets.Rect(track, "Handle", 0, 0, 0, 0);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            handle.pivot = new Vector2(0.5f, 0.5f);
            var image = handle.gameObject.AddComponent<Image>(); image.color = PanelWidgets.Accent;
            bar.handleRect = handle; bar.targetGraphic = image; bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            AddCheckbox("PvP balancing", 0, 0, 124, 21, s => s.FriendlyFire.Balance,
                s => s.FriendlyFireAvailable, "/one friendlyfire balance", parent: content);
            AddCheckbox("Hit-frequency scaling", 128, 0, 131, 21, s => s.FriendlyFire.HitFrequency,
                s => s.FriendlyFireAvailable, "/one friendlyfire hit-frequency", parent: content);
            widgets.Text(content, "FriendlyRefill", "PvP refill (%/s)", 0, 26, 116, 25, 11);
            friendlyRefill = widgets.Input(content, 120, 26, 58, EditFriendlyRefill);
            changeButtons.Add(widgets.Button(content, "Apply", 184, 26, 75, 25, ApplyFriendlyRefill));
            widgets.Text(content, "GuardMp", "Guard MP (%)", 0, 57, 116, 25, 11);
            friendlyGuardMp = widgets.Input(content, 120, 57, 58, EditFriendlyGuardMp);
            changeButtons.Add(widgets.Button(content, "Apply", 184, 57, 75, 25, ApplyFriendlyGuardMp));
            AddCheckbox("All Damage Amplified limit", 0, 88, 259, 21, s => s.FriendlyFire.Amplification.Enabled,
                s => s.FriendlyFireAvailable, "/one friendlyfire amplification", parent: content);
            widgets.Text(content, "AmplificationSoft", "Soft bonus (%)", 0, 113, 82, 18, 10);
            widgets.Text(content, "AmplificationHard", "Hard bonus (%)", 88, 113, 82, 18, 10);
            friendlyAmplificationSoft = widgets.Input(content, 0, 132, 82, EditFriendlyAmplification);
            friendlyAmplificationHard = widgets.Input(content, 88, 132, 82, EditFriendlyAmplification);
            changeButtons.Add(widgets.Button(content, "Apply", 176, 132, 83, 25, ApplyFriendlyAmplification));
            widgets.Text(content, "AmplificationHint", "Bonus 100% = +100%, not C-menu total. Requires PvP balancing.", 0, 161, 259, 39, 10);
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
