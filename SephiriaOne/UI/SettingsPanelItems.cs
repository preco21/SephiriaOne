using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private void BuildItemEditor()
        {
            widgets.Text(pageRoot, "ItemsTitle", "Unlock given items", 16, 0, 275, 24, 14);
            AddCheckbox("Unlock given items", 16, 32, 174, 25, s => s.ItemUnlock, s => s.ItemRestrictionsAvailable, "/one items unlock");
            resetAll = widgets.Button(pageRoot, "Reset", 200, 32, 89, 25, () => Execute("/one items reset", true));
            widgets.Text(pageRoot, "ItemsHelp", "Default off. Host settings apply to all players, including unmodified guests.", 16, 69, 273, 80, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private static string ItemValues(SettingsSnapshot snapshot) => SessionSettings.DescribeItemUnlock(snapshot.ItemUnlock) +
            "\n\n" + L.T("Unlocks starting/given-item sale restrictions and owner-bound drops, including Wishing Fountain items. Fountain items can already be sold normally.") +
            "\n\n" + L.T("The same native flag also unlocks Fountain storage for given items. Intrinsic item destruction/no-drop rules and merchant trade rules still apply.") +
            "\n\n" + L.T("Costume curse tablets keep their intrinsic no-drop rule on unmodified guests.") +
            "\n\n" + L.T("Off/reset restores recorded restrictions without undoing completed trades. Costume item grant/removal and restocking still follow the game.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
