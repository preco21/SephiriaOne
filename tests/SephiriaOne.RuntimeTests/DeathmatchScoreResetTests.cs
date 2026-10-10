using Mirror;
using SephiriaOne;

internal static class DeathmatchScoreResetTests
{
    private const string Command = "/one deathmatch resetkda";

    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void At(float time) { UnityEngine.Time.unscaledTime = time; DeathmatchRuntime.Tick(); }
        PlayerSpawner Begin(bool warmup = false)
        {
            var player = start(); At(0);
            check(SettingsActions.Execute("/one deathmatch duration 10").Success &&
                SettingsActions.Execute("/one deathmatch start").Success, "Prepare score-reset match");
            if (!warmup) At(3);
            return player;
        }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        {
            var receipt = FriendlyFireKda.BeforeDeath(victim, killer, FriendlyFireKda.Epoch);
            victim.IsDead = true;
            check(FriendlyFireKda.CompleteDeath(victim, receipt), "Confirm scored fixture death");
            DeathmatchRuntime.Died(victim);
        }
        bool Zero(PlayerAvatar player) => FriendlyFireKda.Label(player, "P") == "P(0/0/0)";
        var host = Begin(); var guest = add(2); var helper = add(3);
        host.PlayerAvatar.playerNameSource = "Host"; guest.PlayerAvatar.playerNameSource = "Guest";
        helper.PlayerAvatar.playerNameSource = "Helper";
        At(4);
        FriendlyFireKda.Damage(helper.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch);
        Kill(host.PlayerAvatar, guest.PlayerAvatar);
        var retained = FriendlyFireKda.For(host.PlayerAvatar);
        var remaining = DeathmatchRuntime.Describe();
        var revision = SessionSettings.ReadSnapshot().Revision;
        long epoch = FriendlyFireKda.Epoch;
        check(SettingsActions.Execute(Command).Success, "Host command resets deathmatch K/D/A");
        check(Zero(host.PlayerAvatar) && Zero(guest.PlayerAvatar) && Zero(helper.PlayerAvatar) &&
            ReferenceEquals(retained, FriendlyFireKda.For(host.PlayerAvatar)) && FriendlyFireKda.Epoch != epoch,
            "Reset clears all totals and old attribution while retaining score identities");
        check(DeathmatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.Enabled &&
            DeathmatchRuntime.Describe() == remaining && SessionSettings.ReadSnapshot().Revision == revision &&
            guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveCalls == 0,
            "Reset preserves match duration, policy revision, friendly fire and pending respawn");
        check(DungeonManager.Instance.ChatMessages.Count(x => x.Message == "Deathmatch K/D/A scores reset.") == 1,
            "Reset announces once through stock guest chat");
        At(6); check(guest.PlayerAvatar.IsDead, "Reset does not shorten the old respawn deadline");
        At(7); check(!guest.PlayerAvatar.IsDead, "Reset does not extend the old respawn deadline");
        FriendlyFireKda.Damage(helper.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch);
        check(SettingsActions.Execute(Command).Success, "Repeated reset is accepted");
        Kill(host.PlayerAvatar, guest.PlayerAvatar);
        check(FriendlyFireKda.For(host.PlayerAvatar).Kills == 1 && FriendlyFireKda.For(guest.PlayerAvatar).Deaths == 1 && Zero(helper.PlayerAvatar),
            "New kills count from zero; damage before reset cannot earn an assist");
        At(10);
        FriendlyFireKda.Damage(helper.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch);
        Kill(host.PlayerAvatar, guest.PlayerAvatar);
        check(FriendlyFireKda.For(helper.PlayerAvatar).Assists == 1, "New damage can earn assists after reset");
        At(13);
        check(!DeathmatchRuntime.IsRunning && !guest.PlayerAvatar.IsDead &&
            DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("Host(2/0/0)")),
            "Original deadline and final rankings survive score reset");

        // Stable accounts, offline spawners and disconnected participants retain
        // their original ranking entries; replacement connections do not duplicate them.
        host = start(); guest = add(2); helper = add(3);
        host.steamID = 11; guest.steamID = 22;
        host.PlayerAvatar.playerNameSource = "Account"; guest.PlayerAvatar.playerNameSource = "Departed";
        helper.PlayerAvatar.playerNameSource = "Offline";
        At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        FriendlyFireKda.For(guest.PlayerAvatar).Kills = 9;
        FriendlyFireKda.For(helper.PlayerAvatar).Assists = 8;
        PlayerSpawner.MultiplayerList.Remove(guest); PlayerSpawner.MultiplayerList.Remove(helper); At(4);
        check(SettingsActions.Execute(Command).Success && Zero(guest.PlayerAvatar) && Zero(helper.PlayerAvatar),
            "Reset clears disconnected account and offline participant scores");
        var replacement = add(2); replacement.steamID = 22; replacement.PlayerAvatar.playerNameSource = "Rejoined"; At(5);
        check(Zero(replacement.PlayerAvatar), "Rejoining account sees reset totals");
        Kill(host.PlayerAvatar, replacement.PlayerAvatar);
        SettingsActions.Execute("/one deathmatch stop");
        var standings = DungeonManager.Instance.ChatMessages.Where(x => x.Message.Length > 2 && x.Message[1] == '.' && x.Message[2] == ' ').ToArray();
        check(standings.Length == 3 && standings.Count(x => x.Message.Contains("Rejoined(0/1/0)")) == 1 &&
            standings.Any(x => x.Message.Contains("Offline(0/0/0)")),
            "Rankings keep departed players and do not duplicate reconnected participants after reset");

        host = Begin(true); guest = add(2);
        guest.PlayerAvatar.IsDead = true; DeathmatchRuntime.Died(guest.PlayerAvatar);
        At(1); check(SettingsActions.Execute(Command).Success && !SessionSettings.FriendlyFireForHit.Enabled,
            "Warmup reset cannot arm friendly fire early");
        At(3); check(SessionSettings.FriendlyFireForHit.Enabled && !guest.PlayerAvatar.IsDead,
            "Warmup reset preserves start and pending respawn deadlines");
        guest.connectionToClient.isReady = false;
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "test");
        check(SettingsActions.Execute(Command).Success, "Score-only reset remains available during unrelated faults and guest initialization");

        host = Begin(); FriendlyFireKda.For(host.PlayerAvatar).Kills = 4;
        foreach (string invalid in new[] { Command + " all", "/one deathmatch reset", "/one deathmatch resetkda 1" })
            check(!SettingsActions.Execute(invalid).Success && FriendlyFireKda.For(host.PlayerAvatar).Kills == 4,
                "Malformed reset leaves scores alone: " + invalid);
        NetworkServer.active = false;
        check(!SettingsActions.Execute(Command).Success && FriendlyFireKda.For(host.PlayerAvatar).Kills == 4, "Guest cannot reset scores");
        NetworkServer.active = true;
        UnityEngine.Time.unscaledTime = 13;
        check(!SettingsActions.Execute(Command).Success && FriendlyFireKda.For(host.PlayerAvatar).Kills == 4, "Expired match cannot reset results before its closing tick");
        At(13);
        check(!SettingsActions.Execute(Command).Success, "Idle deathmatch cannot reset normal friendly-fire scores");
        SettingsActions.Execute("/one friendlyfire on"); FriendlyFireKda.For(host.PlayerAvatar).Kills = 6;
        check(!SettingsActions.Execute(Command).Success && FriendlyFireKda.For(host.PlayerAvatar).Kills == 6, "Normal friendly-fire scores stay isolated");
        host = Begin(); FriendlyFireKda.For(host.PlayerAvatar).Kills = 7;
        SaveManager.CurrentRun = new();
        check(!SettingsActions.Execute(Command).Success && FriendlyFireKda.For(host.PlayerAvatar).Kills == 7, "Stale run cannot reset old match scores");
        var harmony = new HarmonyLib.Harmony("Deathmatch.score-reset.tests");
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyLib.HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        try
        {
            host = Begin(); guest = add(2);
            guest.PlayerAvatar.DieCallback = _ => check(SettingsActions.Execute(Command).Success,
                "Score reset can occur inside a native death callback");
            var damage = new DamageInstance();
            FriendlyFireRuntime.Attribute(host.PlayerAvatar, guest.PlayerAvatar, damage);
            guest.PlayerAvatar.Die(1, damage);
            check(Zero(host.PlayerAvatar) && Zero(guest.PlayerAvatar) && DeathmatchRuntime.IsRunning &&
                !DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains(" killed ")),
                "Death finalizer cannot restore cleared points or emit stale K/D/A notice");
            At(6);
            check(!guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveCalls == 1,
                "Invalidating score receipts does not invalidate the same death's respawn");
            guest.PlayerAvatar.DieCallback = null;
            Kill(host.PlayerAvatar, guest.PlayerAvatar);
            guest.PlayerAvatar.ReviveCallback = () => check(!SettingsActions.Execute(Command).Success,
                "Reset is rejected while match-end recovery unwinds");
            SettingsActions.Execute("/one deathmatch stop");
            check(!DeathmatchRuntime.IsRunning && !guest.PlayerAvatar.IsDead &&
                DungeonManager.Instance.ChatMessages.Any(x => x.Message.Contains("(1/0/0)")),
                "Rejected cleanup reset preserves results and recovery");
        }
        finally { harmony.UnpatchAll("Deathmatch.score-reset.tests"); }
        start();
    }
}
