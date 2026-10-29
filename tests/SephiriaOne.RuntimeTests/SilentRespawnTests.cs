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
            !DungeonManager.Instance.ChatMessages.Any(m => RespawnText(m.Message)), scenario + " has no respawn chat/bubble");
        string[] Countdown(PlayerAvatar player) => NativePlayerFloatingText.Messages
            .Where(m => m.Player == player && m.Message.StartsWith("Respawn in:"))
            .Select(m => m.Message).ToArray();
        PlayerAvatar[] Begin(string mode)
        {
            var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar };
            NativeReviveVisuals.Shown.Clear(); NativeReviveVisuals.Fail = false;
            NativePlayerFloatingText.Messages.Clear(); NativePlayerFloatingText.Fail = false;
            NativePlayerFloatingText.Styles.Clear();
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
                check(Countdown(victim).SequenceEqual(new[] { "Respawn in: " + delay + "s" }),
                    scenario + " immediately displays its floating respawn countdown");
                check(NativePlayerFloatingText.Messages.Select((m, i) => (m, i)).Where(p => RespawnText(p.m.Message))
                    .All(p => NativePlayerFloatingText.Styles[p.i]), scenario + " uses emphasized floating text");
                Silent(scenario);
                At(3.2f); At(3 + delay - .1f);
                check(victim.IsDead && NativeReviveVisuals.Shown.Count == 0, scenario + " retains full native respawn delay");
                Silent(scenario);
                At(3 + delay + .1f);
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && NativeReviveVisuals.Shown.SequenceEqual(new[] { victim }) &&
                    victim.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive", "ReviveFx" }), scenario + " keeps one success-only pillar after revival");
                At(4 + delay); Silent(scenario);
                check(Countdown(victim).SequenceEqual(delay == 1 ? new[] { "Respawn in: 1s" } :
                    new[] { "Respawn in: 3s", "Respawn in: 1s" }), scenario + " emits only current seconds and stops at revival");
                check(NativeReviveVisuals.Shown.Count == 1, scenario + " never repeats FX on later ticks");

                foreach (string transition in new[] { "stop", "disconnect", "replacement", "restart", "manual-revive", "loading" })
                {
                    victim = Begin(mode)[playerIndex]; victim.Die(0, new());
                    int shown = Countdown(victim).Length;
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
                    check(Countdown(victim).Length == shown, scenario + " emits no stale countdown after " + transition);
                    check(NativeReviveVisuals.Shown.Count == 0, scenario + " no stale automatic FX after " + transition);
                }
                victim = Begin(mode)[playerIndex]; victim.Die(0, new()); NativeReviveVisuals.Fail = true;
                At(3 + delay + .1f); Silent(scenario + " FX failure");
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && CombatMatchRuntime.IsRunning,
                    scenario + " cosmetic failure cannot change revival or re-enable respawn text");
                victim = Begin(mode)[playerIndex]; NativePlayerFloatingText.Fail = true; victim.Die(0, new());
                At(3.2f); At(3 + delay + .1f);
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && NativeReviveVisuals.Shown.SequenceEqual(new[] { victim }),
                    scenario + " floating transport failure cannot prevent revival or pillar FX");
                Silent(scenario + " floating failure");
            }
            var both = Begin("deathmatch"); both[0].Die(0, new()); both[1].Die(0, new()); At(4); At(5); At(6);
            check(!both[0].IsDead && !both[1].IsDead && NativeReviveVisuals.Shown.Count == 2,
                "Simultaneous host/guest deaths retain independent successful respawn FX");
            foreach (var player in both.Take(2))
                check(Countdown(player).SequenceEqual(new[] { "Respawn in: 3s", "Respawn in: 2s", "Respawn in: 1s" }),
                    "Simultaneous deaths independently display each second exactly once");
            Silent("Simultaneous host/guest recovery");
            both[0].Die(0, new()); At(6.2f);
            check(Countdown(both[0]).SequenceEqual(new[] { "Respawn in: 3s", "Respawn in: 2s", "Respawn in: 1s", "Respawn in: 3s" }),
                "A new death owns a fresh countdown even when the same player dies again");

            var delayed = Begin("deathmatch")[1]; delayed.spawner.connectionToClient.isReady = false;
            delayed.Die(0, new()); At(3.2f);
            check(Countdown(delayed).Length == 0, "Loading guests do not receive premature floating text");
            delayed.spawner.connectionToClient.isReady = true; At(4.2f);
            check(Countdown(delayed).SequenceEqual(new[] { "Respawn in: 2s" }),
                "Ready guest receives the current second without restarting or replaying the countdown");
            At(6.1f);
            check(!delayed.IsDead && Countdown(delayed).Length == 1 && NativeReviveVisuals.Shown.SequenceEqual(new[] { delayed }),
                "Delayed readiness preserves the original deadline and emits no zero/completion text");
        }
        finally
        {
            NativeReviveVisuals.Fail = false;
            NativePlayerFloatingText.Fail = false;
            harmony.UnpatchAll("SilentRespawn.integration.tests"); ReviveAllHooks.Uninstall(); start();
        }
    }
}
