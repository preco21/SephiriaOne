using HarmonyLib;
using Mirror;
using SephiriaOne;

internal static class LargePartyCombatTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        const string patchId = "SephiriaOne.Tests.LargeParty";
        var harmony = new Harmony(patchId);
        FriendlyFireParticipantHooks.Install(harmony);
        ReviveAllHooks.Install();
        void At(float seconds) { UnityEngine.Time.unscaledTime = seconds; CombatMatchRuntime.Tick(); }
        PlayerAvatar[] New()
        {
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= 9; i++) players.Add(add(i).PlayerAvatar);
            foreach (var player in players) player.currentFloorGuid = "town";
            At(0);
            check(SessionSettings.EnsureResourceScope(), "Nine-player lobby session initializes before participant selection");
            check(HostStateAdapter.TryCollect(out var context, out _) && context.Players.Count == 9,
                "Lobby readiness collects all nine players without a fixed-size cutoff");
            return players.ToArray();
        }
        try
        {
            var all = New();
            check(SettingsActions.Execute("/one friendlyfire on").Success && all.All(p => FriendlyFireParticipants.Includes(p)),
                "Casual lobby friendly fire includes all nine current players");
            var ninth = all[8]; var old = FriendlyFireParticipants.Capture(ninth);
            ninth.spawner.connectionToClient.isReady = false;
            check(SessionSettings.FriendlyFireForHit.Enabled && all.Take(8).All(p => FriendlyFireParticipants.Includes(p)) &&
                !FriendlyFireParticipants.Includes(ninth), "An initializing ninth player does not disable existing lobby combat");
            ninth.spawner.connectionToClient.isReady = true;
            check(FriendlyFireParticipants.Includes(ninth), "Ninth player joins combat when initialization finishes");
            FriendlyFireParticipants.SetAll(all.Select(FriendlyFireParticipants.Capture).ToArray());
            var connection = ninth.spawner.connectionToClient;
            NetworkServer.RemoveConnection(connection.connectionId);
            NetworkServer.AddConnection(connection);
            check(!FriendlyFireParticipants.IsCurrent(old) && !FriendlyFireParticipants.IsSelected(ninth) &&
                FriendlyFireParticipants.SelectedCount == 8 && !FriendlyFireParticipants.Includes(ninth),
                "Reconnecting ninth player cannot reuse an old explicit selection");
            FriendlyFireParticipants.ResetSelection();
            check(all.All(p => FriendlyFireParticipants.Includes(p)), "Empty selection admits all nine fresh current lifetimes");

            foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
            {
                all = New();
                check(SettingsActions.Execute("/one " + mode + " start").Success && !SessionSettings.FriendlyFireForHit.Enabled,
                    mode + ": nine-player lobby countdown starts with combat disarmed");
                At(3);
                int pairs = all.Sum(a => all.Count(b => a != b && CombatMatchRuntime.AllowsPair(a, b)));
                int expected = mode == "tournament" ? 2 : mode == "zombie" ? 16 : 72;
                check(CombatMatchRuntime.IsRunning && SessionSettings.FriendlyFireForHit.Enabled && pairs == expected,
                    mode + ": correct nine-player pair/team targeting after lobby countdown");
                check(SettingsActions.Execute("/one friendlyfire balance off").Success && CombatMatchRuntime.IsRunning &&
                    SessionSettings.FriendlyFireForHit.Enabled && !SessionSettings.FriendlyFireForHit.Balance,
                    mode + ": balance comparison preserves the active lobby match");
                CombatMatchRuntime.Stop(true, false);

                // Explicitly exercise the last roster entry in every mode, including
                // Tournament where random pair selection could otherwise skip it.
                all = New(); ninth = all[8];
                FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(all[0]), true);
                FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(ninth), true);
                check(SettingsActions.Execute("/one " + mode + " start").Success, mode + ": first and ninth selections start");
                At(3);
                check(CombatMatchRuntime.AllowsPair(all[0], ninth) && CombatMatchRuntime.AllowsPair(ninth, all[0]) &&
                    all.Skip(1).Take(7).All(p => !FriendlyFireParticipants.Includes(p)),
                    mode + ": ninth player is a valid opponent while unselected middle entries remain spectators");
                check(SettingsActions.Execute("/one friendlyfire off").Success && !CombatMatchRuntime.IsRunning &&
                    !SessionSettings.FriendlyFireForHit.Enabled, mode + ": shared lobby stop cleans up the mode");
            }

            all = New();
            check(SettingsActions.Execute("/one deathmatch start").Success, "Nine-player lobby recovery match starts");
            At(3);
            all[8].IsDead = true;
            CombatMatchRuntime.Died(all[8]);
            At(6);
            check(!all[8].IsDead && all[8].ReviveRpcCalls == 1,
                "Ninth player receives automatic native revival in a lobby match");
            CombatMatchRuntime.Stop(true, false);
        }
        finally
        {
            CombatMatchRuntime.Stop(true, false);
            ReviveAllHooks.Uninstall();
            harmony.UnpatchAll(patchId);
        }
    }
}
