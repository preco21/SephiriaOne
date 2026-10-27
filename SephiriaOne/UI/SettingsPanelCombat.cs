using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField coverCount;
        private bool coverCountDraft;
        private Slider friendlyDamage;
        private bool friendlyDraft;
        private Button reviveAll, combatResetKda, giveLargePotions, teleportAll;

        private void BuildCombatEditor()
        {
            coverCountDraft = false;
            var outer = pageRoot;
            var content = widgets.ScrollSection(outer, "CombatScroll", 10, 0, 273, PageHeight, 510);
            // Existing Combat rows use a 16-unit form origin. Align that origin
            // to the scroll content while retaining their shared layout values.
            pageRoot = PanelWidgets.Rect(content, "CombatForm", -16, 0, 289, 510);
            combatResetKda = widgets.Button(pageRoot, "Reset K/D/A", 16, 0, 135, 24,
                () => Execute("/one friendlyfire resetkda", true));
            reviveAll = widgets.Button(pageRoot, "Revive all players", 162, 0, 127, 24, () => Execute("/one reviveall", true));
            AddCheckbox("Friendly fire", 16, 25, 174, 21, s => s.FriendlyFire.Enabled, s => s.FriendlyFireAvailable, "/one friendlyfire",
                s => s.CanMutate || CombatMatchRuntime.IsRunning && s.HostActive && s.SessionIdentity != null);
            resetAll = widgets.Button(pageRoot, "Reset", 200, 25, 89, 21, () => Execute("/one friendlyfire reset", true));
            BuildFriendlyDamageEditor();
            BuildPvpSuppliesEditor();
            teleportAll = widgets.Button(pageRoot, "Teleport all players", 156, 145, 133, 25,
                () => Execute("/one teleportall", true));
            AddCheckbox("Combat Mode", 16, 278, 273, 21, s => s.FriendlyFire.CombatMode,
                s => s.FriendlyFireAvailable && CombatModeFeature.Available, "/one friendlyfire combat-mode",
                s => s.HostActive && s.SessionIdentity != null);
            widgets.Text(pageRoot, "CombatModeHint", "Blocks Map/inventory shortcuts and map travel. Also affects sprint/recovery. Existing and special-interaction menus may remain open.", 16, 306, 273, 54, 10);
            AddCheckbox("Temporary cover", 16, 372, 257, 24, s => s.FriendlyFire.Cover.Enabled,
                s => s.FriendlyFireAvailable && PvpCoverFeature.Available, "/one friendlyfire cover",
                s => s.HostActive && s.SessionIdentity != null);
            widgets.Text(pageRoot, "CoverCount", "Obstacles / level", 16, 406, 120, 25, 11);
            coverCount = widgets.Input(pageRoot, 142, 406, 55, _ => coverCountDraft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 203, 406, 70, 25,
                () => Execute("/one friendlyfire cover count " + coverCount.text.Trim(), true, clearInputOnSuccess: () => coverCountDraft = false)));
            widgets.Text(pageRoot, "CoverHint", "1–32 · Safe space may reduce the count. Removed when PvP ends. Cover also blocks nonparticipants.", 16, 440, 257, 38, 10);
            pageRoot = outer;
            BuildParticipantsEditor();
        }

        private void BuildPvpSuppliesEditor()
        {
            giveLargePotions = widgets.Button(pageRoot, "Give Large Potion to all", 16, 145, 133, 25,
                () => Execute("/one friendlyfire potion", true));
            AddCheckbox("Auto Large Potion", 16, 176, 273, 21, s => s.FriendlyFire.AutoPotion,
                s => s.FriendlyFireAvailable && LargePotionGrant.Available, "/one friendlyfire auto-potion");
            AddCheckbox("Infinite PvP HP potions", 16, 202, 273, 21, s => s.FriendlyFire.InfiniteHpPotions,
                s => s.FriendlyFireAvailable && RabbitPotionFeature.Available, "/one friendlyfire infinite-hp");
            widgets.Text(pageRoot, "PvpPotionPolicy", "PvP potion consumption also applies to Rabbit. Grants are separate.", 16, 228, 273, 42, 10);
        }

        private void BuildFriendlyDamageEditor()
        {
            AddCheckbox("Block lobby HP/MP recovery", 16, 58, 273, 21, s => s.FriendlyFire.BlockLobbyRegen,
                s => s.FriendlyFireAvailable && LobbyRegenFeature.Available, "/one friendlyfire block-lobby-regen");
            widgets.Text(pageRoot, "FriendlyPercent", "Allied damage (%)", 16, 86, 116, 25, 11);
            amount = widgets.Input(pageRoot, 136, 86, 62, EditFriendlyDamage);
            changeButtons.Add(widgets.Button(pageRoot, "Apply damage", 204, 86, 85, 25, ApplyFriendlyDamage));
            var track = PanelWidgets.Rect(pageRoot, "AlliedDamageSlider", 24, 124, 255, 14);
            PanelSurface.Add(track.gameObject, new Color32(48, 66, 88, 255), 5);
            friendlyDamage = track.gameObject.AddComponent<Slider>();
            friendlyDamage.minValue = 0; friendlyDamage.maxValue = 300; friendlyDamage.wholeNumbers = false;
            // Slider stretches the orthogonal anchors. Zero height delta keeps
            // the handle inside the track instead of doubling its height.
            var handle = PanelWidgets.Rect(track, "Handle", 0, 0, 14, 0);
            handle.pivot = new Vector2(0.5f, 0.5f);
            var handleImage = PanelSurface.Add(handle.gameObject, PanelWidgets.Accent, 4);
            friendlyDamage.handleRect = handle; friendlyDamage.targetGraphic = handleImage;
            friendlyDamage.direction = Slider.Direction.LeftToRight;
            friendlyDamage.onValueChanged.AddListener(DragFriendlyDamage);
        }

        private void RefreshCombat(SettingsSnapshot snapshot)
        {
            if (page != 9) return;
            if (coverCount && !coverCountDraft) coverCount.SetTextWithoutNotify(snapshot.FriendlyFire.Cover.Count.ToString(CultureInfo.InvariantCulture));
            if (giveLargePotions) giveLargePotions.interactable = LargePotionGrant.CanExecute;
            if (reviveAll) reviveAll.interactable = ReviveAllAction.CanExecute;
            // Travel is session-wide and does not depend on PvP compatibility
            // or selection. The shared command rechecks readiness on click.
            if (teleportAll) teleportAll.interactable = snapshot.HostActive && snapshot.SessionIdentity != null;
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
