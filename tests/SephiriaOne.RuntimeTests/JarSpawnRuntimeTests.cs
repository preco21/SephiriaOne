using Mirror;
using SephiriaOne;

internal static class JarSpawnRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one jars chance 50").Success, "Host can change Mystic Jar chance");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("jars chance 50"), "Jar percentage appears in snapshot");
        check(SettingsActions.Execute("/one jars chance x2").Success, "Host can multiply native Jar chance");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("jars multiplier 2"), "Jar multiplier is relative intent");
        check(SessionSettings.JarSpawnsForGeneration.Probability(.18f) == .36f, "Generation sees intent before a synchronization frame");
        HorayModAPI.StartSession();
        check(SessionSettings.JarSpawnsForGeneration.Probability(.18f) == .36f, "Second run keeps Jar policy without accumulating it");
        var guest = add(19); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Remove(guest); SessionSettings.Synchronize(); add(19);
        check(SessionSettings.JarSpawnsForGeneration.Probability(.4f) == .8f, "Rejoining connection uses current shared intent and correct baseline");
        check(SettingsActions.Execute("/one save").Success, "Jar policy can be saved");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("jars multiplier 2"), "Jar policy restores after controller restart");
        check(SettingsActions.Execute("/one jars reset").Success, "Jar reset succeeds");
        check(!SessionSettings.ReadSnapshot().ActiveSettings.Any(x => x.StartsWith("jars ")), "Jar reset removes override");
        JarSpawnFeature.Available = false;
        check(!SettingsActions.Execute("/one jars chance 100").Success && SettingsActions.Execute("/one jars reset").Success,
            "Unavailable hooks block overrides but allow reset");
        JarSpawnFeature.Available = true;
        check(SettingsActions.Execute("/one forget").Success, "Forget saved Jar override");
        DungeonManager.Instance = new DungeonManager();
        check(!SessionSettings.JarSpawnsForGeneration.HasChanges, "Replacement session resolves native defaults immediately");
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one jars chance 100").Success, "Guests cannot change Jar chance");
    }
}
