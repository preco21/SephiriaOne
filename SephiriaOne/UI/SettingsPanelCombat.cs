using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Slider friendlyDamage;
        private bool friendlyDraft;
        private Button reviveAll;

        private void BuildCombatEditor()
        {
            widgets.Text(pageRoot, "FriendlyTitle", "Friendly fire", 16, 0, 273, 24, 14);
            AddCheckbox("Friendly fire", 16, 28, 174, 25, s => s.FriendlyFire.Enabled, s => s.FriendlyFireAvailable, "/one friendlyfire",
                s => s.CanMutate || DeathmatchRuntime.IsRunning && s.HostActive && s.SessionIdentity != null);
            resetAll = widgets.Button(pageRoot, "Reset", 200, 28, 89, 25, () => Execute("/one friendlyfire reset", true));
            widgets.Text(pageRoot, "FriendlyPercent", "Allied damage (%)", 16, 62, 180, 25, 11);
            amount = widgets.Input(pageRoot, 200, 62, 89, EditFriendlyDamage);
            var track = PanelWidgets.Rect(pageRoot, "AlliedDamageSlider", 24, 90, 255, 24);
            track.gameObject.AddComponent<Image>().color = new Color32(48, 66, 88, 255);
            friendlyDamage = track.gameObject.AddComponent<Slider>();
            friendlyDamage.minValue = 0; friendlyDamage.maxValue = 300; friendlyDamage.wholeNumbers = false;
            // Slider stretches the orthogonal anchors. Zero height delta keeps
            // the handle inside the track instead of doubling its height.
            var handle = PanelWidgets.Rect(track, "Handle", 0, 0, 14, 0);
            handle.pivot = new Vector2(0.5f, 0.5f);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = PanelWidgets.Accent;
            friendlyDamage.handleRect = handle; friendlyDamage.targetGraphic = handleImage;
            friendlyDamage.direction = Slider.Direction.LeftToRight;
            friendlyDamage.onValueChanged.AddListener(DragFriendlyDamage);
            changeButtons.Add(widgets.Button(pageRoot, "Apply damage", 16, 126, 135, 25, ApplyFriendlyDamage));
            reviveAll = widgets.Button(pageRoot, "Revive all players", 162, 126, 127, 25, () => Execute("/one reviveall", true));
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private void RefreshCombat(SettingsSnapshot snapshot)
        {
            if (!friendlyDamage) return;
            if (reviveAll) reviveAll.interactable = ReviveAllAction.CanExecute;
            amount.interactable = friendlyDamage.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyDraft)
            {
                friendlyDamage.SetValueWithoutNotify((float)snapshot.FriendlyFire.DamagePercent);
                amount.SetTextWithoutNotify(snapshot.FriendlyFire.Number);
            }
        }
        private void EditFriendlyDamage(string text)
        {
            friendlyDraft = true;
            if (FriendlyFireCommand.TryPercent(text, out decimal percent)) friendlyDamage.SetValueWithoutNotify((float)percent);
        }
        private void DragFriendlyDamage(float value)
        {
            friendlyDraft = true;
            decimal percent = Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
            friendlyDamage.SetValueWithoutNotify((float)percent);
            amount.SetTextWithoutNotify(percent.ToString("0.##", CultureInfo.InvariantCulture));
        }
        private void ApplyFriendlyDamage() => Execute("/one friendlyfire damage " + amount.text.Trim(), true);
        private static string CombatValues(SettingsSnapshot snapshot) => SessionSettings.DescribeFriendlyFire(snapshot.FriendlyFire) +
            (ReviveAllFeature.Available ? "" : "\n\n" + L.T("Revive-all compatibility checks failed. Inspect Player.log; no players were changed.")) +
            "\n\n" + L.T("0–300% · 2 decimals (e.g. 0.1%)") +
            "\n\n" + L.T("Attacks, supported debuffs and reflection affect allies. Companions exclude their owner. Off/0% blocks allied hits and new debuffs.") +
            "\n\n" + L.T("Safe areas and invulnerability stay native. Toggling resets K/D/A.") +
            "\n\n" + L.T("Revive all: dead players return at full HP. Living players and K/D/A stay unchanged.");
    }
}
