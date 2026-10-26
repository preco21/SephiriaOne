using SephiriaOne;
using UnityEngine;

// Run with --burst-audit. Actual addon hooks on the combat fixture, followed
// by controlled packet comparisons. Historical arithmetic lives only here.
internal static class DamagePipelineAudit
{
    internal static void Run()
    {
        const double hp = 150;
        int checks = 0;
        void Near(double actual, double expected, string name)
        { if (Math.Abs(actual - expected) > .001) throw new Exception(name); checks++; }
        PlayerAvatar source = null, target = null;
        void Fresh(bool balance, decimal burst = PvpRapidSettings.DefaultBurstPercent)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; Mirror.NetworkServer.active = true; Time.time = 10;
            SessionSettings.FriendlyFireForHit = new(true, .3m, balance: balance, refillPercent: 300,
                hitFrequency: false, rapid: new PvpRapidSettings(burstPercent: burst));
            source = new(); target = new() { Hp = (float)hp, MaxHp = (float)hp };
        }
        double Strike(double beforeBalance)
        {
            target.Hp = (float)hp;
            target.ApplyDamage(new DamageInstance { origin = source, damage = (float)(beforeBalance / .003d),
                fromType = EDamageFromType.DirectAttack });
            return hp - target.Hp;
        }
        Console.WriteLine("HP=150; damage scale=0.3%; refill=300%; frequency OFF; no shields, roles or amplification cap.");
        foreach (double input in new[] { 100d, 130d })
        {
            Fresh(false); double off = Strike(input); Near(off, input, "Unbalanced scaled baseline");
            foreach (decimal burst in new[] { 20m, 75m, 100m })
            {
                Fresh(true, burst); double on = Strike(input);
                Near(on, Math.Min(input, hp * (double)burst / 100), "Shared reserve governs the heavy hit");
                Time.time += 2.5f; Near(Strike(input), on, "Slow hit regains its stored allowance");
                Console.WriteLine($"Native resolved {input / .003d:F3} -> scale {off:F3} -> reserve {burst}% -> HP loss {on:F3} (old curve {LegacySoften(input, hp):F3}).");
            }
        }
        foreach (double duration in new[] { 0d, 1d })
        foreach (double refill in new[] { .7, 3 })
        foreach (bool frequencyOn in new[] { false, true })
        {
            var budget = new FriendlyFireDamageBudget();
            var frequency = new FriendlyFireHitFrequency();
            var rapid = new PvpRapidSettings(hitsPerSecond: 6);
            double total = 0, funded = 0;
            for (int i = 0; i < 16; i++)
            {
                double now = duration * i / 15d;
                double damage = 130d / 16;
                if (frequencyOn) damage *= frequency.Next(now, rapid);
                total += budget.Take(damage, hp, now, out double charge, refillPerSecond: refill, minimumDamage: 1);
                funded += charge;
            }
            if (funded > hp * (.7 + refill * duration) + .001 || total - funded > 16)
                throw new Exception("Split packets cannot acquire extra funded reserve");
            if (!frequencyOn) Near(total, duration == 0 ? 108 : 130, "Only elapsed refill and the minimum explain split damage");
            checks++;
            Console.WriteLine($"130 / 16 hits over {duration}s, refill {100 * refill}%/s, frequency {(frequencyOn ? "ON, rate 6" : "OFF")}: {total:F3} damage ({funded:F3} funded).");
        }
        Console.WriteLine($"Passed {checks} shared-burst diagnostic checks. Fixture, not live gameplay.");
    }

    private static double LegacySoften(double damage, double hp)
    {
        double knee = hp * .05, span = hp * .15;
        return damage <= knee ? damage : knee + span / (1 + span / (damage - knee));
    }
}
