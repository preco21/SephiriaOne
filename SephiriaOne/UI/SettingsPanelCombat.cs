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
        private Button friendlyOff;
        private bool friendlyDraft;

        private void BuildCombatEditor()
        {
            widgets.Text(pageRoot, "FriendlyTitle", "Friendly fire", 16, 0, 273, 24, 14);
            changeButtons.Add(widgets.Button(pageRoot, "On", 16, 28, 82, 25, () => Execute("/one friendlyfire on", true)));
            friendlyOff = widgets.Button(pageRoot, "Off", 108, 28, 82, 25, () => Execute("/one friendlyfire off", true));
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
            widgets.Text(pageRoot, "FriendlyRange", "0–300% (100% = normal)", 162, 127, 128, 24, 9);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private void RefreshCombat(SettingsSnapshot snapshot)
        {
            if (friendlyOff) friendlyOff.interactable = snapshot.CanMutate;
            if (!friendlyDamage) return;
            friendlyDamage.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            if (!friendlyDraft) friendlyDamage.SetValueWithoutNotify(snapshot.FriendlyFire.DamagePercent);
            RefreshFriendlyPercent();
        }
        private void RefreshFriendlyPercent() => friendlyPercent.text = L.F("Allied damage: {0}%", (int)friendlyDamage.value);
        private static string CombatValues(SettingsSnapshot snapshot) => SessionSettings.DescribeFriendlyFire(snapshot.FriendlyFire) +
            "\n\n" + L.T("Default off. Host settings apply to all players, including unmodified guests.") +
            "\n\n" + L.T("Melee/projectile hits can hurt other players and allies. Enemy-seeking skills keep their normal targets; self and enemy damage are unchanged.") +
            "\n\n" + L.T("Companions attack other players, never their owner. Off or 0% stops this targeting and blocks their hits on players immediately.") +
            "\n\n" + L.T("Damage scales after defenses, before shields. Zero blocks allied hits. Native reflection can return damage to the attacker; repeated reflection and other nested ally hits are blocked.") +
            "\n\n" + L.T("Confirmed friendly-fire kills are announced in chat. Safe areas and invulnerability retain native protection.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
