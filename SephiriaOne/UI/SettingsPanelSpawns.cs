namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private int spawnPage;
        private bool eventSpawnsSelected => spawnPage == 1;
        private bool projectilesSelected => spawnPage == 2;
        private void BuildSpawnEditor()
        {
            void Select(int delta) { spawnPage = (spawnPage + delta + 3) % 3; SelectPage(10); }
            widgets.Button(pageRoot, "<", 16, 0, 24, 24, () => Select(-1));
            widgets.Text(pageRoot, "SpawnTitle", projectilesSelected ? "Secondary projectiles" : eventSpawnsSelected ? "Random event rooms" : "Mystic Jar spawn chance", 44, 0, 217, 24, 12);
            widgets.Button(pageRoot, ">", 265, 0, 24, 24, () => Select(1));
            if (projectilesSelected) { BuildProjectileEditor(); return; }
            widgets.Text(pageRoot, "SpawnUnits", eventSpawnsSelected ? "xN (native chance × N); x1 restores native" : "0–100% or xN (native chance × N)", 16, 29, 273, 21, 10);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            string command = eventSpawnsSelected ? "/one events" : "/one jars";
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 120, 56, 77, 25, () => Execute(command + " chance " + draft.Text, true)));
            resetAll = widgets.Button(pageRoot, "Reset", 205, 56, 84, 25, () => Execute(command + " reset", true));
            widgets.Text(pageRoot, "SpawnHelp", eventSpawnsSelected ?
                "New chapters only. x0 disables events; x1 restores native odds." :
                "Future spawn checks only. Reset restores native odds.", 16, 94, 273, 62, 10);
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }

        private bool SpawnAvailable(SettingsSnapshot snapshot) => projectilesSelected ? snapshot.ProjectilesAvailable :
            eventSpawnsSelected ? snapshot.EventSpawnsAvailable : snapshot.JarSpawnsAvailable;
        private string SpawnUnavailableMessage() => projectilesSelected ?
            L.T("Projectile compatibility checks failed. Off/reset remain available; see Player.log.") : eventSpawnsSelected ?
            L.T("Random event compatibility checks failed. Reset remains available; see Player.log.") :
            L.T("Mystic Jar compatibility checks failed. Reset remains available; see Player.log.");
        private string SpawnCommandFamily => projectilesSelected ? "projectiles" : eventSpawnsSelected ? "events" : "jars";

        private static string EventValues(SettingsSnapshot snapshot) => SessionSettings.DescribeEventSpawns(snapshot.EventSpawns) +
            "\n\n" + L.T("Blood donation, obelisks, fountains and other optional event rooms. Up to 2 rooms per chapter.") +
            "\n\n" + L.T("Merchants, travelers and Mystic Jars have separate spawn rules.");

        private static string JarValues(SettingsSnapshot snapshot) => SessionSettings.DescribeJarSpawns(snapshot.JarSpawns) +
            "\n\n" + L.T("Random locations only; chapter gates, guaranteed Jars and hidden-room rewards stay native.") +
            "\n\n" + L.T("0%: no random Jars. 100%: all eligible locations. Existing Jars stay unchanged.");
    }
}
