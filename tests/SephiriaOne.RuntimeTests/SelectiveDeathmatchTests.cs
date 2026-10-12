using SephiriaOne;

internal static class SelectiveDeathmatchTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        var loadingHost = start(); var loadingGuest = add(2);
        At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        loadingGuest.PlayerAvatar.Die(1, new()); loadingGuest.connectionToClient.isReady = false;
        At(4); At(7);
        check(loadingGuest.PlayerAvatar.IsDead, "Loading connected participant waits for readiness without native revival");
        loadingGuest.connectionToClient.isReady = true; At(8);
        check(!loadingGuest.PlayerAvatar.IsDead && loadingGuest.PlayerAvatar.ReviveCalls == 1,
            "Temporary readiness loss preserves original respawn deadline instead of restarting countdown");
        var host = start(); var guest = add(2); var bystander = add(3);
        bystander.PlayerAvatar.playerNameSource = "Bystander";
        SettingsActions.Execute("/one friendlyfire player 1 on"); SettingsActions.Execute("/one friendlyfire player 2 on");
        At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        int shownBeforeDeaths = FriendlyFireDeathVisuals.Shown;
        guest.PlayerAvatar.Die(1, new()); bystander.PlayerAvatar.Die(1, new());
        check(FriendlyFireDeathVisuals.Shown == shownBeforeDeaths + 1,
            "Deathmatch shows one FX for the participant and none for the excluded spectator");
        check(CombatMatchRuntime.ProtectDeath(guest.PlayerAvatar) && !CombatMatchRuntime.ProtectDeath(bystander.PlayerAvatar),
            "Deathmatch protects only selected participants' deaths");
        At(6);
        check(!guest.PlayerAvatar.IsDead && bystander.PlayerAvatar.IsDead && bystander.PlayerAvatar.ReviveCalls == 0,
            "Selected player respawns; excluded player's normal death is untouched");
        guest.PlayerAvatar.Die(1, new());
        check(SettingsActions.Execute("/one friendlyfire players reset").Success && !CombatMatchRuntime.IsRunning &&
            !guest.PlayerAvatar.IsDead && bystander.PlayerAvatar.IsDead && FriendlyFireParticipants.SelectedCount == 0,
            "Reset selection stops match, restores pending participant and does not revive excluded bystander");
        check(!DungeonManager.Instance.ChatMessages.Any(m => m.Message.Contains("Bystander(")), "Excluded player never appears in deathmatch standings");

        foreach (string command in new[] { "/one friendlyfire player 2 off", "/one friendlyfire players reset", "/one friendlyfire players all" })
        {
            host = start(); guest = add(2); bystander = add(3);
            SettingsActions.Execute("/one friendlyfire player 1 on"); SettingsActions.Execute("/one friendlyfire player 2 on");
            At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
            guest.PlayerAvatar.DieCallback = _ =>
            {
                check(SettingsActions.Execute(command).Success, "Selection action succeeds inside dying callback: " + command);
                check(guest.PlayerAvatar.ReviveCalls == 0 && CombatMatchRuntime.ProtectDeath(guest.PlayerAvatar),
                    "Admitted death remains protected until native RpcDie finishes after selection edit");
            };
            guest.PlayerAvatar.Die(1, new());
            check(!CombatMatchRuntime.IsRunning && !guest.PlayerAvatar.IsDead &&
                guest.PlayerAvatar.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive" }),
                "Selection edit recovers exactly once in death-before-revival order: " + command);
        }

        host = start(); guest = add(2); At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        guest.PlayerAvatar.Die(1, new());
        PlayerSpawner.MultiplayerList.Remove(guest); var replacement = add(2);
        replacement.PlayerAvatar.IsDead = true;
        At(5); At(6);
        check(replacement.PlayerAvatar.IsDead && replacement.PlayerAvatar.ReviveCalls == 0,
            "Rejoined player in everyone mode receives a fresh timer instead of old connection deadline");
        At(8);
        check(!replacement.PlayerAvatar.IsDead && replacement.PlayerAvatar.ReviveCalls == 1, "Fresh connection respawns on its own deadline");
        CombatMatchRuntime.Stop(true, false);

        host = start(); guest = add(2);
        SettingsActions.Execute("/one friendlyfire players all");
        At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        guest.PlayerAvatar.Die(1, new());
        host.PlayerAvatar.Die(1, new());
        guest.PlayerAvatar.ReviveCallback = () => check(SettingsActions.Execute("/one friendlyfire player 2 off").Success,
            "A selection edit may stop deathmatch inside an admitted native revival");
        At(6);
        check(!CombatMatchRuntime.IsRunning && !guest.PlayerAvatar.IsDead && guest.PlayerAvatar.ReviveRpcCalls == 1 &&
            !host.PlayerAvatar.IsDead && host.PlayerAvatar.ReviveRpcCalls == 1 &&
            !FriendlyFireParticipants.IsSelected(guest.PlayerAvatar),
            "Deselecting reviving participant still completes native revival and guest RPC exactly once");

        host = start(); guest = add(2); bystander = add(3);
        SettingsActions.Execute("/one friendlyfire players all");
        At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        PlayerSpawner.MultiplayerList.Remove(guest); replacement = add(2); replacement.PlayerAvatar.IsDead = true;
        CombatMatchRuntime.Died(replacement.PlayerAvatar); At(8);
        check(replacement.PlayerAvatar.IsDead && !CombatMatchRuntime.ProtectDeath(replacement.PlayerAvatar),
            "Rejoining explicit participant is unselected and gets no stale deathmatch timer/protection");
        CombatMatchRuntime.Stop(true, false);
        check(replacement.PlayerAvatar.IsDead, "Stop does not recover unselected returning player");

        host = start(); guest = add(2);
        SettingsActions.Execute("/one friendlyfire players all");
        At(0); SettingsActions.Execute("/one deathmatch start"); At(3);
        guest.PlayerAvatar.Die(1, new());
        guest.PlayerAvatar.ReviveCallback = () => { PlayerSpawner.MultiplayerList.Remove(host); add(1); };
        check(!SettingsActions.Execute("/one friendlyfire players all").Success && !CombatMatchRuntime.IsRunning,
            "Select All rejects atomic selection when match recovery replaces a captured connection");
        start();
    }
}
