using SephiriaOne;

internal static class DeathVisualTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Reset()
        {
            FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100);
            FriendlyFireDeathVisuals.Shown.Clear();
            FriendlyFireDeathAudio.Reset();
            FriendlyFireDeathVisuals.Fail = false;
        }
        void Die(PlayerAvatar player) => player.Die(5, new() { origin = new UnitAvatar { faction = "enemy" } });
        Reset();
        var player = new PlayerAvatar();
        Die(player);
        check(FriendlyFireDeathVisuals.Shown.SequenceEqual(new[] { player }), "Enabled FF shows death FX even for environmental/enemy deaths without KDA attribution");
        check(FriendlyFireDeathAudio.Shown.SequenceEqual(new[] { player }), "A confirmed death dispatches audio with FX");
        Die(player);
        check(FriendlyFireDeathVisuals.Shown.Count == 1, "An already-dead player cannot replay death FX");
        check(FriendlyFireDeathAudio.Shown.Count == 1, "Repeated native death callback cannot replay audio");
        player.Revive(100); Die(player);
        check(FriendlyFireDeathVisuals.Shown.Count == 2, "A new life can show death FX again");
        Reset();
        new UnitAvatar().Die(5, new());
        check(FriendlyFireDeathVisuals.Shown.Count == 0, "NPC deaths do not receive player FX");
        foreach (string transition in new[] { "off", "selection", "disconnect", "run", "revive", "destroy" })
        {
            Reset(); player = new PlayerAvatar();
            player.OnDeath = _ =>
            {
                switch (transition)
                {
                    case "off": SessionSettings.FriendlyFireForHit = default; break;
                    case "selection": FriendlyFireParticipants.Select(new PlayerAvatar()); break;
                    case "disconnect": FriendlyFireParticipants.Depart(player); break;
                    case "run": FriendlyFireParticipants.Clear(); break;
                    case "revive": player.Revive(100); break;
                    case "destroy": player.Destroyed = true; break;
                }
            };
            Die(player);
            check(FriendlyFireDeathVisuals.Shown.Count == 0, "Death callback invalidates cosmetic receipt: " + transition);
            check(FriendlyFireDeathAudio.Shown.Count == 0, "Death callback also invalidates audio: " + transition);
        }
        Reset(); player = new PlayerAvatar(); FriendlyFireParticipants.Select(new PlayerAvatar()); Die(player);
        check(FriendlyFireDeathVisuals.Shown.Count == 0, "Unselected spectators do not receive FF death FX");
        Reset(); SessionSettings.FriendlyFireForHit = default; Die(new());
        check(FriendlyFireDeathVisuals.Shown.Count == 0, "Normal play retains native local death FX only");
        Reset(); player = new PlayerAvatar { ExtraLife = true };
        player.ApplyDamage(new() { origin = new PlayerAvatar(), damage = 200 });
        check(!player.IsDead && FriendlyFireDeathVisuals.Shown.Count == 0, "Extra-life protection is not a confirmed down");
        Reset(); Mirror.NetworkServer.active = false; Die(new()); Mirror.NetworkServer.active = true;
        check(FriendlyFireDeathVisuals.Shown.Count == 0, "Clients never send death FX");
        Reset(); player = new PlayerAvatar { OnDeath = _ => throw new InvalidOperationException("failed native death") };
        try { Die(player); } catch (InvalidOperationException) { }
        check(FriendlyFireDeathVisuals.Shown.Count == 0, "Incomplete native death cannot broadcast a cosmetic event ahead of RpcDie");
        Reset(); player = new PlayerAvatar(); FriendlyFireDeathVisuals.Fail = true; Die(player);
        check(FriendlyFireDeathAudio.Shown.Count == 1, "Optional visual failure cannot skip valid audio");
        check(CombatMatchRuntime.LastDead == player, "Optional visual load failure does not skip shared match recovery");
        Reset();
    }
}

namespace SephiriaOne
{
    internal static class FriendlyFireDeathAudio
    {
        internal static readonly List<PlayerAvatar> Shown = new();
        internal static void Reset() => Shown.Clear();
        internal static void Show(PlayerAvatar player) => Shown.Add(player);
    }
    // Native transport is checked against installed assemblies by the contract suite.
    internal static class FriendlyFireDeathVisuals
    {
        internal static readonly List<PlayerAvatar> Shown = new();
        internal static bool Fail;
        internal static void Show(PlayerAvatar player)
        { if (Fail) throw new TypeLoadException("optional cosmetic unavailable"); Shown.Add(player); }
        internal static void Reset() { }
    }
}
