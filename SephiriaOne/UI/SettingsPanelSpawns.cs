namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private void BuildJarEditor()
        {
            widgets.Text(pageRoot, "JarTitle", "Mystic Jar spawn chance", 16, 0, 273, 24, 14);
            widgets.Text(pageRoot, "JarUnits", "0–100% or xN (native chance × N)", 16, 29, 273, 21, 10);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 120, 56, 77, 25, () => Execute("/one jars chance " + draft.Text, true)));
            resetAll = widgets.Button(pageRoot, "Reset", 205, 56, 84, 25, () => Execute("/one jars reset", true));
            widgets.Text(pageRoot, "JarHelp", "Reset restores each location's native chance. Changes affect future spawn checks only.", 16, 94, 273, 62, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private static string JarValues(SettingsSnapshot snapshot) => SessionSettings.DescribeJarSpawns(snapshot.JarSpawns) +
            "\n\n" + L.T("Normal random Jar locations only. Chapter gates, guaranteed placements and hidden-room rewards keep native behavior.") +
            "\n\n" + L.T("100% fills eligible random locations; it does not create new locations or guarantee a Jar on every floor. 0% disables these random spawns.") +
            "\n\n" + L.T("Existing Jars are not rerolled or refilled. Host settings also apply to unmodified guests.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
