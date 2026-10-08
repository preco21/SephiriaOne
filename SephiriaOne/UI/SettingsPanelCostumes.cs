using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button batOn, batOff, collinOn, collinOff, collinReset;
        private void BuildCostumeEditor()
        {
            widgets.Text(pageRoot, "BatTitle", "Wingless Bat HP steal", 16, 0, 273, 24, 14);
            batOn = widgets.Button(pageRoot, "On", 16, 26, 82, 25, () => Execute("/one bat hp-steal on", true));
            batOff = widgets.Button(pageRoot, "Off", 108, 26, 82, 25, () => Execute("/one bat hp-steal off", true));
            resetAll = widgets.Button(pageRoot, "Reset", 200, 26, 89, 25, () => Execute("/one bat reset", true));
            widgets.Text(pageRoot, "CollinTitle", "Collin starting artifact", 16, 68, 273, 24, 14);
            collinOn = widgets.Button(pageRoot, "On", 16, 94, 82, 25, () => Execute("/one collin on", true));
            collinOff = widgets.Button(pageRoot, "Off", 108, 94, 82, 25, () => Execute("/one collin off", true));
            collinReset = widgets.Button(pageRoot, "Reset", 200, 94, 89, 25, () => Execute("/one collin reset", true));
            widgets.Text(pageRoot, "CollinHelp", "Mole / Farmer Squirrel / Turtle. Applies on the next costume equip or fresh-run restock. Default off.", 16, 125, 273, 36, 9);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }
        private static string CostumeValues(SettingsSnapshot snapshot) => SessionSettings.DescribeCollin(snapshot.CollinStartingArtifact) +
            (snapshot.CollinAvailable ? "" : "\n" + L.T("Collin compatibility checks failed. Off/reset remain available; see Player.log.")) +
            "\n\n" + L.T("Switch away from these costumes to remove the granted Collin. Independently obtained copies are preserved. Saved runs resume their native inventory; no extra Collin is injected.") +
            "\n\n" + L.T("Multiple Collin crests keep the game's shared NPC and leader behavior; a separate companion per player is not guaranteed.") +
            "\n\n" + SessionSettings.DescribeBat(snapshot.BatHpSteal) +
            (snapshot.BatAvailable ? "" : "\n" + L.T("Bat costume compatibility checks failed. Off/reset remain available; see Player.log.")) +
            "\n\n" + L.T("Applies only while Wingless Bat is equipped, including unmodified guests. Off/reset restores the native costume bonus. Equipment, buffs and stat multipliers keep their normal behavior.") +
            "\n\n" + L.T("The native costume selection preview still shows 5. The character's effective HP steal and healing use the host's setting.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
