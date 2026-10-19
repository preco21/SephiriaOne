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
    public Timer frostDamageTimer = new(1);
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
public class CharacterDebuff_Plasma : PeriodicDebuffFixture
{
    public Timer tickTimer = new(0.5f), addDamageTimer = new(1);
    public float Speed = 1;
    public Action OnRefreshAttack;
    [MethodImpl(MethodImplOptions.NoInlining)] public override void RefreshDurationTimer(float defaultDuration, float bonusPercent)
    {
        OnRefreshAttack?.Invoke();
        base.RefreshDurationTimer(defaultDuration, bonusPercent);
        addDamageTimer.Elapsed = 0;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnUpdate_Server()
    {
        addDamageTimer.AddTimerNoLimit(Time.deltaTime);
        float speed = Speed;
        if (tickTimer.Update(Time.deltaTime * speed)) Tick("Debuff_Plasma");
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
            float balancedInterval = effect is CharacterDebuff_Wound ? 1.33f : 0.75f;
            effect.Step(balancedInterval - 0.01f);
            check(effect.Ticks == 0, effect.GetType().Name + " waits for its balanced periodic interval");
            effect.Step(0.011f);
            check(effect.Ticks == 1, effect.GetType().Name + " ticks at its configured seconds; Wound keeps prior cadence");
            if (effect is CharacterDebuff_Plasma plasma)
                check(Math.Abs(plasma.addDamageTimer.Elapsed - (balancedInterval + 0.001f)) < 0.0001f,
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
        var settings = default(PvpDotSettings);
        foreach (var kind in Enum.GetValues<PvpDotEffect>()) settings = settings.Apply(new(kind, (int)kind + 1));
        foreach (var kind in new[] { PvpDotEffect.Burn, PvpDotEffect.PlasmaBurn, PvpDotEffect.Frostbite, PvpDotEffect.Poison, PvpDotEffect.Wound })
        {
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, dotIntervals: settings);
            CharacterDebuff effect = kind switch
            {
                PvpDotEffect.Burn => new CharacterDebuff_Burn { Speed = 100 },
                PvpDotEffect.PlasmaBurn => new CharacterDebuff_Plasma { Speed = 100 },
                PvpDotEffect.Frostbite => new CharacterDebuff_Frostbite(),
                PvpDotEffect.Wound => new CharacterDebuff_Wound(),
                _ => new CharacterDebuff_Poison()
            };
            effect.InitializeAndSpawn(new PlayerAvatar(), new PlayerAvatar());
            if (effect is CharacterDebuff_Burn burn) effect.OnTick = burn.OnUpdate_Server;
            else effect.OnTick = ((PeriodicDebuffFixture)effect).OnUpdate_Server;
            Timer timer = effect switch
            {
                CharacterDebuff_Burn b => b.tickTimer, CharacterDebuff_Plasma p => p.tickTimer,
                CharacterDebuff_Wound w => w.tickTimer,
                CharacterDebuff_Frostbite f => f.frostDamageTimer, CharacterDebuff_Poison p => p.poisonTickTimer,
                _ => throw new Exception()
            };
            void Step(float delta)
            {
                if (effect is CharacterDebuff_Burn b) b.Step(delta);
                else ((PeriodicDebuffFixture)effect).Step(delta);
            }
            int Ticks() => effect is CharacterDebuff_Burn b ? b.Ticks : ((PeriodicDebuffFixture)effect).Ticks;
            // Prove interval independence and support for a serialized native
            // timer length different from the constructor default.
            timer.time = 2.5f;
            float seconds = settings.Interval(kind);
            Step(seconds - 0.01f); check(Ticks() == 0, kind + " uses its own interval, regardless of native length/speed");
            // A refreshed Plasma can reach a two-second tick interval without
            // expiring first. Lifetime/late-tick behavior is tested separately.
            if (effect is CharacterDebuff_Plasma) effect.RefreshDurationTimer(999, 999);
            Step(0.011f);
            check(Ticks() == 1 && timer.time == 2.5f, kind + " ticks without mutating native timer length");
            Step(seconds / 2);
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, dot: new(kind, 0.5m)));
            Step(0.24f); check(Ticks() == 1, kind + " live edit preserves fractional progress");
            Step(0.011f); check(Ticks() == 2, kind + " live edit applies without restarting the effect");
            Step(0); check(Ticks() == 2, kind + " paused time adds no tick");
            for (int n = 0; n < 100; n++) Step(0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int n = 0; n < 1000; n++) Step(0);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            check(bytes == 0, kind + " adds no warmed per-tick allocation");
        }
        SessionSettings.FriendlyFireForHit = default;
    }
}
