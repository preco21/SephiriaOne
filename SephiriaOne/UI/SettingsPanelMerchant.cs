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
            widgets.Text(pageRoot, "MerchantStatus", "Current settings", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
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
                    L.T("One guaranteed encounter per type on a random eligible floor. At 0%, only the guarantee remains.") :
                    L.T("Chance rolls only; no guaranteed encounter.")) +
                "\n\n" + L.T("Types roll independently: up to 1 each per floor. The run cap includes the guarantee and reserves its slot while reachable.") +
                "\n\n" + L.T("Floor = main dungeon stage. Normal rooms only; excludes boss-only floors, towns and training.") +
                "\n\n" + L.T("Base HP by stage 1–6+: ×1/2/4/5/7/8. Native bonuses also apply; set once at spawn.") +
                "\n\n" + L.T("Added merchants are hostile with no negotiation penalty. Off/reset stops future spawns; existing merchants stay unchanged.") +
                (snapshot.MerchantsAvailable ? "" : "\n\n" + L.T("Merchant hooks unavailable; native behavior continues. See Player.log."));
        }
    }
}
