using System.Globalization;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {

        private void BuildMerchantEditor()
        {
            widgets.Button(pageRoot, "<", 16, 0, 24, 23, () => MoveMerchantSelection(-1));
            widgets.Button(pageRoot, ">", 265, 0, 24, 23, () => MoveMerchantSelection(1));
            selection = widgets.Text(pageRoot, "MerchantSelection", "", 47, 1, 212, 24, 12);
            selection.text = L.T(MerchantCatalog.All[merchantIndex].Name);
            AddCheckbox("Merchant", 16, 27, 178, 22,
                s => s.Merchants[MerchantCatalog.All[merchantIndex].Id].Enabled, s => s.MerchantsAvailable, MerchantPrefix());
            resetAll = widgets.Button(pageRoot, "Reset selected", 206, 27, 83, 22,
                () => Execute(MerchantPrefix() + " reset", true));
            AddCheckbox("Guarantee", 16, 54, 273, 22,
                s => s.Merchants[MerchantCatalog.All[merchantIndex].Id].Guarantee,
                s => s.MerchantsAvailable && MerchantCatalog.All[merchantIndex].HasGuarantee, MerchantPrefix() + " guarantee");
            amount = widgets.Input(pageRoot, 16, 81, 78, draft.Edit);
            changeButtons.Add(widgets.Button(pageRoot, "Set chance", 103, 81, 186, 25,
                () => Execute(MerchantPrefix() + " chance " + draft.Text, true)));
            changeButtons.Add(widgets.Button(pageRoot, "Set first floor", 16, 111, 132, 22,
                () => Execute(MerchantPrefix() + " from " + draft.Text, true)));
            changeButtons.Add(widgets.Button(pageRoot, "Set run limit", 157, 111, 132, 22,
                () => Execute(MerchantPrefix() + " limit " + draft.Text, true)));
            widgets.Text(pageRoot, "MerchantHelp", "Chance: 0..100%. First eligible floor: 1..1000. Run limit: 0..1000 (0 = unlimited).",
                16, 137, 273, 25, 9);
            widgets.Text(pageRoot, "MerchantStatus", "Current setting and behavior", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
        }

        private string MerchantPrefix() => "/one merchant " + MerchantCatalog.All[merchantIndex].Id;

        private void MoveMerchantSelection(int delta)
        {
            merchantIndex = (merchantIndex + delta + MerchantCatalog.All.Count) % MerchantCatalog.All.Count;
            SelectPage(7);
        }

        private string MerchantValues(SettingsSnapshot snapshot)
        {
            MerchantDefinition definition = MerchantCatalog.All[merchantIndex];
            MerchantSettings settings = snapshot.Merchants[definition.Id];
            return L.F("{0}: {1}", L.T(definition.Name), L.T(settings.Enabled ? "ON" : "OFF")) +
                "\n" + L.F("Guarantee: {0}", L.T(settings.Guarantee && definition.HasGuarantee ? "ON" : "OFF")) +
                "\n" + L.F("Chance: {0}%", settings.Chance) +
                "\n" + L.F("First eligible floor: {0}", settings.FirstFloor) +
                "\n" + L.F("Per-run limit: {0}", settings.MaxPerRun == 0 ? L.T("Unlimited") : settings.MaxPerRun.ToString(CultureInfo.InvariantCulture)) +
                "\n\n" + (settings.Guarantee && definition.HasGuarantee ?
                    L.T("Each enabled type guarantees one encounter per run on a random floor that meets its conditions. At 0%, only that encounter remains. Other eligible floors use this type's chance.") :
                    L.T("This type uses its own chance on each eligible floor; no encounter is guaranteed.")) +
                "\n\n" + L.T("Types roll independently and can share a floor. At most one extra merchant of each type per eligible floor.") +
                "\n\n" + L.T("The run limit counts all spawns of this type. With guarantee on, one slot is reserved while its encounter remains reachable. Guarantee off releases that slot for chance rolls.") +
                "\n\n" + L.T("First eligible floor means the main dungeon stage (1, 2, ...), not each map within it. Optional maps use the current main stage. Boss-only floors, lobby, towns, and training are excluded.") +
                "\n\n" + L.T("Added merchant base HP multipliers by main stage: 1 = x1, 2 = x2, 3 = x4, 4 = x5, 5 = x7, 6+ = x8. Maps within a stage share the same factor. Native stage and multiplayer bonuses still apply. Unknown progress uses x1. Health is set once at spawn.") +
                "\n\n" + L.T("Added merchants are hostile, cannot talk, and have no negotiation/crime penalty. Natural merchants keep their usual behavior.") +
                "\n\n" + L.F("Changing guarantee, chance or conditions keeps this type's spawn toggle. Reset selected restores spawns off, guarantee on, {0}%, first floor 1, and no run limit.", definition.DefaultChance) +
                "\n\n" + L.T("Guarantee changes preserve saved targets, completed encounters, spawn counts and consumed floor rolls. Turning it back on cannot reroll floors or bypass the run limit.") +
                "\n\n" + L.T("Off/reset stop future spawns; existing added merchants keep their penalty exemption until floor teardown.") +
                "\n\n" + L.T("The host spawns merchants for all players, including unmodified guests.") +
                (snapshot.MerchantsAvailable ? "" : "\n\n" + L.T("Merchant hooks unavailable; native behavior continues. See Player.log.")) +
                "\n\n" + L.T("Save these options from Presets for future sessions.");
        }
    }
}
