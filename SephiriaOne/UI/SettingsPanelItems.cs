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
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private static string ItemValues(SettingsSnapshot snapshot) => SessionSettings.DescribeItemUnlock(snapshot.ItemUnlock) +
            "\n\n" + L.T("Allows sharing, selling and Fountain storage for given items. Fountain items already allow selling.") +
            "\n\n" + L.T("Intrinsic no-drop rules, including costume curse tablets, still apply.") +
            "\n\n" + L.T("Off/reset restores restrictions; completed trades stay completed.");
    }
}
