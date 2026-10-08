using SephiriaOne;
using Mirror;
namespace SephiriaOne { internal static class EventSpawnFeature { internal static bool Available = true; } }
internal static class EventSpawnRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        start();
        check(SettingsActions.Execute("/one events chance x2").Success, "Host changes event multiplier");
        check(SessionSettings.EventSpawnsForGeneration.Multiplier == 2, "Generation sees latest policy immediately");
        HorayModAPI.StartSession();
        check(SessionSettings.EventSpawnsForGeneration.Multiplier == 2, "Second run keeps policy without compounding");
        var guest = add(19); SessionSettings.Synchronize();
        PlayerSpawner.MultiplayerList.Remove(guest); SessionSettings.Synchronize(); add(19);
        check(SessionSettings.EventSpawnsForGeneration.Multiplier == 2, "Re-entry retains shared policy with no connection-specific state");
        check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("events multiplier 2"), "Event snapshot contains policy");
        check(SettingsActions.Execute("/one events status").Success, "Event status command");
        check(SettingsActions.Execute("/one save").Success, "Event preset saves");
        SessionSettings.Stop(); SessionSettings.Start();
        check(SessionSettings.EventSpawnsForGeneration.Multiplier == 2, "Generation lazily resolves saved new scope");
        check(SettingsActions.Execute("/one events reset").Success && !SessionSettings.EventSpawnsForGeneration.HasChanges, "Event reset native");
        EventSpawnFeature.Available = false;
        check(!SettingsActions.Execute("/one events chance x2").Success && SettingsActions.Execute("/one events reset").Success, "Compatibility failure blocks changes and permits reset");
        EventSpawnFeature.Available = true;
        check(SettingsActions.Execute("/one forget").Success, "Forget event saved settings");
        DungeonManager.Instance = new DungeonManager();
        check(!SessionSettings.EventSpawnsForGeneration.HasChanges, "Replacement session immediately uses native defaults");
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one events chance x2").Success, "Guests cannot modify event odds"); NetworkServer.active = true;
    }
}
