using SephiriaOne;
using UnityEngine;

internal static class SharedBurstTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        void Fresh(decimal burst = 75, decimal refill = 300, bool frequency = false)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; Mirror.NetworkServer.active = true; Time.time = 10;
            SessionSettings.FriendlyFireForHit = new(true, .3m, balance: true, refillPercent: refill,
                hitFrequency: frequency, rapid: new PvpRapidSettings(burstPercent: burst));
            source = new(); target = new() { Hp = 150, MaxHp = 150 };
        }
        float Strike(float scaled, EDamageFromType type = EDamageFromType.DirectAttack, string id = "")
        {
            // Restore HP without a revive: comparisons retain the same live pair's budget.
            target.Hp = 150;
            target.ApplyDamage(new() { origin = source, damage = scaled / .003f, fromType = type, id = id });
            return 150 - target.Hp;
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .001;
        Fresh();
        check(Near(Strike(100), 100), "Infrequent 100 damage hit is preserved within a 112.5-HP shared allowance");
        Fresh();
        check(Near(Strike(130), 112.5), "Large hit is limited only by shared allowance, not the old per-hit ceiling");
        Time.time += 2.5f;
        check(Near(Strike(130), 112.5), "Slow attacks recover their allowance during native downtime");
        Fresh(20);
        check(Near(Strike(130), 30), "Explicit legacy 20-percent setting remains a real capacity limit");
        Fresh(100);
        check(Near(Strike(130), 130), "Configured full-HP reserve does not hide another low per-hit ceiling");

        Fresh(); double combined = Strike(100);
        Fresh(); double split = 0;
        for (int i = 0; i < 16; i++) split += Strike(100f / 16, EDamageFromType.Magic);
        check(Near(combined, split), "Equal simultaneous damage is independent of packet size while funded");
        Fresh(); split = 0;
        for (int i = 0; i < 16; i++) split += Strike(130f / 16, EDamageFromType.Magic);
        check(Near(split, 114.5), "Exhausted barrage has only the documented minimum-one exception, not fresh per-packet allowance");

        Fresh(refill: 70);
        check(Near(Strike(100), 100) && Near(Strike(100, EDamageFromType.Magic), 12.5) &&
            Strike(100, EDamageFromType.Magic, "Charm_TestProjectile") == 1,
            "Direct, spell and artifact damage consume the same pair reserve");
        Time.time += .1f;
        check(Near(Strike(100), 10.5), "Sustained output recovers at configured HP-per-second rate");
        check(Strike(100) == 1 && target.Hits == 5, "Valid exhausted hits retain native impact callbacks");

        Fresh(frequency: true);
        check(Near(Strike(100), 100), "First heavy hit is preserved with optional frequency scaling on too");
        Fresh(); target.Shield = 40;
        check(Near(Strike(100), 60) && target.Shield == 0, "Native shields absorb the newly funded heavy hit normally");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, .3m, balance: false);
        check(Near(Strike(130), 130), "Disabling balance restores native PvP-scaled damage immediately");
        check(default(PvpRapidSettings).BurstPercent == 70 && default(PvpRapidSettings).BurstScale == .7,
            "Default policy stores a meaningful 70-percent burst without altering frequency parameters");
        check(new FriendlyFireDamageBudget().Take(130, 150, 0) == 105,
            "Pure budget and session settings use the same default capacity");
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear(); SessionSettings.FriendlyFireForHit = default;
    }
}
