using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button batOff;
        private void BuildBatEditor()
        {
            widgets.Text(pageRoot, "BatTitle", "Wingless Bat HP steal", 16, 0, 273, 24, 14);
            changeButtons.Add(widgets.Button(pageRoot, "On", 16, 32, 82, 25, () => Execute("/one bat hp-steal on", true)));
            batOff = widgets.Button(pageRoot, "Off", 108, 32, 82, 25, () => Execute("/one bat hp-steal off", true));
            resetAll = widgets.Button(pageRoot, "Reset", 200, 32, 89, 25, () => Execute("/one bat reset", true));
            widgets.Text(pageRoot, "BatHelp", "On reduces the costume's HP steal from 5 to 1. Default off; other HP-steal sources are unchanged.", 16, 69, 273, 80, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }
        private static string BatValues(SettingsSnapshot snapshot) => SessionSettings.DescribeBat(snapshot.BatHpSteal) +
            "\n\n" + L.T("Applies only while Wingless Bat is equipped, including unmodified guests. Off/reset restores the native costume bonus. Equipment, buffs and stat multipliers keep their normal behavior.") +
            "\n\n" + L.T("The native costume selection preview still shows 5. The character's effective HP steal and healing use the host's setting.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
