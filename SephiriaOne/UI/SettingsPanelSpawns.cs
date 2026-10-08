namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private bool eventSpawnsSelected;
        private void BuildSpawnEditor()
        {
            void Select() { eventSpawnsSelected = !eventSpawnsSelected; SelectPage(10); }
            widgets.Button(pageRoot, "<", 16, 0, 24, 24, Select);
            widgets.Text(pageRoot, "SpawnTitle", eventSpawnsSelected ? "Random event rooms" : "Mystic Jar spawn chance", 44, 0, 217, 24, 12);
            widgets.Button(pageRoot, ">", 265, 0, 24, 24, Select);
            widgets.Text(pageRoot, "SpawnUnits", eventSpawnsSelected ? "xN (native chance × N); x1 restores native" : "0–100% or xN (native chance × N)", 16, 29, 273, 21, 10);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            string command = eventSpawnsSelected ? "/one events" : "/one jars";
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 120, 56, 77, 25, () => Execute(command + " chance " + draft.Text, true)));
            resetAll = widgets.Button(pageRoot, "Reset", 205, 56, 84, 25, () => Execute(command + " reset", true));
            widgets.Text(pageRoot, "SpawnHelp", eventSpawnsSelected ?
                "New chapters only. Existing floor data is not rerolled. x0 disables optional event rooms; x2 doubles their native probabilities." :
                "Reset restores each location's native chance. Changes affect future spawn checks only.", 16, 94, 273, 62, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private static string EventValues(SettingsSnapshot snapshot) => SessionSettings.DescribeEventSpawns(snapshot.EventSpawns) +
            "\n\n" + L.T("Optional event rooms include blood donation, obelisks and magic fountains. Native room selection and encounter conditions remain in effect.") +
            "\n\n" + L.T("Native odds: 4.3% for at least one room, including 0.3% for two rooms. These cumulative probabilities are multiplied, with a two-room maximum.") +
            "\n\n" + L.T("Scheduled merchants and travelers (including Collin), hostile merchant options and Mystic Jar chances are separate.") +
            "\n\n" + L.T("Generated encounters are saved and synchronized by the game. Unmodified guests use the same floor data, including after reconnecting.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");

        private static string JarValues(SettingsSnapshot snapshot) => SessionSettings.DescribeJarSpawns(snapshot.JarSpawns) +
            "\n\n" + L.T("Normal random Jar locations only. Chapter gates, guaranteed placements and hidden-room rewards keep native behavior.") +
            "\n\n" + L.T("100% fills eligible random locations; it does not create new locations or guarantee a Jar on every floor. 0% disables these random spawns.") +
            "\n\n" + L.T("Existing Jars are not rerolled or refilled. Host settings also apply to unmodified guests.") +
            "\n\n" + L.T("Save these options from Presets for future sessions.");
    }
}
