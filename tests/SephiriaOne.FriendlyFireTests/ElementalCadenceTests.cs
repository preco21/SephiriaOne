using System.Runtime.CompilerServices;
using SephiriaOne;
using UnityEngine;

public abstract class PeriodicDebuffFixture : CharacterDebuff
{
    public int Ticks;
    public abstract void OnUpdate_Server();
    protected void Tick(string id)
    { Ticks++; NetworkTarget.ApplyDamage(EffectTests.Tick(NetworkAttacker, id, 2)); }
    public void Step(float delta)
    { Time.deltaTime = delta; Time.time += delta; Update(); }
}
public sealed class CharacterDebuff_Frostbite : PeriodicDebuffFixture
{
    private Timer frostDamageTimer = new(1);
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnUpdate_Server()
    { if (NetworkAttacker && frostDamageTimer.Update(Time.deltaTime)) Tick("Debuff_Frostbite"); }
}
public sealed class CharacterDebuff_Poison : PeriodicDebuffFixture
{
    public Timer poisonTickTimer = new(1);
    public bool isSystemDamage;
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnUpdate_Server()
    { if (poisonTickTimer.Update(Time.deltaTime)) Tick("Debuff_Poison"); }
}
public sealed class CharacterDebuff_Wound : PeriodicDebuffFixture
{
    public Timer tickTimer = new(1);
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnUpdate_Server()
    { if (tickTimer.Update(Time.deltaTime)) Tick("Debuff_Wound"); }
}
public sealed class CharacterDebuff_Plasma : PeriodicDebuffFixture
{
    public Timer tickTimer = new(0.5f), addDamageTimer = new(1);
    public float Speed = 1;
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnUpdate_Server()
    {
        addDamageTimer.AddTimerNoLimit(Time.deltaTime);
        float speed = Speed;
        if (tickTimer.Update(Time.deltaTime * speed)) Tick("Debuff_Burn");
    }
}

internal static class ElementalCadenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        foreach (var create in new Func<PeriodicDebuffFixture>[] {
            () => new CharacterDebuff_Frostbite(), () => new CharacterDebuff_Poison(),
            () => new CharacterDebuff_Wound(), () => new CharacterDebuff_Plasma() })
        {
            PeriodicDebuffFixture New(UnitAvatar target = null)
            {
                var effect = create(); effect.InitializeAndSpawn(new PlayerAvatar(), target ?? new PlayerAvatar());
                effect.OnTick = effect.OnUpdate_Server; return effect;
            }
            var effect = New(); float interval = effect is CharacterDebuff_Plasma ? 0.5f : 1;
            effect.Step(interval);
            check(effect.Ticks == 0, effect.GetType().Name + " shares the slower PvP damage timer");
            effect.Step(interval / 3 + 0.001f);
            check(effect.Ticks == 1, effect.GetType().Name + " still ticks at 75% native frequency");
            if (effect is CharacterDebuff_Plasma plasma)
                check(Math.Abs(plasma.addDamageTimer.Elapsed - (interval * 4 / 3 + 0.001f)) < 0.0001f,
                    "Plasma's separate shock charge accumulator is not slowed as a periodic timer");
            effect = New(new UnitAvatar { faction = "enemy" }); effect.Step(interval);
            check(effect.Ticks == 1, effect.GetType().Name + " preserves PvE cadence");
            effect = New(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: false); effect.Step(interval);
            check(effect.Ticks == 1, effect.GetType().Name + " preserves native cadence with balancing off");
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        }
        var system = new CharacterDebuff_Poison { isSystemDamage = true };
        system.InitializeAndSpawn(new PlayerAvatar(), new PlayerAvatar()); system.OnTick = system.OnUpdate_Server;
        system.Step(1); check(system.Ticks == 1, "System poison keeps its native timer");
        SessionSettings.FriendlyFireForHit = default;
    }
}
