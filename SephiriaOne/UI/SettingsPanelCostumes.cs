using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button collinReset;
        private void BuildCostumeEditor()
        {
            widgets.Text(pageRoot, "BatTitle", "Wingless Bat HP steal", 16, 0, 273, 24, 14);
            AddCheckbox("Wingless Bat HP steal", 16, 26, 174, 25, s => s.BatHpSteal, s => s.BatAvailable, "/one bat hp-steal");
            resetAll = widgets.Button(pageRoot, "Reset", 200, 26, 89, 25, () => Execute("/one bat reset", true));
            widgets.Text(pageRoot, "CollinTitle", "Collin starting artifact", 16, 68, 273, 24, 14);
            AddCheckbox("Collin starting artifact", 16, 94, 174, 25, s => s.CollinStartingArtifact, s => s.CollinAvailable, "/one collin");
            collinReset = widgets.Button(pageRoot, "Reset", 200, 94, 89, 25, () => Execute("/one collin reset", true));
            widgets.Text(pageRoot, "CollinHelp", "Mole / Farmer Squirrel / Turtle. Next equip or fresh-run restock.", 16, 125, 273, 36, 9);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }
        private static string CostumeValues(SettingsSnapshot snapshot) => SessionSettings.DescribeCollin(snapshot.CollinStartingArtifact) +
            (snapshot.CollinAvailable ? "" : "\n" + L.T("Collin compatibility checks failed. Off/reset remain available; see Player.log.")) +
            "\n\n" + L.T("Costume Collin is removed on costume change. Other copies and saved-run inventories stay unchanged.") +
            "\n\n" + L.T("Multiple crests share the native Collin NPC.") +
            "\n\n" + SessionSettings.DescribeBat(snapshot.BatHpSteal) +
            (snapshot.BatAvailable ? "" : "\n" + L.T("Bat costume compatibility checks failed. Off/reset remain available; see Player.log.")) +
            "\n\n" + L.T("Bat HP steal: 5 → 1 while equipped. Off/reset restores 5. The costume preview still shows 5.");
    }
}
