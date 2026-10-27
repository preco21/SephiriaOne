using HarmonyLib;
using SephiriaOne;

internal static class SilentRespawnTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        ReviveAllHooks.Install();
        var harmony = new Harmony("SilentRespawn.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        bool RespawnText(string text) => text.StartsWith("Respawn") || text.Contains("Reviving as");
        void Silent(string scenario) => check(
            !DungeonManager.Instance.ChatMessages.Any(m => RespawnText(m.Message)) &&
            !NativePlayerFloatingText.Messages.Any(m => RespawnText(m.Message)), scenario + " has no respawn chat/bubble or floating text");
        PlayerAvatar[] Begin(string mode)
        {
            var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar };
            NativeReviveVisuals.Shown.Clear(); NativeReviveVisuals.Fail = false;
            NativePlayerFloatingText.Messages.Clear(); NativePlayerFloatingText.Fail = false;
            At(0); check(SettingsActions.Execute("/one " + mode + " start").Success, "Start silent respawn: " + mode);
            At(3); DungeonManager.Instance.ChatMessages.Clear();
            return players;
        }
        try
        {
            foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
            foreach (int playerIndex in new[] { 0, 1 })
            {
                int delay = mode is "tag" or "zombie" ? 1 : 3;
                string scenario = mode + (playerIndex == 0 ? " host" : " guest");
                var victim = Begin(mode)[playerIndex]; victim.Die(0, new());
                Silent(scenario);
                At(3.2f); At(3 + delay - .1f);
                check(victim.IsDead && NativeReviveVisuals.Shown.Count == 0, scenario + " retains full native respawn delay");
                Silent(scenario);
                At(3 + delay + .1f);
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && NativeReviveVisuals.Shown.SequenceEqual(new[] { victim }) &&
                    victim.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive", "ReviveFx" }), scenario + " keeps one success-only pillar after revival");
                At(4 + delay); Silent(scenario);
                check(NativeReviveVisuals.Shown.Count == 1, scenario + " never repeats FX on later ticks");

                foreach (string transition in new[] { "stop", "disconnect", "replacement", "restart", "manual-revive", "loading" })
                {
                    victim = Begin(mode)[playerIndex]; victim.Die(0, new());
                    switch (transition)
                    {
                        case "stop": CombatMatchRuntime.Stop(true, false); break;
                        case "disconnect": FriendlyFireParticipants.Retire(victim.spawner); break;
                        case "replacement": PlayerSpawner.MultiplayerList.Remove(victim.spawner); add(victim.netId); break;
                        case "restart": HorayModAPI.StartSession(); break;
                        case "manual-revive": victim.Revive(100); break;
                        case "loading": victim.spawner.connectionToClient.isReady = false; break;
                    }
                    At(3 + delay + .1f); Silent(scenario + " " + transition);
                    check(NativeReviveVisuals.Shown.Count == 0, scenario + " no stale automatic FX after " + transition);
                }
                victim = Begin(mode)[playerIndex]; victim.Die(0, new()); NativeReviveVisuals.Fail = true;
                At(3 + delay + .1f); Silent(scenario + " FX failure");
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && CombatMatchRuntime.IsRunning,
                    scenario + " cosmetic failure cannot change revival or re-enable respawn text");
            }
            var both = Begin("deathmatch"); both[0].Die(0, new()); both[1].Die(0, new()); At(4); At(5); At(6);
            check(!both[0].IsDead && !both[1].IsDead && NativeReviveVisuals.Shown.Count == 2,
                "Simultaneous host/guest deaths retain independent successful respawn FX");
            Silent("Simultaneous host/guest recovery");
        }
        finally
        {
            NativeReviveVisuals.Fail = false;
            harmony.UnpatchAll("SilentRespawn.integration.tests"); ReviveAllHooks.Uninstall(); start();
        }
    }
}
