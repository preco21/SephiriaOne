using HarmonyLib;
using SephiriaOne;

internal static class KilledByRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var harmony = new Harmony("KilledBy.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        {
            // Simulate the completed native revival before the next lethal hit.
            victim.IsDead = false; FriendlyFireKda.ForgetLife(victim);
            // Production BeforeHit resolves policy and participant scope before
            // capturing the KDA epoch; this fixture bypasses only damage admission.
            _ = SessionSettings.FriendlyFireForHit;
            FriendlyFireParticipants.Includes(killer); FriendlyFireParticipants.Includes(victim);
            var damage = new DamageInstance(); FriendlyFireRuntime.Attribute(killer, victim, damage);
            victim.Die(0, damage);
        }
        long Count(PlayerAvatar killer, PlayerAvatar victim) => FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(killer), FriendlyFireKda.For(victim));
        PlayerAvatar host = null, guest = null;
        void Begin(string mode = "friendlyfire")
        {
            host = start().PlayerAvatar; guest = add(2).PlayerAvatar;
            host.spawner.steamID = 11; guest.spawner.steamID = 22;
            host.playerNameSource = "Host"; guest.playerNameSource = "Guest";
            NativePlayerChat.Messages.Clear(); At(0);
            check(SettingsActions.Execute("/one " + mode + (mode == "friendlyfire" ? " on" : " start")).Success,
                "Prepare killed-by scenario: " + mode);
            if (mode != "friendlyfire") At(3);
        }
        try
        {
            Begin("deathmatch"); Kill(host, guest);
            check(NativePlayerChat.Messages.Single() == (guest, "Killed by ‘Host’ (Matchup: Me 0–1 Opponent)") &&
                !DungeonManager.Instance.ChatMessages.Any(x => x.Player == guest),
                "Deathmatch keeps the private killed-by notice without an automatic respawn bubble");
            At(6); Kill(guest, host);
            check(NativePlayerChat.Messages.Last() == (host, "Killed by ‘Guest’ (Matchup: Me 1–1 Opponent)"),
                "Host receives the correct reverse record after the guest respawns");
            check(SettingsActions.Execute("/one deathmatch resetkda").Success && Count(host, guest) == 0 && Count(guest, host) == 0,
                "Deathmatch reset clears both matchup directions without stopping recovery");
            At(9); Kill(host, guest);
            check(NativePlayerChat.Messages.Last().Message.Contains("Me 0–1"), "Deathmatch starts new record after score reset");
            SettingsActions.Execute("/one deathmatch stop");
            check(Count(host, guest) == 0, "Match stop discards matchup history");

            Begin(); Kill(host, guest);
            var retired = guest; var receipt = FriendlyFireParticipants.Capture(guest);
            FriendlyFireParticipants.Retire(guest.spawner);
            PlayerSpawner.MultiplayerList.Remove(guest.spawner);
            guest = add(2).PlayerAvatar; guest.spawner.steamID = 22; guest.playerNameSource = "Renamed";
            Kill(host, guest);
            check(!FriendlyFireParticipants.IsCurrent(receipt) && NativePlayerChat.Messages.Last() ==
                (guest, "Killed by ‘Host’ (Matchup: Me 0–2 Opponent)"),
                "Same-account reconnect preserves history but messages only the new native lifetime");
            retired.IsDead = false; FriendlyFireKda.ForgetLife(retired);
            int notices = NativePlayerChat.Messages.Count; Kill(host, retired);
            check(NativePlayerChat.Messages.Count == notices && Count(host, guest) == 2,
                "Retired avatar cannot receive or add an old connection's death notice");
            Kill(guest, host);
            check(NativePlayerChat.Messages.Last() == (host, "Killed by ‘Renamed’ (Matchup: Me 2–1 Opponent)"),
                "Reconnected opponent uses fresh name and stable account totals");
            FriendlyFireParticipants.Retire(guest.spawner); PlayerSpawner.MultiplayerList.Remove(guest.spawner);
            guest = add(2).PlayerAvatar; guest.spawner.steamID = 33; guest.playerNameSource = "Guest";
            Kill(host, guest);
            check(NativePlayerChat.Messages.Last().Message.Contains("Me 0–1"), "Recycled network ID with another account starts a distinct matchup");

            foreach (string transition in new[] { "resetkda", "players reset", "players all", "off" })
            {
                Begin(); Kill(host, guest);
                SettingsActions.Execute("/one friendlyfire " + transition);
                check(Count(host, guest) == 0 && Count(guest, host) == 0, "Casual reset clears matchup: " + transition);
            }
            foreach (string transition in new[] { "restart", "save", "dungeon", "authority", "unload" })
            {
                Begin(); Kill(host, guest);
                switch (transition)
                {
                    case "restart": HorayModAPI.StartSession(); break;
                    case "save": SaveManager.CurrentRun = new(); break;
                    case "dungeon": DungeonManager.Instance = new(); break;
                    case "authority": Mirror.NetworkServer.active = false; break;
                    case "unload": SessionSettings.Stop(); break;
                }
                FriendlyFireParticipants.Capture(host); // Validate scope, as all combat entry paths do.
                check(Count(host, guest) == 0, "New run/session scope clears matchups: " + transition);
            }
        }
        finally { harmony.UnpatchAll("KilledBy.integration.tests"); start(); }
    }
}
