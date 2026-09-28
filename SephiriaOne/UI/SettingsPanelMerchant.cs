using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button merchantOff;

        private void BuildMerchantEditor()
        {
            widgets.Text(pageRoot, "MerchantTitle", "Extra Wandering Merchant", 16, 0, 273, 24, 14);
            widgets.Text(pageRoot, "MerchantScope", "Hostile encounter · normal dungeon rooms", 16, 29, 273, 23, 10);
            changeButtons.Add(widgets.Button(pageRoot, "On", 16, 55, 83, 25,
                () => Execute("/one merchant on", true)));
            merchantOff = widgets.Button(pageRoot, "Off", 111, 55, 83, 25,
                () => Execute("/one merchant off", true));
            resetAll = widgets.Button(pageRoot, "Reset", 206, 55, 83, 25,
                () => Execute("/one merchant reset", true));
            widgets.Text(pageRoot, "MerchantChance", "Chance %", 16, 91, 77, 23, 10);
            amount = widgets.Input(pageRoot, 99, 88, 78, draft.Edit);
            changeButtons.Add(widgets.Button(pageRoot, "Set chance", 187, 88, 102, 25,
                () => Execute("/one merchant chance " + draft.Text, true)));
            widgets.Text(pageRoot, "MerchantHelp", "One guaranteed each new run, then 0..100% (default 25%). Set chance keeps the toggle; Reset restores 25% and off. Evaluated floors never reroll.",
                16, 121, 273, 41, 10);
            widgets.Text(pageRoot, "MerchantStatus", "Current setting and behavior", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
        }

        private static string MerchantValues(SettingsSnapshot snapshot) =>
            L.F("Extra Wandering Merchant: {0}", L.T(snapshot.MerchantSpawns ? "ON" : "OFF")) +
            "\n" + L.F("Later-floor spawn chance: {0}%", snapshot.MerchantSpawnChance) +
            "\n" + L.T("One encounter is guaranteed each new run while enabled, even at 0%.") +
            "\n\n" + L.T("At most one extra merchant per eligible normal dungeon floor. Boss-only floors, lobby, towns, and training are excluded.") +
            "\n\n" + L.T("Added merchants are hostile combat encounters with 3x normal HP and cannot talk. Only these actors have no negotiation/crime penalty; natural merchants keep their usual behavior.") +
            "\n\n" + L.T("Off/reset stop future spawns; existing added merchants keep their penalty exemption until floor teardown.") +
            "\n\n" + L.T("The host spawns merchants for all players, including unmodified guests.") +
            (snapshot.MerchantsAvailable ? "" : "\n\n" + L.T("Merchant hooks unavailable; native behavior continues. See Player.log.")) +
            "\n\n" + L.T("Save this option from Presets for future sessions.");
    }
}
