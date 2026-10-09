using SephiriaOne;

internal static class ReviveAllTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        ReviveAllHooks.Install();
        var host = start(); var guest = add(90);
        host.PlayerAvatar.IsDead = true; guest.PlayerAvatar.IsDead = true;
        var result = SettingsActions.Execute("/one reviveall");
        check(result.Recognized && result.Success && !host.PlayerAvatar.IsDead && !guest.PlayerAvatar.IsDead,
            "Dead host can revive all session players through shared command action");
        check(host.PlayerAvatar.hp == host.PlayerAvatar.MaxHp && guest.PlayerAvatar.hp == guest.PlayerAvatar.MaxHp,
            "Native revival receives each player's own full MaxHp");
        result = SettingsActions.Execute("/one reviveall");
        check(result.Success && host.PlayerAvatar.ReviveCalls == 1 && guest.PlayerAvatar.ReviveCalls == 1,
            "Repeated recovery is a harmless no-op for living players");
        host = start(); guest = add(90); guest.PlayerAvatar.IsDead = true; guest.PlayerAvatar.MaxHp = 175; host.PlayerAvatar.hp = 12;
        result = SettingsActions.Execute(" /ONE REVIVEALL ");
        check(result.Success && guest.PlayerAvatar.hp == 175 && host.PlayerAvatar.hp == 12 && host.PlayerAvatar.ReviveCalls == 0,
            "Off/default recovery preserves living HP and accepts command casing");
        foreach (string command in new[] { "/one reviveall 5", "/one reviveall all", "/one reviveall help extra" })
            check(!SettingsActions.Execute(command).Success, "Reject invalid revive syntax: " + command);
        check(SettingsActions.Execute("/one reviveall help").Success, "Revive help is discoverable");
        guest.PlayerAvatar.IsDead = true; Mirror.NetworkServer.active = false;
        check(!SettingsActions.Execute("/one reviveall").Success && guest.PlayerAvatar.IsDead, "Guest cannot trigger host revival");
        Mirror.NetworkServer.active = true;
        foreach (string unavailable in new[] { "settled", "victory", "leaving" })
        {
            SaveManager.CurrentRun.enableSave = unavailable != "settled";
            DungeonManager.Instance.victoryType = unavailable == "victory" ? 1 : 0;
            DungeonManager.Instance.requestLeaveOnHost = unavailable == "leaving";
            check(!ReviveAllAction.CanExecute && !SettingsActions.Execute("/one reviveall").Success && guest.PlayerAvatar.IsDead,
                "Recovery refuses terminal run state: " + unavailable);
        }
        host = start(); guest = add(90); var third = add(91);
        host.PlayerAvatar.IsDead = guest.PlayerAvatar.IsDead = third.PlayerAvatar.IsDead = true;
        guest.connectionToClient.isReady = false;
        result = SettingsActions.Execute("/one reviveall");
        check(!result.Success && !host.PlayerAvatar.IsDead && guest.PlayerAvatar.IsDead && !third.PlayerAvatar.IsDead,
            "Loading dead player is reported without blocking ready dead players");
        guest.connectionToClient.isReady = true;
        check(SettingsActions.Execute("/one reviveall").Success && !guest.PlayerAvatar.IsDead, "Retry restores newly ready player");
        foreach (float invalidHp in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            guest.PlayerAvatar.IsDead = true; guest.PlayerAvatar.MaxHp = invalidHp;
            check(!SettingsActions.Execute("/one reviveall").Success && guest.PlayerAvatar.IsDead,
                "Invalid native maximum HP is never sent to clients: " + invalidHp);
        }
        host = start(); guest = add(90);
        host.PlayerAvatar.IsDead = guest.PlayerAvatar.IsDead = true;
        host.PlayerAvatar.ReviveCallback = () => throw new InvalidOperationException("native revive callback");
        int laterCallbacks = 0;
        host.PlayerAvatar.OnRevive += () => laterCallbacks++;
        host.PlayerAvatar.OnHpChangedServerside += _ => throw new InvalidOperationException("HP callback");
        host.PlayerAvatar.OnHpChangedServerside += _ => laterCallbacks++;
        result = SettingsActions.Execute("/one reviveall");
        check(!result.Success && !guest.PlayerAvatar.IsDead && ReviveAllAction.CanExecute, "Native callback failure is reported while other players recover and action lock clears");
        check(laterCallbacks == 2 && host.PlayerAvatar.ReviveProtectionCalls == 1 && host.PlayerAvatar.RemoteInventoryCalls == 1 &&
            host.PlayerAvatar.ReviveRpcCalls == 1, "Throwing subscribers cannot strand native recovery before its guest RPC");
        host = start(); guest = add(90); host.PlayerAvatar.IsDead = guest.PlayerAvatar.IsDead = true;
        host.PlayerAvatar.ReviveCallback = () => PlayerSpawner.MultiplayerList.Remove(guest);
        check(!SettingsActions.Execute("/one reviveall").Success && guest.PlayerAvatar.ReviveCalls == 0, "Disconnect inside revival cannot mutate a departed avatar");
        foreach (string transition in new[] { "dungeon", "run", "generation", "settled" })
        {
            host = start(); guest = add(90); SessionSettings.Synchronize();
            host.PlayerAvatar.IsDead = guest.PlayerAvatar.IsDead = true;
            host.PlayerAvatar.ReviveCallback = () =>
            {
                if (transition == "dungeon") DungeonManager.Instance = new DungeonManager();
                else if (transition == "run") SaveManager.CurrentRun = new SaveData();
                else if (transition == "generation") HorayModAPI.StartSession();
                else SaveManager.CurrentRun.enableSave = false;
            };
            check(!SettingsActions.Execute("/one reviveall").Success && guest.PlayerAvatar.ReviveCalls == 0,
                "Revival stops when callback changes " + transition);
            check(host.PlayerAvatar.ReviveRpcCalls == 0, "Scope change aborts before the native guest RPC: " + transition);
        }
        host = start(); host.PlayerAvatar.IsDead = true; bool nestedSucceeded = true;
        host.PlayerAvatar.ReviveCallback = () => nestedSucceeded = SettingsActions.Execute("/one reviveall").Success;
        check(SettingsActions.Execute("/one reviveall").Success && !nestedSucceeded && host.PlayerAvatar.ReviveCalls == 1,
            "Reentrant revive action is rejected");
        host = start(); guest = add(90); host.PlayerAvatar.IsDead = guest.PlayerAvatar.IsDead = true;
        PlayerSpawner.MultiplayerList.Add(guest);
        check(SettingsActions.Execute("/one reviveall").Success && guest.PlayerAvatar.ReviveCalls == 1, "Duplicate roster entry does not revive twice");
        host = start(); guest = add(90); SettingsActions.Execute("/one friendlyfire on");
        FriendlyFireKda.Damage(host.PlayerAvatar, guest.PlayerAvatar, FriendlyFireKda.Epoch);
        var death = FriendlyFireKda.BeforeDeath(guest.PlayerAvatar, host.PlayerAvatar, FriendlyFireKda.Epoch);
        guest.PlayerAvatar.IsDead = true; FriendlyFireKda.CompleteDeath(guest.PlayerAvatar, death);
        check(SettingsActions.Execute("/one reviveall").Success && FriendlyFireKda.Label(host.PlayerAvatar, "A") == "A(1/0/0)" &&
            FriendlyFireKda.Label(guest.PlayerAvatar, "B") == "B(0/1/0)", "Recovery preserves KDA totals");
        host = start(); guest = add(90); guest.PlayerAvatar.IsDead = true;
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "test fault");
        FriendlyFireFeature.Available = false;
        check(SettingsActions.Execute("/one reviveall").Success && !guest.PlayerAvatar.IsDead, "Recovery does not depend on friendly-fire hooks or an unrelated stat fault");
        FriendlyFireFeature.Available = true;
        host = start(); guest = add(90); guest.PlayerAvatar.IsDead = true;
        guest.PlayerAvatar.ReviveCallback = () => guest.PlayerAvatar.hp = float.NaN;
        check(!SettingsActions.Execute("/one reviveall").Success, "Invalid post-revival health is not reported as successful recovery");
        host = start(); guest = add(90); var oldAvatar = guest.PlayerAvatar; oldAvatar.IsDead = true;
        oldAvatar.ReviveCallback = () => guest.PlayerAvatar = new PlayerAvatar();
        check(!SettingsActions.Execute("/one reviveall").Success && oldAvatar.ReviveRpcCalls == 0, "Replacing the last avatar aborts before native guest RPC");
        host = start(); host.PlayerAvatar.IsDead = true; SaveManager.CurrentRun = null;
        check(!SettingsActions.Execute("/one reviveall").Success && host.PlayerAvatar.IsDead, "No run context is not a recoverable session");
        host = start(); host.PlayerAvatar.IsDead = true; ReviveAllFeature.Available = false;
        check(!SettingsActions.Execute("/one reviveall").Success && host.PlayerAvatar.IsDead, "Missing recovery hooks reject before mutation");
        ReviveAllFeature.Available = true;
        host.PlayerAvatar.ReviveCallback = () => throw new InvalidOperationException("ordinary native revive");
        bool nativeThrew = false;
        try { host.PlayerAvatar.Revive(100); } catch (InvalidOperationException) { nativeThrew = true; }
        check(nativeThrew && host.PlayerAvatar.ReviveRpcCalls == 0, "Unscoped native revival preserves callback exception behavior");
        host = start(); host.PlayerAvatar.IsDead = true;
        var unrelated = new PlayerAvatar { IsDead = true, ReviveCallback = () => throw new InvalidOperationException("nested native revive") };
        host.PlayerAvatar.ReviveCallback = () => unrelated.Revive(100);
        check(!SettingsActions.Execute("/one reviveall").Success && host.PlayerAvatar.ReviveRpcCalls == 1 && unrelated.ReviveRpcCalls == 0,
            "Callback containment applies only to the exact action-owned avatar");
        var nativeCode = HarmonyLib.PatchProcessor.GetOriginalInstructions(HarmonyLib.AccessTools.Method(typeof(UnitAvatar), "Revive")).ToList();
        var changed = nativeCode.Select(i => new HarmonyLib.CodeInstruction(i)).ToList();
        changed.RemoveAt(changed.FindIndex(i => i.operand is System.Reflection.MethodInfo m && m.Name == "Invoke"));
        bool rejected = false;
        try { ReviveAllHooks.Rewrite(changed).ToList(); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "Missing native event anchor rejects recovery patch");
        ReviveAllHooks.Uninstall();
        check(HarmonyLib.PatchProcessor.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(UnitAvatar), "Revive"))?.Transpilers.Count is null or 0,
            "Unloading recovery restores native callback dispatch");
        start();
    }
}
