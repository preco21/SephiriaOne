using SephiriaOne;
using UnityEngine;

internal static class TeleportAllTests
{
    internal static void Run(Action<bool, string> check, Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add)
    {
        var host = start();
        Mirror.NetworkServer.localConnection = host.connectionToClient;
        host.PlayerAvatar.transform.position = new Vector3(10, 20, 0);
        var guest = add(2, 1, 1, 0);
        var other = add(3, 1, 1, 0);
        check(SettingsActions.Execute("/one teleportall").Success && guest.PlayerAvatar.Teleports.Count == 1 &&
            guest.PlayerAvatar.Teleports[0] == host.PlayerAvatar.transform.position && other.PlayerAvatar.Teleports.Count == 1,
            "Teleport all uses the host position for every ready guest");
        check(host.PlayerAvatar.Teleports.Count == 0, "Host already at destination is not moved");
        check(SettingsActions.Execute("/ONE TELEPORTALL help").Success && !SettingsActions.Execute("/one teleportall extra").Success &&
            guest.PlayerAvatar.Teleports.Count == 1, "Teleport help/invalid arguments do not move anyone");
        Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one teleportall").Success && guest.PlayerAvatar.Teleports.Count == 1,
            "Guest command cannot teleport the party");
        Mirror.NetworkServer.active = true;
        guest.PlayerAvatar.IsDead = true; other.connectionToClient.isReady = false;
        check(SettingsActions.Execute("/one teleportall").Success && guest.PlayerAvatar.Teleports.Count == 1 && other.PlayerAvatar.Teleports.Count == 1,
            "Dead and loading players are skipped without queuing later movement");
        guest.PlayerAvatar.IsDead = false; other.connectionToClient.isReady = true;
        guest.PlayerAvatar.loadingScreenType = 0;
        other.PlayerAvatar.loadingScreenType = 0;
        check(SettingsActions.Execute("/one teleportall").Success && guest.PlayerAvatar.Teleports.Count == 1 && other.PlayerAvatar.Teleports.Count == 1,
            "Traveling players cannot receive coordinates before loading completes");
        guest.PlayerAvatar.loadingScreenType = -1; other.PlayerAvatar.loadingScreenType = -1;
        host.PlayerAvatar.transform.position = new Vector3(float.NaN, 0, 0);
        check(!SettingsActions.Execute("/one teleportall").Success && guest.PlayerAvatar.Teleports.Count == 1, "Invalid destination rejects before sending");
        host.PlayerAvatar.transform.position = new Vector3(3, 4, 0);
        DungeonManager.Instance.IsHostTraveling = true;
        check(!SettingsActions.Execute("/one teleportall").Success, "Host travel blocks party teleport");
        DungeonManager.Instance.IsHostTraveling = false;
        SaveManager.CurrentRun.enableSave = false;
        check(!SettingsActions.Execute("/one teleportall").Success, "Settled run cannot be teleported");
        SaveManager.CurrentRun.enableSave = true;
        bool nestedRejected = false;
        guest.PlayerAvatar.OnTeleport = () => nestedRejected = !SettingsActions.Execute("/one teleportall").Success;
        check(SettingsActions.Execute("/one teleportall").Success && nestedRejected && other.PlayerAvatar.Teleports.Count == 2,
            "Native callback cannot recursively teleport the party");
        guest.PlayerAvatar.OnTeleport = () => SaveManager.CurrentRun = new();
        check(!SettingsActions.Execute("/one teleportall").Success && other.PlayerAvatar.Teleports.Count == 2,
            "Run replacement inside callback stops remaining native requests");
        guest.PlayerAvatar.OnTeleport = () => throw new Exception("injected teleport failure");
        check(!SettingsActions.Execute("/one teleportall").Success && other.PlayerAvatar.Teleports.Count == 3,
            "One request failure is reported without blocking other eligible players");
        guest.PlayerAvatar.OnTeleport = () => other.connectionToClient = new();
        check(SettingsActions.Execute("/one teleportall").Success && other.PlayerAvatar.Teleports.Count == 3,
            "Connection replacement is skipped, never retargeted");
        guest.PlayerAvatar.OnTeleport = null;
        PlayerSpawner.MultiplayerList.Add(guest);
        int before = guest.PlayerAvatar.Teleports.Count;
        check(SettingsActions.Execute("/one teleportall").Success && guest.PlayerAvatar.Teleports.Count == before + 1,
            "Duplicate roster entries send only one request");
        Mirror.NetworkServer.localConnection = null;
        check(!SettingsActions.Execute("/one teleportall").Success, "Missing host connection never falls back to an arbitrary guest");
    }
}
