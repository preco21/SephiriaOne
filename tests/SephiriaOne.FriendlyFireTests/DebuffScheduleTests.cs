using SephiriaOne;
using UnityEngine;

internal static class DebuffScheduleTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Native Timer accumulates float delta independently of Time.time's
        // absolute clock. Do not manufacture timestamps from the gate deadline.
        foreach (float start in new[] { 100f, 8192f, 65536f })
            foreach (float seconds in new[] { 0.05f, 0.25f, 0.75f, 0.9f, 1.33f, 10f })
                foreach (int fps in new[] { 60, 120, 144 })
                {
                    var timer = new Timer(0.5f);
                    var clock = new FriendlyFireRuntime.DebuffPulse();
                    double wall = start;
                    float delta = 1f / fps;
                    int ticks = 0;
                    while (ticks < 5)
                    {
                        wall += delta;
                        if (!timer.Update(delta * (timer.time / seconds))) continue;
                        check(clock.Ready((float)wall, seconds), "Accumulated native tick remains due: " + start + "/" + seconds + "/" + fps);
                        clock.Reserve((float)wall);
                        check(!clock.Ready((float)wall, seconds), "Precision allowance never admits a same-frame repeat");
                        ticks++;
                    }
                }
        var cases = new[] {
            (PvpDotEffect.Burn, "Debuff_Burn"), (PvpDotEffect.PlasmaBurn, "Debuff_Plasma"),
            (PvpDotEffect.Frostbite, "Debuff_Frostbite"), (PvpDotEffect.Poison, "Debuff_Poison"),
            (PvpDotEffect.Wound, "Debuff_Wound"), (PvpDotEffect.Lightning, "Debuff_Electric"),
            (PvpDotEffect.Freeze, "Debuff_Freeze"), (PvpDotEffect.Chim, "Debuff_Chim") };
        foreach (var (effect, id) in cases)
        {
            var clock = new FriendlyFireRuntime.DebuffPulse();
            foreach (float start in new[] { 100f, 4096f, 65536f })
                foreach (float interval in new[] { 0.05f, 0.9f, 1.33f, 10f })
                {
                    clock.Reserve(start);
                    check(clock.Ready(start + interval, interval), "Representable due time never drops a tick after long uptime");
                    check(!clock.Ready(start, interval), "Same-frame reapplication cannot pass clock protection");
                }
            FriendlyFireParticipants.Clear(); Time.time = 100;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            var source = new PlayerAvatar(); var target = new PlayerAvatar();
            void Hit(float amount = 2) => target.ApplyDamage(EffectTests.Tick(source, id, amount));
            void Toggle(bool enabled) => SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false,
                dotSchedule: new(effect, enabled)));
            void Interval(decimal seconds) => SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false,
                dot: new(effect, seconds)));
            Interval(1);
            Hit(0); Hit();
            check(target.Hp == 98, effect + " keeps first positive immediate native hit");
            // Each new effect has the same pair clock; failed reapplications must not move it.
            for (int i = 1; i <= 9; i++)
            {
                Time.time = 100 + i / 10f;
                var replacement = new CharacterDebuff { OnTick = () => Hit() };
                replacement.InitializeAndSpawn(source, target); replacement.AddStack();
                check(target.Hp == 98 && replacement.NativeElapsed == 0, effect + " refresh retains lifetime but cannot acquire a fresh damage clock");
            }
            Time.time = 101; Hit(); Hit();
            check(target.Hp == 96, effect + " rejected attempts do not postpone boundary or admit a second hit");
            Toggle(false); Hit(); Hit();
            check(target.Hp == 92, effect + " disabling protection immediately permits native repeated pulses");
            Time.time = 101.5f; Toggle(true); Hit();
            check(target.Hp == 92, effect + " enabling retains last hit while disabled");
            Interval(0.5m); Hit();
            check(target.Hp == 90, effect + " live shortening takes effect without resetting the clock");
            foreach (var (other, otherId) in cases)
                if (other != effect)
                {
                    target.ApplyDamage(EffectTests.Tick(source, otherId, 1));
                    check(target.Hp < 90, "Sibling effect has independent first hit: " + other);
                }
            target.Hp = 100; Hit();
            check(target.Hp == 100, effect + " sibling hits never reset this clock");
            target.OnHit = (_, _) => Hit(); Time.time += 1; Hit(); target.OnHit = null;
            check(target.Hp == 98, effect + " reserve precedes nested native callbacks");
            var companion = new UnitAvatar { NetworkLeader = source };
            target.ApplyDamage(EffectTests.Tick(companion, id, 2));
            check(target.Hp == 98, effect + " companions share their owner's clock");
            var nextSource = new PlayerAvatar();
            target.ApplyDamage(EffectTests.Tick(nextSource, id, 2));
            check(target.Hp == 96, effect + " unrelated attackers retain independent clocks");
            FriendlyFireParticipants.Depart(source); FriendlyFireParticipants.Rejoin(source); Hit();
            check(target.Hp == 94, effect + " rejoin never inherits a previous connection clock");
            target.IsDead = true; target.Revive(100); Hit();
            check(target.Hp == 98, effect + " revival retires the prior life clock");
            FriendlyFireKda.ResetScores(); Hit(); check(target.Hp == 96, effect + " score/session reset clears clock");
            target.currentFloorGuid = "new-floor"; Hit(); check(target.Hp == 94, effect + " floor transition clears clock");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, balance: false));
            Hit(); Hit(); check(target.Hp == 90, effect + " disabling balancing restores native repeated damage");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, balance: true));
            var enemy = new UnitAvatar { faction = "enemy" };
            enemy.ApplyDamage(EffectTests.Tick(source, id, 2)); enemy.ApplyDamage(EffectTests.Tick(source, id, 2));
            check(enemy.Hp == 96, effect + " outgoing PvE keeps all native hits");
            target.ApplyDamage(EffectTests.Tick(enemy, id, 2)); target.ApplyDamage(EffectTests.Tick(enemy, id, 2));
            check(target.Hp == 86, effect + " incoming PvE keeps all native hits");
        }
        // Real periodic timer adapter plus native-like duration refresh: neither restarts cadence.
        FriendlyFireParticipants.Clear(); Time.time = 200;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        var attacker = new PlayerAvatar(); var victim = new PlayerAvatar();
        var burn = new CharacterDebuff_Burn(); burn.InitializeAndSpawn(attacker, victim);
        burn.OnTick = burn.OnUpdate_Server; burn.Step(0.45f);
        float progress = burn.tickTimer.Elapsed;
        burn.OnTick = null; // Native AddStack does not call the per-frame periodic update.
        for (int i = 0; i < 20; i++) burn.AddStack();
        check(burn.tickTimer.Elapsed == progress && burn.NativeElapsed == 0, "Burn stacking refreshes lifetime without resetting periodic progress");
        burn.OnTick = burn.OnUpdate_Server; burn.Step(0.451f);
        check(victim.Hp == 98 && burn.Ticks == 1, "Periodic Burn retains its delayed first tick, not a synthetic immediate hit");
        var plasma = new CharacterDebuff_Plasma(); plasma.InitializeAndSpawn(attacker, victim);
        plasma.tickTimer.Elapsed = 0.2f; plasma.addDamageTimer.Elapsed = 0.7f;
        plasma.OnRefreshAttack = () => {
            var damage = EffectTests.Tick(attacker, "Debuff_Plasma", 2); damage.flag = "DEBUFF_ELECTRIC";
            victim.ApplyDamage(damage);
        };
        plasma.RefreshDurationTimer(2, 0); plasma.addDamageTimer.Elapsed = 0.1f;
        plasma.RefreshDurationTimer(2, 0);
        check(victim.Hp == 96 && plasma.tickTimer.Elapsed == 0.2f && plasma.addDamageTimer.Elapsed == 0,
            "Plasma retains periodic progress and native shock charge resets, without duplicate refresh damage");
        SessionSettings.FriendlyFireForHit = default;
    }
}
