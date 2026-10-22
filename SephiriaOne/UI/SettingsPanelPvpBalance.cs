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

        private void BuildPvpBalanceEditor()
        {
            var container = PanelWidgets.Rect(pageRoot, "BalanceScroll", 16, 0, 286, PageHeight);
            container.gameObject.AddComponent<Image>().color = new Color32(17, 27, 40, 255);
            var scroll = container.gameObject.AddComponent<ScrollRect>();
            var viewport = PanelWidgets.Rect(container, "Viewport", 0, 0, 273, PageHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = PanelWidgets.Rect(viewport, "BalanceControls", 0, 0, 273, 510);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var track = PanelWidgets.Rect(container, "Scrollbar", 278, 0, 8, PageHeight);
            track.gameObject.AddComponent<Image>().color = new Color32(39, 59, 82, 255);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var handle = PanelWidgets.Rect(track, "Handle", 0, 0, 0, 0);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            handle.pivot = new Vector2(0.5f, 0.5f);
            var image = handle.gameObject.AddComponent<Image>(); image.color = PanelWidgets.Accent;
            bar.handleRect = handle; bar.targetGraphic = image; bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
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
            BuildPvpLowerBound(PanelWidgets.Rect(content, "LowerBoundControls", 0, 264, 273, 238));
            BuildPvpDotEditor();
        }

        private void RefreshPvpBalance(SettingsSnapshot snapshot)
        {
            RefreshPvpLowerBound(snapshot);
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
    }
}
