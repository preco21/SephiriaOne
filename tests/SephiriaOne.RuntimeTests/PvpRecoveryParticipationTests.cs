using SephiriaOne;

internal static class PvpRecoveryParticipationTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        PlayerAvatar[] New()
        {
            UnityEngine.Time.unscaledTime = 0;
            var all = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar, add(4).PlayerAvatar };
            SessionSettings.EnsureResourceScope();
            return all;
        }
        var players = New();
        check(players.All(p => !PvpParticipation.IsActive(p)), "PvE keeps shared recovery enabled");
        check(SettingsActions.Execute("/one friendlyfire on").Success && players.All(PvpParticipation.IsActive),
            "Casual PvP uses all current players when selection is empty");
        check(SettingsActions.Execute("/one friendlyfire balance off").Success && players.All(PvpParticipation.IsActive),
            "Recovery restrictions do not depend on optional balancing");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[0]), true);
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[1]), true);
        check(!PvpParticipation.CanShareRecovery(players[0], players[2]) &&
            !PvpParticipation.CanShareRecovery(players[2], players[0]) &&
            PvpParticipation.CanShareRecovery(players[2], players[3]),
            "Selected PvP participants neither send nor receive sharing; spectators can share with spectators");
        check(SettingsActions.Execute("/one friendlyfire off").Success && players.All(p => !PvpParticipation.IsActive(p)),
            "Turning casual PvP off restores recovery immediately");

        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            players = New();
            check(SettingsActions.Execute("/one " + mode + " start").Success && players.All(p => !PvpParticipation.IsActive(p)),
                mode + ": pre-fight countdown retains native recovery");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            var fighters = players.Where(PvpParticipation.IsActive).ToArray();
            check(fighters.Length == (mode == "tournament" ? 2 : 4), mode + ": effective participation scopes shared recovery");
            check(fighters.All(a => players.All(b => !PvpParticipation.CanShareRecovery(a, b) &&
                !PvpParticipation.CanShareRecovery(b, a))), mode + ": active combat blocks both directions of shared healing");
            if (mode == "zombie")
            {
                var humans = players.Where(p => !CombatMatchRuntime.IsActiveZombie(p)).ToArray();
                check(!CombatMatchRuntime.AllowsPair(humans[0], humans[1]) &&
                    !PvpParticipation.CanShareRecovery(humans[0], humans[1]),
                    "Zombie teammates still participate even though same-team damage is blocked");
            }
            check(SettingsActions.Execute("/one friendlyfire balance off").Success && fighters.All(PvpParticipation.IsActive),
                mode + ": turning balancing off keeps recovery restrictions active");
            check(SettingsActions.Execute("/one " + mode + " stop").Success && players.All(p => !PvpParticipation.IsActive(p)),
                mode + ": ending the mode restores recovery without a sync frame");
        }

        players = New(); SettingsActions.Execute("/one tournament start");
        UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
        var oldFighter = players.First(PvpParticipation.IsActive);
        var departed = players.First(p => p != oldFighter);
        // Selected lifetime retirement invokes match cleanup from admission.
        CombatMatchRuntime.Stop(true, false);
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(oldFighter), true);
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(departed), true);
        SettingsActions.Execute("/one tournament start");
        UnityEngine.Time.unscaledTime = 6; CombatMatchRuntime.Tick();
        Mirror.NetworkServer.connections.Remove(departed.spawner.connectionToClient.connectionId);
        check(!PvpParticipation.IsActive(oldFighter) && !CombatMatchRuntime.IsRunning,
            "Selection retirement during a query cannot leave recovery blocked after match cleanup");
    }
}
