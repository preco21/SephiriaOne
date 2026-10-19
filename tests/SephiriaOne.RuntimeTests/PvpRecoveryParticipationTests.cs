using SephiriaOne;

internal static class PvpRecoveryParticipationTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        PlayerAvatar[] New()
        {
            UnityEngine.Time.unscaledTime = 0;
            var all = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar, add(4).PlayerAvatar };
            foreach (var player in all) player.Inventory.Bread = new Charm_HitInvincible
            { NetworkAvatar = player, IsEffectEnabled = true };
            SessionSettings.EnsureResourceScope();
            return all;
        }
        bool BreadProtects(PlayerAvatar player) => PvpWhiteBread.IsHitInvincible(true, player, 0.4f, EInvincibleType.Normal, null);
        var players = New();
        check(players.All(BreadProtects), "PvE retains native White Bread protection");
        check(players.All(p => !PvpParticipation.IsActive(p)), "PvE keeps shared recovery enabled");
        check(SettingsActions.Execute("/one friendlyfire on").Success && players.All(PvpParticipation.IsActive),
            "Casual PvP uses all current players when selection is empty");
        check(SettingsActions.Execute("/one friendlyfire balance off").Success && players.All(PvpParticipation.IsActive),
            "Recovery restrictions do not depend on optional balancing");
        check(players.All(p => !BreadProtects(p)), "Casual PvP suppresses White Bread even with balancing off");
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[0]), true);
        FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[1]), true);
        check(!PvpParticipation.CanShareRecovery(players[0], players[2]) &&
            !PvpParticipation.CanShareRecovery(players[2], players[0]) &&
            PvpParticipation.CanShareRecovery(players[2], players[3]),
            "Selected PvP participants neither send nor receive sharing; spectators can share with spectators");
        check(!BreadProtects(players[0]) && !BreadProtects(players[1]) && BreadProtects(players[2]) && BreadProtects(players[3]),
            "White Bread follows the same live participant selection");
        check(SettingsActions.Execute("/one friendlyfire off").Success && players.All(p => !PvpParticipation.IsActive(p)),
            "Turning casual PvP off restores recovery immediately");
        check(players.All(BreadProtects), "Casual PvP off restores Bread without an equipment refresh");

        foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
        {
            players = New();
            check(SettingsActions.Execute("/one " + mode + " start").Success && players.All(p => !PvpParticipation.IsActive(p)),
                mode + ": pre-fight countdown retains native recovery");
            check(players.All(BreadProtects), mode + ": countdown keeps native Bread behavior");
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            var fighters = players.Where(PvpParticipation.IsActive).ToArray();
            check(players.All(p => BreadProtects(p) == !fighters.Contains(p)), mode + ": only current fighters lose Bread extension");
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
            check(fighters.All(p => !BreadProtects(p)), mode + ": Bread suppression is independent of balancing");
            check(SettingsActions.Execute("/one rabbit block-pvp-share off").Success && CombatMatchRuntime.IsRunning &&
                !SessionSettings.RabbitPotionsForUse.BlockPvpShare && fighters.All(PvpParticipation.IsActive) && fighters.All(p => !BreadProtects(p)),
                mode + ": Rabbit opt-out is live without stopping combat or changing White Bread/participant rules");
            check(SessionSettings.DescribeFriendlyFire(SessionSettings.FriendlyFireForHit).Contains("Block Rabbit shared healing in PvP: off.") &&
                SettingsActions.Execute("/one rabbit block-pvp-share on").Success && SessionSettings.RabbitPotionsForUse.BlockPvpShare,
                mode + ": combat status reflects live restriction and on restores it");
            check(SettingsActions.Execute("/one " + mode + " stop").Success && players.All(p => !PvpParticipation.IsActive(p)),
                mode + ": ending the mode restores recovery without a sync frame");
            check(players.All(BreadProtects), mode + ": mode cleanup restores Bread immediately");
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
        check(players.All(BreadProtects), "Disconnect cleanup cannot retain White Bread suppression");
    }
}
