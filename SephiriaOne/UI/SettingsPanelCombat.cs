using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Slider friendlyDamage;
        private TMP_Text friendlyPercent;
        private bool friendlyDraft;
        private Button reviveAll;

        private void BuildCombatEditor()
        {
            widgets.Text(pageRoot, "FriendlyTitle", "Friendly fire", 16, 0, 273, 24, 14);
            AddCheckbox("Friendly fire", 16, 28, 174, 25, s => s.FriendlyFire.Enabled, s => s.FriendlyFireAvailable, "/one friendlyfire",
                s => s.CanMutate || DeathmatchRuntime.IsRunning && s.HostActive && s.SessionIdentity != null);
            resetAll = widgets.Button(pageRoot, "Reset", 200, 28, 89, 25, () => Execute("/one friendlyfire reset", true));
            friendlyPercent = widgets.Text(pageRoot, "FriendlyPercent", "", 16, 62, 273, 22, 11);
            var track = PanelWidgets.Rect(pageRoot, "AlliedDamageSlider", 24, 90, 255, 24);
            track.gameObject.AddComponent<Image>().color = new Color32(48, 66, 88, 255);
            friendlyDamage = track.gameObject.AddComponent<Slider>();
            friendlyDamage.minValue = 0; friendlyDamage.maxValue = 300; friendlyDamage.wholeNumbers = true;
            // Slider stretches the orthogonal anchors. Zero height delta keeps
            // the handle inside the track instead of doubling its height.
            var handle = PanelWidgets.Rect(track, "Handle", 0, 0, 14, 0);
            handle.pivot = new Vector2(0.5f, 0.5f);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = PanelWidgets.Accent;
            friendlyDamage.handleRect = handle; friendlyDamage.targetGraphic = handleImage;
            friendlyDamage.direction = Slider.Direction.LeftToRight;
            friendlyDamage.onValueChanged.AddListener(value => { friendlyDraft = true; RefreshFriendlyPercent(); });
            changeButtons.Add(widgets.Button(pageRoot, "Apply damage", 16, 126, 135, 25, () =>
                Execute("/one friendlyfire damage " + ((int)friendlyDamage.value).ToString(CultureInfo.InvariantCulture), true)));
            reviveAll = widgets.Button(pageRoot, "Revive all players", 162, 126, 127, 25, () => Execute("/one reviveall", true));
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private void RefreshCombat(SettingsSnapshot snapshot)
        {
            if (!friendlyDamage) return;
            if (reviveAll) reviveAll.interactable = ReviveAllAction.CanExecute;
            friendlyDamage.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyDraft) friendlyDamage.SetValueWithoutNotify(snapshot.FriendlyFire.DamagePercent);
            RefreshFriendlyPercent();
        }
        private void RefreshFriendlyPercent() => friendlyPercent.text = L.F("Allied damage: {0}%", (int)friendlyDamage.value);
        private static string CombatValues(SettingsSnapshot snapshot) => SessionSettings.DescribeFriendlyFire(snapshot.FriendlyFire) +
            (ReviveAllFeature.Available ? "" : "\n\n" + L.T("Revive-all compatibility checks failed. Inspect Player.log; no players were changed.")) +
            "\n\n" + L.T("Default off. Host settings apply to all players, including unmodified guests.") +
            "\n\n" + L.T("0–300% (100% = normal)") +
            "\n\n" + L.T("Revive all restores dead players at full HP, including the host. It also works with friendly fire off. Living players and KDA totals are unchanged.") +
            "\n\n" + L.T("Melee, projectiles and supported burn/debuff items can affect other players. Healing and unrelated automatic skills keep native targeting.") +
            "\n\n" + L.T("Companions attack other players, never their owner. Off or 0% stops this targeting and blocks their hits on players immediately.") +
            "\n\n" + L.T("Damage, including debuff ticks, scales after defenses and before shields. Off or 0% blocks new allied debuffs and damage. Reflection, immediate debuff damage and burning-death explosions are allowed with recursion limits.") +
            "\n\n" + L.T("Confirmed friendly-fire kills are announced in chat. Safe areas and invulnerability retain native protection.") +
            "\n\n" + L.T("Player kill logs show K/D/A. Assists count each player who damaged the victim during that life. Toggling friendly fire resets all scores and pending assists.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
