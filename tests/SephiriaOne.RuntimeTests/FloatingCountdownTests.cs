using HarmonyLib;
using SephiriaOne;

internal static class FloatingCountdownTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        ReviveAllHooks.Install();
        var harmony = new Harmony("FloatingCountdown.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        void At(float value) { UnityEngine.Time.unscaledTime = value; CombatMatchRuntime.Tick(); }
        PlayerAvatar[] Begin(string mode)
        {
            var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar };
            NativePlayerFloatingText.Messages.Clear(); NativePlayerFloatingText.Fail = false;
            NativePlayerFloatingText.Styles.Clear();
            At(0); check(SettingsActions.Execute("/one " + mode + " start").Success, "Start floating countdown: " + mode);
            At(3); return players;
        }
        void Kill(PlayerAvatar attacker, PlayerAvatar victim)
        {
            var damage = new DamageInstance(); FriendlyFireRuntime.Attribute(attacker, victim, damage); victim.Die(0, damage);
        }
        try
        {
            foreach (string mode in new[] { "deathmatch", "tournament" })
            {
                var players = Begin(mode);
                check(NativePlayerFloatingText.Messages.Count == 0, mode + " opening/opponent notices keep their current presentation");
                var victim = players.First(a => players.Any(b => CombatMatchRuntime.AllowsPair(a, b)));
                victim.Die(0, new()); At(3.2f); At(4); At(4.2f); At(5); At(5.2f);
                var expected = new[] { "Respawn in: 3s", "Respawn in: 2s", "Respawn in: 1s" };
                check(NativePlayerFloatingText.Messages.Select(m => m.Message).SequenceEqual(expected) &&
                    NativePlayerFloatingText.Messages.All(m => m.Player == victim), mode + " sends one particle per displayed respawn second");
                check(NativePlayerFloatingText.Styles.Count == 3 && NativePlayerFloatingText.Styles.All(bold => bold),
                    mode + " respawn particles use emphasized styling");
                check(DungeonManager.Instance.ChatMessages.Where(m => m.Player == victim && m.Message.StartsWith("Respawn in:"))
                    .Select(m => m.Message).SequenceEqual(expected), mode + " retains every original chat/bubble countdown");
                At(6); At(6.2f);
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && NativePlayerFloatingText.Messages.Count == 3,
                    mode + " revives normally without a stale or extra countdown particle");

                foreach (string transition in new[] { "stop", "disconnect", "replacement", "restart", "manual-revive", "loading" })
                {
                    players = Begin(mode); victim = players.First(a => players.Any(b => CombatMatchRuntime.AllowsPair(a, b)));
                    victim.Die(0, new()); NativePlayerFloatingText.Messages.Clear();
                    switch (transition)
                    {
                        case "stop": CombatMatchRuntime.Stop(true, false); break;
                        case "disconnect": FriendlyFireParticipants.Retire(victim.spawner); break;
                        case "replacement": PlayerSpawner.MultiplayerList.Remove(victim.spawner); add(victim.netId); break;
                        case "restart": HorayModAPI.StartSession(); break;
                        case "manual-revive": victim.Revive(100); break;
                        case "loading": victim.spawner.connectionToClient.isReady = false; break;
                    }
                    At(4); At(5);
                    check(NativePlayerFloatingText.Messages.Count == 0, mode + " invalidates particles after " + transition);
                }
                players = Begin(mode); victim = players.First(a => players.Any(b => CombatMatchRuntime.AllowsPair(a, b)));
                NativePlayerFloatingText.Fail = true; victim.Die(0, new()); At(4); At(5); At(6);
                check(!victim.IsDead && victim.ReviveRpcCalls == 1 && CombatMatchRuntime.IsRunning &&
                    DungeonManager.Instance.ChatMessages.Count(m => m.Player == victim && m.Message.StartsWith("Respawn in:")) == 3,
                    mode + " optional cosmetic failure cannot suppress chat, stop the match or interfere with revival");
            }

            var tagPlayers = Begin("tag");
            var first = NativePlayerFloatingText.Messages.Single();
            check(first.Message == "Bomb: 30s", "Tag emits only short localized bomb text, not its long instruction");
            check(NativePlayerFloatingText.Styles.Single() == false, "Bomb timer keeps its normal floating style");
            check(DungeonManager.Instance.ChatMessages.Any(m => m.Player == first.Player && m.Message.Contains("Pass the bomb")),
                "Tag still sends the full bomb instruction through chat/bubble");
            At(3.2f); At(4); At(4.2f);
            check(NativePlayerFloatingText.Messages.Select(m => m.Message).SequenceEqual(new[] { "Bomb: 30s", "Bomb: 29s" }),
                "Tag never emits the same displayed second twice");
            var recipient = tagPlayers.First(p => p != first.Player);
            Kill(first.Player, recipient);
            check(NativePlayerFloatingText.Messages.Last() == (recipient, "Respawn in: 1s"), "Tag retains its one-second respawn countdown");
            check(NativePlayerFloatingText.Styles.Last(), "Tag's one-second respawn also uses emphasized styling");
            int count = NativePlayerFloatingText.Messages.Count; At(4.5f);
            check(NativePlayerFloatingText.Messages.Count == count, "Transferred bomb emits no timer while victim awaits revival");
            At(5.3f); // Death occurred at 4.2; wait the full native one-second delay.
            check(!recipient.IsDead && NativePlayerFloatingText.Messages.Last() == (recipient, "Bomb: 24s"),
                "Reduced bomb fuse appears on the new carrier only after revival");
            count = NativePlayerFloatingText.Messages.Count; SettingsActions.Execute("/one friendlyfire off"); At(6); At(7);
            check(NativePlayerFloatingText.Messages.Count == count, "Stopping Tag leaves no periodic floating-text work behind");

            // Chat can invoke another addon; retain chat but discard stale optional output.
            var host = Begin("deathmatch")[0];
            DungeonManager.Instance.ChatCallback = () => { DungeonManager.Instance.ChatCallback = null; CombatMatchRuntime.Stop(true, false); };
            host.Die(0, new());
            check(NativePlayerFloatingText.Messages.Count == 0 && !CombatMatchRuntime.IsRunning,
                "Match stop during chat dispatch prevents subsequent floating output");

            host = Begin("deathmatch")[0];
            DungeonManager.Instance.ChatCallback = () => { DungeonManager.Instance.ChatCallback = null; host.Revive(100); };
            host.Die(0, new());
            check(!host.IsDead && NativePlayerFloatingText.Messages.Count == 0 && CombatMatchRuntime.IsRunning,
                "Manual revival during chat prevents a stale respawn particle without stopping the match");

            tagPlayers = Begin("tag"); first = NativePlayerFloatingText.Messages.Single();
            recipient = tagPlayers.First(p => p != first.Player); NativePlayerFloatingText.Messages.Clear();
            DungeonManager.Instance.ChatCallback = () => { DungeonManager.Instance.ChatCallback = null; Kill(first.Player, recipient); };
            At(4);
            check(recipient.IsDead && CombatMatchRuntime.IsRunning && NativePlayerFloatingText.Messages.All(m =>
                m.Player != first.Player || !m.Message.StartsWith("Bomb:")),
                "Bomb transfer during chat prevents a stale timer on the former carrier");

            var both = Begin("deathmatch"); both[0].Die(0, new()); both[1].Die(0, new()); At(4); At(5); At(6);
            check(!both[0].IsDead && !both[1].IsDead && NativePlayerFloatingText.Messages.Count == 6 &&
                NativePlayerFloatingText.Messages.Count(m => m.Player == both[0]) == 3 &&
                NativePlayerFloatingText.Messages.Count(m => m.Player == both[1]) == 3,
                "Simultaneous host and guest respawns each emit their own three countdown seconds");
        }
        finally
        {
            NativePlayerFloatingText.Fail = false;
            harmony.UnpatchAll("FloatingCountdown.integration.tests"); ReviveAllHooks.Uninstall(); start();
        }
    }
}

namespace SephiriaOne
{
    // Native serialization/rendering is validated separately against installed IL.
    internal static class NativePlayerFloatingText
    {
        internal static readonly List<(PlayerAvatar Player, string Message)> Messages = new();
        internal static readonly List<bool> Styles = new();
        internal static bool Fail;
        internal static void Show(PlayerAvatar player, string message, bool emphasize = false)
        {
            if (Fail) throw new InvalidOperationException("optional floating text unavailable");
            Messages.Add((player, message));
            Styles.Add(emphasize);
        }
    }
}
