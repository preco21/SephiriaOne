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
        private Button reviveAll, combatResetKda, giveLargePotions;

        private void BuildCombatEditor()
        {
            combatResetKda = widgets.Button(pageRoot, "Reset K/D/A", 16, 0, 135, 24,
                () => Execute("/one friendlyfire resetkda", true));
            reviveAll = widgets.Button(pageRoot, "Revive all players", 162, 0, 127, 24, () => Execute("/one reviveall", true));
            AddCheckbox("Friendly fire", 16, 25, 174, 21, s => s.FriendlyFire.Enabled, s => s.FriendlyFireAvailable, "/one friendlyfire",
                s => s.CanMutate || CombatMatchRuntime.IsRunning && s.HostActive && s.SessionIdentity != null);
            resetAll = widgets.Button(pageRoot, "Reset", 200, 25, 89, 21, () => Execute("/one friendlyfire reset", true));
            BuildFriendlyDamageEditor();
            BuildPvpSuppliesEditor();
            BuildParticipantsEditor();
        }

        private void BuildPvpSuppliesEditor()
        {
            giveLargePotions = widgets.Button(pageRoot, "Give Large Potion to all", 16, 154, 273, 25,
                () => Execute("/one friendlyfire potion", true));
            AddCheckbox("Auto Large Potion", 16, 188, 273, 23, s => s.FriendlyFire.AutoPotion,
                s => s.FriendlyFireAvailable && LargePotionGrant.Available, "/one friendlyfire auto-potion");
            AddCheckbox("Infinite PvP HP potions", 16, 220, 273, 23, s => s.FriendlyFire.InfiniteHpPotions,
                s => s.FriendlyFireAvailable && RabbitPotionFeature.Available, "/one friendlyfire infinite-hp");
        }

        private void BuildFriendlyDamageEditor()
        {
            AddCheckbox("Block lobby HP/MP recovery", 16, 58, 273, 21, s => s.FriendlyFire.BlockLobbyRegen,
                s => s.FriendlyFireAvailable && LobbyRegenFeature.Available, "/one friendlyfire block-lobby-regen");
            widgets.Text(pageRoot, "FriendlyPercent", "Allied damage (%)", 16, 86, 116, 25, 11);
            amount = widgets.Input(pageRoot, 136, 86, 62, EditFriendlyDamage);
            changeButtons.Add(widgets.Button(pageRoot, "Apply damage", 204, 86, 85, 25, ApplyFriendlyDamage));
            var track = PanelWidgets.Rect(pageRoot, "AlliedDamageSlider", 24, 124, 255, 14);
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
        }

        private void RefreshCombat(SettingsSnapshot snapshot)
        {
            if (page != 9) return;
            if (giveLargePotions) giveLargePotions.interactable = LargePotionGrant.CanExecute;
            if (reviveAll) reviveAll.interactable = ReviveAllAction.CanExecute;
            if (combatResetKda) combatResetKda.interactable = snapshot.HostActive && snapshot.SessionIdentity != null && SessionSettings.CanResetFriendlyFireScores;
            if (!friendlyDamage) return;
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
        private void ApplyFriendlyDamage() => Execute("/one friendlyfire damage " + amount.text.Trim(), true,
            clearInputOnSuccess: ClearFriendlyDamage);
        private void ClearFriendlyDamage() { friendlyDraft = false; if (amount) amount.SetTextWithoutNotify(""); }
    }
}
