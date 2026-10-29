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
            check(NativePlayerFloatingText.Messages.Last() == (recipient, "Respawn in: 1s") && NativePlayerFloatingText.Styles.Last(),
                "Tag displays the one-second floating respawn countdown with emphasis");
            int count = NativePlayerFloatingText.Messages.Count; At(4.5f);
            check(NativePlayerFloatingText.Messages.Count == count, "Transferred bomb emits no timer while victim awaits revival");
            At(5.3f); // Death occurred at 4.2; wait the full native one-second delay.
            check(!recipient.IsDead && NativePlayerFloatingText.Messages.Last() == (recipient, "Bomb: 24s"),
                "Reduced bomb fuse appears on the new carrier only after revival");
            count = NativePlayerFloatingText.Messages.Count; SettingsActions.Execute("/one friendlyfire off"); At(6); At(7);
            check(NativePlayerFloatingText.Messages.Count == count, "Stopping Tag leaves no periodic floating-text work behind");

            // Bomb countdown callbacks must still discard stale optional output.
            tagPlayers = Begin("tag");
            NativePlayerFloatingText.Messages.Clear();
            DungeonManager.Instance.ChatCallback = () => { DungeonManager.Instance.ChatCallback = null; CombatMatchRuntime.Stop(true, false); };
            At(4);
            check(NativePlayerFloatingText.Messages.Count == 0 && !CombatMatchRuntime.IsRunning,
                "Match stop during bomb chat dispatch prevents subsequent floating output");

            tagPlayers = Begin("tag"); first = NativePlayerFloatingText.Messages.Single();
            recipient = tagPlayers.First(p => p != first.Player); NativePlayerFloatingText.Messages.Clear();
            DungeonManager.Instance.ChatCallback = () => { DungeonManager.Instance.ChatCallback = null; Kill(first.Player, recipient); };
            At(4);
            check(recipient.IsDead && CombatMatchRuntime.IsRunning && NativePlayerFloatingText.Messages.All(m =>
                m.Player != first.Player || !m.Message.StartsWith("Bomb:")),
                "Bomb transfer during chat prevents a stale timer on the former carrier");


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
