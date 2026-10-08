using SephiriaOne;
using Mirror;

internal static class BatRuntimeTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        int count = 0;
        void Check(bool result, string why) { check(result, why); count++; }
        void Run(string command) => Check(SettingsActions.Execute(command).Success, command);
        int Raw(PlayerAvatar p) => p.customStats.GetValueOrDefault("HPSTEAL");
        BatCostumeHooks.Install();
        try
        {
            var host = start().PlayerAvatar;
            host.customStats["HPSTEAL"] = 9;
            host.UpdateCostumeData("Bat", true);
            Check(Raw(host) == 14 && host.BatStatus.Value == 5, "Off by default, native costume 5 plus other sources");
            Run("/one bat hp-steal on");
            Check(Raw(host) == 10 && host.BatStatus.Value == 1, "Only costume contribution changes");
            Run("/one bat hp-steal on");
            Check(Raw(host) == 10, "Repeated on does not compound");
            host.AddCustomStat(ECustomStat.HPSteal, 7);
            host.customStatsAmp["HPSTEAL"] = 100;
            SessionSettings.Synchronize();
            Check(Raw(host) == 17 && host.GetCustomStatUnsafe("HPSTEAL") == 34, "Equipment and native amplification preserved");
            Run("/one bat hp-steal off");
            Check(Raw(host) == 21 && host.BatStatus.Value == 5, "Off restores costume only");
            Run("/one bat hp-steal on");
            host.UpdateCostumeData("HolyRabbit", true);
            Check(Raw(host) == 16 && BatCostumeRuntime.TrackedCount == 0, "Switch removes exact applied amount and ownership");
            host.UpdateCostumeData("Bat", true);
            Check(Raw(host) == 17 && host.BatStatus.Value == 1, "Switch back uses live policy before sync frame");
            var potion = StatusDatabase.CreateStatusEntity("HP_STEAL/5");
            potion.SetTarget(host); potion.ApplyStatus(true);
            Check(Raw(host) == 22 && potion.Value == 5, "Unrelated HP steal status unaffected on Bat");
            potion.RemoveStatus(); potion.ClearTarget();
            for (int i = 0; i < 10; i++)
            { host.UpdateCostumeData("Bat", false); Check(Raw(host) == 17 && BatCostumeRuntime.TrackedCount == 1, "Repeated native restart does not stack or leak"); }
            var guest = add(78);
            guest.PlayerAvatar.UpdateCostumeData("Bat", false);
            Check(Raw(guest.PlayerAvatar) == 1, "Fresh guest inherits before readiness frame");
            PlayerSpawner.MultiplayerList.Remove(guest); guest.PlayerAvatar.DestroyFixture(); SessionSettings.Synchronize();
            Check(BatCostumeRuntime.TrackedCount == 1, "Departed avatar releases tracking");
            var rejoin = add(78).PlayerAvatar;
            rejoin.UpdateCostumeData("Bat", false);
            Check(Raw(rejoin) == 1 && BatCostumeRuntime.TrackedCount == 2, "Same ID reconnect owns new native instance");
            HorayModAPI.StartSession();
            rejoin.UpdateCostumeData("Bat", false);
            Check(Raw(rejoin) == 1, "Second run receives current policy");
            Run("/one save");
            Check(SessionSettings.ReadSnapshot().ActiveSettings.Contains("bat hp-steal 1"), "Snapshot exposes Bat intent");
            SessionSettings.Stop();
            Check(Raw(host) == 21 && Raw(rejoin) == 5 && BatCostumeRuntime.TrackedCount == 0, "Controller stop restores owned live bonuses");
            SessionSettings.Start(); SessionSettings.Synchronize();
            Check(Raw(host) == 17 && Raw(rejoin) == 1, "Saved preset adopts existing native costumes on restart");
            Run("/one bat reset");
            Check(Raw(host) == 21 && Raw(rejoin) == 5, "Reset restores all current players");
            BatCostumeFeature.Available = false;
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success, "Compatibility failure blocks enabling");
            Run("/one bat reset"); BatCostumeFeature.Available = true;
            NetworkServer.active = false;
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success, "Guest cannot change host settings");
            NetworkServer.active = true;
            Run("/one forget");
            Run("/one bat hp-steal on");
            DungeonManager.Instance = new DungeonManager();
            SessionSettings.Synchronize();
            Check(!SessionSettings.ReadSnapshot().BatHpSteal && Raw(host) == 21, "New unsaved session restores native contribution");

            // Interrupted writes require recovery before another delta is planned.
            host.customStats.AfterWrite = (key, _) =>
            { if (key == "HPSTEAL") { host.customStats.AfterWrite = null; throw new Exception("fixture after native write"); } };
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success, "Partial native write is reported");
            Check(SessionSettings.ReadSnapshot().FaultedFeature == "bat", "Shared journal contains Bat failure");
            Run("/one bat reset");
            Check(Raw(host) == 21 && host.BatStatus.Value == 5, "Recovery finishes interrupted pair then restores native once");
            host.customStats.AfterWrite = (key, _) =>
            { if (key == "HPSTEAL") { host.customStats.AfterWrite = null; throw new Exception("after write then costume change"); } };
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success && host.BatStatus.Value == 1, "Failed setter leaves matching native removal amount");
            host.UpdateCostumeData("HolyRabbit", true);
            Check(Raw(host) == 16, "Costume switch during fault preserves full unrelated baseline");
            DungeonManager.Instance = new DungeonManager(); SessionSettings.Synchronize();
            Check(SessionSettings.ReadSnapshot().FaultedFeature == "" && !SessionSettings.ReadSnapshot().BatHpSteal, "New session drops obsolete journal without blocking scope");
            host.UpdateCostumeData("Bat", true);
            host.customStats.AfterWrite = (key, _) =>
            { if (key == "HPSTEAL") { host.customStats.AfterWrite = null; throw new Exception("after write then new session"); } };
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success, "Fault before session replacement");
            DungeonManager.Instance = new DungeonManager(); SessionSettings.Synchronize();
            Check(Raw(host) == 21 && host.BatStatus.Value == 5, "Replacement scope restores live costume despite stale journal");
            host.customStats.BeforeWrite = (key, _) =>
            { if (key == "HPSTEAL") { host.customStats.BeforeWrite = null; throw new Exception("before native write"); } };
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success && Raw(host) == 21 && host.BatStatus.Value == 5, "Pre-write rejection restores old removal amount");
            Run("/one bat reset");
            Run("/one bat hp-steal on");
            host.customStats["HPSTEAL"] = int.MaxValue;
            Check(!SettingsActions.Execute("/one bat reset").Success && host.BatStatus.Value == 1, "Overflow rejected without changing owned value");
            host.customStats["HPSTEAL"] = 17;
            Run("/one bat reset");
            Run("/one bat hp-steal on");
            host.customStats.AfterWrite = (key, _) =>
            {
                if (key != "HPSTEAL") return;
                host.customStats.AfterWrite = null;
                host.UpdateCostumeData("HolyRabbit", true);
                throw new Exception("Native callback unequipped costume during write");
            };
            Check(!SettingsActions.Execute("/one bat hp-steal off").Success && Raw(host) == 16, "Reentrant native unequip sees matching value before callback");
            DungeonManager.Instance = new DungeonManager(); SessionSettings.Synchronize();
            Check(SessionSettings.ReadSnapshot().FaultedFeature == "", "Callback failure cannot poison replacement session");
            host.UpdateCostumeData("Bat", true);
            Run("/one bat hp-steal on");
            SessionSettings.RecordFault("inheritance", new StateWriteBatch(() => false), "unrelated expired fixture journal");
            DungeonManager.Instance = new DungeonManager(); SessionSettings.Synchronize();
            Check(Raw(host) == 21 && SessionSettings.ReadSnapshot().FaultedFeature == "", "Unrelated expired inheritance journal does not block Bat restoration");
            host.customStats.AfterWrite = (key, _) =>
            { if (key == "HPSTEAL") { host.customStats.AfterWrite = null; throw new Exception("after write then stop server"); } };
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success, "Fault before authority loss");
            NetworkServer.active = false; SessionSettings.Synchronize();
            Check(BatCostumeRuntime.TrackedCount == 0, "Authority loss discards obsolete ownership without network writes");
            NetworkServer.active = true;
            host.DestroyFixture(); rejoin.DestroyFixture();
            host = start().PlayerAvatar; host.customStats["HPSTEAL"] = 16; host.UpdateCostumeData("Bat", true);
            Check(Raw(host) == 21 && !SessionSettings.ReadSnapshot().BatHpSteal, "Fresh hosted session initializes after faulted shutdown");
            host.customStats.AfterWrite = (key, _) =>
            { if (key == "HPSTEAL") { host.customStats.AfterWrite = null; throw new Exception("after write before addon unload"); } };
            Check(!SettingsActions.Execute("/one bat hp-steal on").Success, "Bat journal pending at addon shutdown");
            SessionSettings.RestoreBat();
            ChoicePoints.RemoveContributions();
            Check(Raw(host) == 21 && SessionSettings.ReadSnapshot().FaultedFeature == "", "Bat restoration retires its journal before Choice shutdown");
            Check(SessionSettings.PrepareCommand("bat", false, out var context, out _), "Prepare mixed inheritance fixture");
            var mixed = context.CreateBatch();
            BatCostumeRuntime.Append(mixed, host, true);
            mixed.Add("native luck fixture", () => host.customStats.GetValueOrDefault("LUCK"), n => host.customStats["LUCK"] = n, 90);
            host.customStats.AfterWrite = (key, _) =>
            { if (key == "LUCK") { host.customStats.AfterWrite = null; throw new Exception("mixed inheritance callback"); } };
            Check(!mixed.TryCommit(out string mixedError), "Mixed inheritance interrupted after Bat and stat writes");
            SessionSettings.RecordFault("inheritance", mixed, mixedError);
            bool blocked = false;
            try { SessionSettings.BeforeBatShutdown(); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked && Raw(host) == 17 && BatCostumeRuntime.TrackedCount != 0, "Mixed journal blocks unload before restoring/unpatching Bat");
            Check(SessionSettings.Recover(mixed, out _), "Mixed journal recovers with Bat ownership still intact");
            SessionSettings.BeforeBatShutdown(); SessionSettings.RestoreBat();
            Check(Raw(host) == 21 && host.BatStatus.Value == 5, "Recovered mixed inheritance can unload without orphaned reduction");
            Run("/one bat hp-steal on");
            SessionSettings.RestoreBat(); BatCostumeHooks.Uninstall();
            host.UpdateCostumeData("Bat", true);
            Check(Raw(host) == 21 && host.BatStatus.Value == 5, "Unload restores and future native application remains 5");
        }
        finally { SessionSettings.RestoreBat(); BatCostumeHooks.Uninstall(); BatCostumeFeature.Available = true; }
        Console.WriteLine($"Passed {count} Bat command/native-status lifecycle checks.");
    }
}
