using SephiriaOne;
using UnityEngine;

internal static class PeriodicRecoveryTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        DamageInstance hit = null;
        void Set(string edit)
        {
            check(FriendlyFireCommand.Parse("/one friendlyfire rapid " + edit, out var command) == FriendlyFireParseResult.Valid,
                "Periodic recovery command: " + edit);
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(command);
        }
        void Fresh(bool frequency = true)
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            Mirror.NetworkServer.active = true; Time.time = Time.unscaledTime = 0;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: frequency,
                refillPercent: 1, rapid: new PvpRapidSettings(burstPercent: 20));
            source = new(); target = new() { Hp = 1000, MaxHp = 100 };
            hit = new() { origin = source, damage = 100, fromType = EDamageFromType.DirectAttack };
        }
        float Strike(float time, DamageInstance packet = null, PlayerAvatar victim = null)
        {
            Time.time = time; victim ??= target; victim.Hp = 1000;
            victim.ApplyDamage(packet ?? hit); return 1000 - victim.Hp;
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .002;

        foreach (var sample in new[] { (.1m, 1d, 2d), (.12m, 100d, 103d) })
        {
            var clock = new PvpRecoveryClock();
            var settings = new PvpRapidSettings(resetSeconds: sample.Item1);
            check(!clock.Due(sample.Item2, settings, 0) && clock.Due(sample.Item3, settings, 0) &&
                !clock.Due(sample.Item3, settings, 0), "Fractional missed deadlines recover only once at the same timestamp");
        }

        Fresh(); Set("window 10"); Set("rate 0.1"); Set("exponent 2");
        check(Strike(0) == 20, "First hit uses normal configured reserve");
        for (int i = 1; i < 10; i++) check(Near(Strike(i / 10f), 1), "Continuous valid hits retain the floor while depleted");
        check(Strike(1) == 20, "Default one-second recovery restores burst AND cadence during uninterrupted fire");
        for (int i = 11; i < 20; i++) Strike(i / 10f);
        check(Strike(2) == 20, "Repeated recovery does not require a firing pause");

        Fresh(false); Set("interval 3"); check(Strike(0) == 20, "Frequency-off has the same shared reserve");
        check(Near(Strike(2.9f), 2.9) && Strike(3) == 20 && Strike(3) == 1,
            "Periodic recovery remains independent of frequency toggle and occurs once per boundary");
        check(Strike(100) == 20 && Strike(100) == 1, "Long idle never banks missed resets");
        check(Strike(102) == 20, "Late observation retains the original fixed deadline phase");
        Set("recovery off"); check(Strike(105) == 3, "Off immediately retains only continuous refill");
        Set("recovery on"); check(Strike(105) == 1 && Strike(107) == 2 && Strike(108) == 20,
            "Re-enabling arms a future deadline without granting immediate credit");
        Set("interval 2"); check(Strike(108) == 1 && Strike(109) == 1 && Strike(110) == 20,
            "Live interval edit starts a new deadline without minting an immediate burst");
        Fresh(false); Set("interval 3"); Strike(0); Set("recovery off"); Set("recovery on");
        check(Strike(3) == 3 && Strike(6) == 20, "Unobserved off/on edits rearm through settings revision without minting credit");
        Fresh(false); Set("interval 3"); Strike(0); target.currentFloorGuid = "next-floor";
        check(Strike(2) == 20 && Strike(3) == 1 && Strike(5) == 20, "Floor transition starts a fresh pair clock with normal lifetime cleanup");

        Fresh(false); Set("interval 3"); var other = new PlayerAvatar { Hp = 1000, MaxHp = 100 };
        Strike(0); Strike(1, victim: other);
        check(Strike(3) == 20 && Strike(3, victim: other) == 2 && Strike(4, victim: other) == 20,
            "Opponents keep independent clocks");
        var secondary = new DamageInstance { origin = source, damage = 100, id = "Charm_Meteor" };
        check(Strike(4, secondary) == 1, "Artifact and direct attacks share the same pair capacity");

        Fresh(false); Set("interval 3"); Strike(0);
        target.SuperArmor = true;
        bool nested = false;
        target.OnHit = (_, _) =>
        {
            if (nested) return;
            nested = true; target.SuperArmor = false; Time.time = 3;
            new Charm_FrostiumRing { NetworkAvatar = source, Target = target,
                Damage = new DamageInstance { origin = source, damage = 100 } }.HandleAttackUnit();
        };
        float nestedDamage = Strike(2.9f);
        check(nestedDamage > 20 && nested, $"Nested hit can cross a recovery boundary: damage {nestedDamage}, nested {nested}");
        target.OnHit = null;
        check(Strike(3) == 1, "Earlier in-flight hit cannot refund into the recovered reserve");

        Fresh(false); Set("interval 3");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, refillPercent: 300,
            rapid: new PvpRapidSettings(burstPercent: 20, resetSeconds: 3), lowerBound: new PvpLowerBoundSettings(true));
        Strike(0); hit.damage = 1; target.SuperArmor = true;
        target.OnHit = (_, _) =>
        {
            target.OnHit = null; target.SuperArmor = false; Time.time = 3;
            new Charm_FrostiumRing { NetworkAvatar = source, Target = target,
                Damage = new DamageInstance { origin = source, damage = 100 } }.HandleAttackUnit();
        };
        Strike(2.9f);
        check(Near(Strike(3.1f), 4.5), "Periodic recovery preserves refunds to the unchanged assistance pool");

        Fresh(); Set("window 10"); Set("rate 0.1"); Set("exponent 2");
        hit.damage = 10; target.MaxHp = 100000;
        Strike(0);
        for (int i = 1; i < 30; i++) Strike(i / 10f);
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        int hits = target.Hits;
        for (int i = 30; i < 3030; i++) Strike(i / 10f);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        check(bytes == 0 && target.Hits == hits + 3000, "Warmed continuous fire and periodic recovery allocate nothing and discard no valid hits");
        Console.WriteLine($"Periodic recovery: {bytes} bytes/3000 continuous hits");
        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
    }
}
