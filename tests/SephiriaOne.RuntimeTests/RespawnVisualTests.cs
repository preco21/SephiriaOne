using HarmonyLib;
using SephiriaOne;

internal static class RespawnVisualTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        ReviveAllHooks.Install();
        var harmony = new Harmony("RespawnVisual.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"));
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        PlayerAvatar Begin(string mode, bool selected = false)
        {
            var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar };
            NativeReviveVisuals.Shown.Clear(); NativeReviveVisuals.Fail = false;
            if (selected) check(SettingsActions.Execute("/one friendlyfire players all").Success, "Select current players for respawn FX scenario");
            At(0); check(SettingsActions.Execute("/one " + mode + " start").Success, "Start respawn FX scenario: " + mode); At(3);
            return mode == "deathmatch" ? players[1] : players.First(a => players.Any(b => CombatMatchRuntime.AllowsPair(a, b)));
        }
        try
        {
            foreach (string mode in new[] { "deathmatch", "tournament" })
            {
                var victim = Begin(mode); victim.Die(0, new()); At(4); At(5);
                check(victim.IsDead && NativeReviveVisuals.Shown.Count == 0, mode + " countdown emits no early FX");
                At(6);
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && NativeReviveVisuals.Shown.SequenceEqual(new[] { victim }) &&
                    victim.Lifecycle.SequenceEqual(new[] { "RpcDie", "RpcRevive", "ReviveFx" }),
                    mode + " emits one effect on the revived avatar after native revival completes");
                At(6.2f); At(7);
                check(NativeReviveVisuals.Shown.Count == 1, mode + " later ticks cannot replay the effect");

                victim = Begin(mode); victim.Die(0, new()); At(4); victim.Revive(100); At(6);
                check(NativeReviveVisuals.Shown.Count == 0, mode + " manual revival invalidates the automatic timer without FX");
                victim = Begin(mode); victim.Die(0, new()); victim.spawner.connectionToClient.isReady = false; At(6);
                check(NativeReviveVisuals.Shown.Count == 0 && victim.IsDead, mode + " loading delays revival and effect together");
                victim.spawner.connectionToClient.isReady = true; At(7);
                check(NativeReviveVisuals.Shown.SequenceEqual(new[] { victim }), mode + " delayed successful revival emits exactly once");

                foreach (string invalid in new[] { "hp", "callback", "disconnect", "replacement", "restart", "scope-callback", "die-callback", "stop", "stop-callback" })
                {
                    victim = Begin(mode); victim.Die(0, new());
                    switch (invalid)
                    {
                        case "hp": victim.MaxHp = float.NaN; break;
                        case "callback": victim.ReviveCallback = () => throw new Exception("native callback failed"); break;
                        case "disconnect": FriendlyFireParticipants.Retire(victim.spawner); break;
                        case "replacement":
                            PlayerSpawner.MultiplayerList.Remove(victim.spawner); add(victim.netId); break;
                        case "restart": HorayModAPI.StartSession(); break;
                        case "scope-callback": victim.ReviveCallback = () => SaveManager.CurrentRun = new(); break;
                        case "die-callback": victim.ReviveCallback = () => victim.IsDead = true; break;
                        case "stop": SettingsActions.Execute("/one " + mode + " stop"); break;
                        case "stop-callback": victim.ReviveCallback = () => SettingsActions.Execute("/one " + mode + " stop"); break;
                    }
                    At(6); At(7);
                    check(NativeReviveVisuals.Shown.Count == 0, mode + " no false success effect for " + invalid);
                }
                victim = Begin(mode); victim.Die(0, new()); NativeReviveVisuals.Fail = true; At(6);
                check(!victim.IsDead && CombatMatchRuntime.IsRunning && victim.ReviveRpcCalls == 1,
                    mode + " optional FX failure cannot stop the match or repeat revival");
            }
            var pruned = Begin("tournament", selected: true);
            var peer = PlayerSpawner.MultiplayerList.First(spawner => spawner.PlayerAvatar != pruned);
            pruned.Die(0, new());
            pruned.ReviveCallback = () => Mirror.NetworkServer.connections.Remove(peer.connectionToClient.connectionId);
            At(6);
            check(!pruned.IsDead && !CombatMatchRuntime.IsRunning && NativeReviveVisuals.Shown.Count == 0,
                "Lazy pruning of another selected connection after revival cancels Tournament FX");

            var expired = Begin("deathmatch");
            At(302); expired.Die(0, new()); At(303);
            check(!expired.IsDead && !CombatMatchRuntime.IsRunning && NativeReviveVisuals.Shown.Count == 0,
                "Match expiry cleanup is not a completed three-second respawn");
            var host = start(); host.PlayerAvatar.IsDead = true;
            NativeReviveVisuals.Shown.Clear(); NativeReviveVisuals.Fail = false;
            check(SettingsActions.Execute("/one reviveall").Success && NativeReviveVisuals.Shown.Count == 0,
                "Manual revive-all retains its existing presentation");
            check(ReviveAllAction.TryRevive(new HostPlayer(host), () => true, out _, out int restored) && restored == 0,
                "Already living player reports successful no-op with no actual restoration");
        }
        finally { harmony.UnpatchAll("RespawnVisual.integration.tests"); ReviveAllHooks.Uninstall(); start(); }
    }
}

namespace SephiriaOne
{
    internal static class NativeReviveVisuals
    {
        internal static readonly List<PlayerAvatar> Shown = new();
        internal static bool Fail;
        internal static void Show(PlayerAvatar player)
        {
            if (Fail) throw new InvalidOperationException("optional native FX unavailable");
            Shown.Add(player); player.Lifecycle.Add("ReviveFx");
        }
    }
}
