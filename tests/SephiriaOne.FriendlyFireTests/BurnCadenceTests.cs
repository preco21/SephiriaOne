using System.Runtime.CompilerServices;
using SephiriaOne;
using UnityEngine;

// The native tick/expiry timers are independent. Keep their boundaries visible
// to the real transpiler; native IL is checked separately against the game DLL.
public sealed class Timer
{
    public float time, Elapsed;
    public Timer(float duration) { time = duration; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void SetTimer(float elapsed) { Elapsed = elapsed; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void AddTimerNoLimit(float delta) { Elapsed += delta; }
    [MethodImpl(MethodImplOptions.NoInlining)] public bool Update(float delta)
    {
        Elapsed += delta;
        if (Elapsed < time) return false;
        Elapsed = 0;
        return true;
    }
}
public sealed class CharacterDebuff_Burn : CharacterDebuff
{
    public Timer tickTimer = new(0.5f);
    public float Speed = 1, DurationElapsed;
    public int Ticks;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnUpdate_Server()
    {
        float speed = Speed;
        if (NetworkTarget && !NetworkTarget.IsDead && tickTimer.Update(Time.deltaTime * speed))
        {
            Ticks++;
            NetworkTarget.ApplyDamage(EffectTests.Tick(NetworkAttacker, amount: 2));
        }
    }
    public void Step(float delta)
    {
        Time.deltaTime = delta; Time.time += delta;
        Update(); DurationElapsed += delta;
    }
}

internal static class BurnCadenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        CharacterDebuff_Burn Burn(UnitAvatar source = null, UnitAvatar target = null)
        {
            var effect = new CharacterDebuff_Burn();
            effect.InitializeAndSpawn(source ?? new PlayerAvatar(), target ?? new PlayerAvatar());
            effect.OnTick = effect.OnUpdate_Server;
            return effect;
        }
        var burn = Burn(); burn.Step(0.5f);
        check(burn.Ticks == 0, "Balanced PvP burn no longer ticks after the native half second");
        burn.Step(0.17f); check(burn.Ticks == 0, "Default PvP burn waits past the former 0.667-second interval");
        burn.Step(0.08f);
        check(burn.Ticks == 0, "Default PvP burn waits past the former 0.75-second default");
        burn.Step(0.151f);
        check(burn.Ticks == 1 && Math.Abs(burn.DurationElapsed - 0.901f) < 0.0001f && burn.tickTimer.time == 0.5f,
            "PvP burn ticks at 0.9 seconds without modifying duration or the native timer interval");
        burn = Burn(); burn.Speed = 2; burn.Step(0.25f);
        check(burn.Ticks == 0, "Burn-speed bonuses cannot bypass the configured PvP interval");
        burn.Step(0.651f); check(burn.Ticks == 1, "Fast burn follows the same configured seconds");
        burn = Burn(); burn.Step(0.25f);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false);
        burn.Step(0.362f); check(burn.Ticks == 1, "Balance off immediately restores native timer advance without resetting elapsed time");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        burn.Step(0.5f); check(burn.Ticks == 1, "Reenabling balancing affects an existing burn without rewriting its state");
        burn.Step(0.401f); check(burn.Ticks == 2, "Existing burn continues naturally after balancing is restored");

        foreach (var pair in new[] {
            (new PlayerAvatar() as UnitAvatar, new UnitAvatar { faction = "enemy" }),
            (new UnitAvatar { faction = "enemy" }, new PlayerAvatar() as UnitAvatar),
            (new PlayerAvatar() as UnitAvatar, new UnitAvatar { faction = "friends" }) })
        {
            burn = Burn(pair.Item1, pair.Item2); burn.Step(0.5f);
            check(burn.Ticks == 1, "PvE and non-player burn victims keep native cadence");
        }
        var self = new PlayerAvatar(); burn = Burn(self, self); burn.Step(0.5f);
        check(burn.Ticks == 1, "Self-effect timer remains native");
        burn = Burn(); Mirror.NetworkServer.active = false; burn.Step(0.5f);
        check(burn.Ticks == 1, "Client-side execution never scales the burn timer"); Mirror.NetworkServer.active = true;
        burn = Burn(); SessionSettings.FriendlyFireForHit = new(false, 100, balance: true); burn.Step(0.5f);
        check(burn.Ticks == 1 && burn.NetworkTarget.Hp == 100, "FF off keeps native time while shared impact policy blocks allied damage");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        burn = Burn(); FriendlyFireParticipants.Select((PlayerAvatar)burn.NetworkAttacker); burn.Step(0.5f);
        check(burn.Ticks == 1 && burn.NetworkTarget.Hp == 100, "Unselected players are not slowed or damaged");
        FriendlyFireParticipants.Clear();
        burn = Burn(); CombatMatchRuntime.PairRule = (_, _) => false; burn.Step(0.5f);
        check(burn.Ticks == 1 && burn.NetworkTarget.Hp == 100, "Tournament bystanders keep native time and stay protected");
        CombatMatchRuntime.PairRule = null; burn.Step(0.5f);
        check(burn.Ticks == 1, "Current Tournament opponents use the shared cadence policy");
        var owner = new PlayerAvatar(); burn = Burn(new UnitAvatar { NetworkLeader = owner }); burn.Step(0.5f);
        check(burn.Ticks == 0, "Owned companion burn follows its player's PvP balancing policy");
        burn = Burn(); FriendlyFireParticipants.Depart((PlayerAvatar)burn.NetworkAttacker); burn.Step(1);
        check(burn.IsEndBuff && burn.Ticks == 0, "Departed source's burn is retired before any tick");
        FriendlyFireParticipants.Rejoin((PlayerAvatar)burn.NetworkAttacker); burn.Step(1);
        check(burn.Ticks == 0, "Reconnection cannot revive the previous connection's burn");
        burn = Burn(); burn.Step(0); check(burn.Ticks == 0, "Paused game time cannot advance burn");
        for (int i = 0; i < 1000; i++) burn.OnUpdate_Server();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) burn.OnUpdate_Server();
        check(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed burn cadence hook allocates no per-frame memory");
        SessionSettings.FriendlyFireForHit = default;
    }
}
