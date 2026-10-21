using SephiriaOne;
using UnityEngine;

// Explicit diagnostic, not a permanent assertion that this tuning is desirable.
// Run with --burst-audit. Uses the actual addon hooks on the combat fixture,
// then the production arithmetic for controlled packet-size comparisons.
internal static class DamagePipelineAudit
{
    internal static void Run()
    {
        const double hp = 150;
        int checks = 0;
        void Near(double actual, double expected, string name)
        { if (Math.Abs(actual - expected) > .001) throw new Exception(name); checks++; }
        PlayerAvatar source = null, target = null;
        void Fresh(bool balance, decimal burst = 20)
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
        Console.WriteLine("HP=150; damage scale=0.3%; refill=300%; rapid-hit toggle OFF; no shields, role modifiers or amplification cap.");
        foreach (double input in new[] { 100d, 130d })
        {
            Fresh(false); double off = Strike(input); Near(off, input, "Unbalanced scaled baseline");
            Fresh(true); double on = Strike(input);
            double softened = FriendlyFireBalance.Soften(input, hp);
            Near(on, softened, "Single hit loses damage in unconditional per-hit curve");
            Time.time += 2.5f; Near(Strike(input), softened, "Long pause cannot escape per-hit curve");
            Fresh(true, 100); Near(Strike(input), softened, "100% stored capacity cannot escape per-hit curve");
            var pool = new FriendlyFireDamageBudget();
            Near(pool.Take(input, hp, 0, refillPerSecond: 3, minimumDamage: 1), 30, "Bypassing curve alone still leaves 20% capacity");
            Console.WriteLine($"Native resolved {input / .003d:F3} -> scale {off:F3} -> soften {softened:F3} -> full-budget {on:F3} -> HP loss {on:F3}; native displayed integer {(int)on}.");
        }
        foreach (bool frequencyOn in new[] { false, true })
        {
            var budget = new FriendlyFireDamageBudget();
            var frequency = new FriendlyFireHitFrequency();
            var rapid = new PvpRapidSettings(hitsPerSecond: 6);
            double total = 0;
            for (int i = 0; i < 16; i++)
            {
                double now = i / 15d;
                double damage = FriendlyFireBalance.Soften(130d / 16, hp);
                if (frequencyOn) damage *= frequency.Next(now, rapid);
                total += budget.Take(damage, hp, now, refillPerSecond: 3, minimumDamage: 1);
            }
            if (total <= 3 * FriendlyFireBalance.Soften(130, hp)) throw new Exception("Expected current split-hit advantage");
            checks++;
            Console.WriteLine($"Same total 130 split into 16 hits over 1s; rapid {(frequencyOn ? "ON, rate 6" : "OFF")}: {total:F3} damage.");
        }
        Console.WriteLine($"Passed {checks} current-pipeline diagnostic checks. No game or production tuning changed.");
    }
}
