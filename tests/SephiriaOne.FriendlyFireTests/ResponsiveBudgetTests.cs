using SephiriaOne;
using UnityEngine;

// Explicit 20% reserve below tests legacy configured capacity, independent of shipped defaults.
internal static class ResponsiveBudgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Fresh(bool frequency = false)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, rapid: new PvpRapidSettings(burstPercent: 20), hitFrequency: frequency);
            Time.time = 10;
        }
        Fresh();
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        var hit = new DamageInstance { origin = source, damage = float.MaxValue, fromType = EDamageFromType.DirectAttack };
        target.ApplyDamage(hit); target.ApplyDamage(hit); target.ApplyDamage(hit);
        check(target.Hp == 78 && target.Hits == 3, "Exhausted budget retains one damage on every valid hit");

        Fresh(true); source = new(); target = new() { Hp = 10000, MaxHp = 10000 };
        hit = new() { origin = source, damage = 2, fromType = EDamageFromType.Magic };
        for (int i = 0; i < 4; i++) target.ApplyDamage(hit);
        var helper = new UnitAvatar { NetworkLeader = source };
        for (int i = 0; i < 30; i++) target.ApplyDamage(new() { origin = helper, damage = 2 });
        float before = target.Hp; target.ApplyDamage(hit);
        check(Math.Abs(before - target.Hp - 2 * Math.Sqrt(4d / 5)) < .001,
            "Companion barrage cannot inflate direct-attack cadence");
        // Accepted periodic effects and async artifact projectiles use the same
        // incidental category, even when native tags look like direct magic.
        foreach (bool artifact in new[] { false, true })
        {
            Fresh(true); source = new(); target = new() { Hp = 10000, MaxHp = 10000 };
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(
                new(false, dotSchedule: new(PvpDotEffect.Burn, false)));
            hit = new() { origin = source, damage = 2, fromType = EDamageFromType.DirectAttack };
            for (int i = 0; i < 4; i++) target.ApplyDamage(hit);
            var incidental = artifact
                ? new DamageInstance { origin = source, damage = 2, fromType = EDamageFromType.Magic, id = "Charm_TestProjectile" }
                : EffectTests.Tick(source, "Debuff_Burn", 2);
            for (int i = 0; i < 30; i++) target.ApplyDamage(incidental);
            before = target.Hp; target.ApplyDamage(hit);
            check(Math.Abs(before - target.Hp - 2 * Math.Sqrt(4d / 5)) < .001,
                "Accepted incidental damage never inflates direct cadence: artifact=" + artifact);
            // Pooled ID changes must be reclassified without an object cache.
            incidental.id = "Skill_FireBolt"; incidental.fromType = EDamageFromType.Magic;
            incidental.damageType = EDamageType.Projectile;
            before = target.Hp; target.ApplyDamage(incidental);
            check(Math.Abs(before - target.Hp - 2 * Math.Sqrt(4d / 6)) < .001,
                "Reused damage joins existing direct history when current metadata changes");
            FriendlyFireKda.ResetScores();
            before = target.Hp; target.ApplyDamage(hit); target.ApplyDamage(EffectTests.Tick(source, "Debuff_Burn", 2));
            check(before - target.Hp == 4, "Score reset clears both cadence categories together");
        }
        Fresh(true); source = new(); target = new() { Hp = 10000, MaxHp = 10000 };
        hit = new() { origin = source, damage = 2, fromType = EDamageFromType.Magic };
        for (int i = 0; i < 30; i++) target.ApplyDamage(hit);
        before = target.Hp; target.ApplyDamage(EffectTests.Tick(source, "Debuff_Poison", 2));
        check(before - target.Hp == 2, "Rapid direct attacks do not slow the first incidental hit");
        var companionHit = new DamageInstance { origin = new UnitAvatar { NetworkLeader = source }, damage = 2 };
        for (int i = 0; i < 3; i++) target.ApplyDamage(companionHit);
        before = target.Hp;
        target.ApplyDamage(new() { origin = source, damage = 2, fromType = EDamageFromType.None, id = "Charm_Other" });
        check(Math.Abs(before - target.Hp - 2 * Math.Sqrt(4d / 5)) < .001,
            "Different incidental sources share one history instead of gaining counters per item");
        // A real death/revival boundary retires both histories, not just direct.
        source.IsDead = true; source.Revive(100);
        before = target.Hp; target.ApplyDamage(hit); target.ApplyDamage(companionHit);
        check(before - target.Hp == 4, "Revival retires both history categories through the shared owner lifetime");

        Fresh(); source = new(); target = new();
        hit = new() { origin = source, damage = float.MaxValue };
        target.ApplyDamage(hit); target.SuperArmor = true;
        for (int i = 0; i < 30; i++) target.ApplyDamage(hit);
        target.SuperArmor = false; before = target.Hp; target.ApplyDamage(hit);
        check(before - target.Hp == 1, "Unfunded armor losses cannot manufacture refunded allowance");
        target.IsMpShield = true; target.Mp = 100;
        for (int i = 0; i < 10; i++) target.ApplyDamage(hit);
        check(target.Mp == 50 && target.Hp == before - 1, "Minimum-only hits pay the shield MP floor without free absorption or token refunds");
        target.IsMpShield = false;
        var nested = new Charm_FrostiumRing { NetworkAvatar = source, Target = target, Damage = hit };
        target.OnHit = (_, _) => { target.OnHit = null; nested.HandleAttackUnit(); };
        before = target.Hp; target.ApplyDamage(hit); target.ApplyDamage(hit);
        check(before - target.Hp == 3, "Nested minimum-only hits never reuse credit or create a second burst");

        // An actual late veto refunds only what was funded, never the floor.
        var result = EApplyDamageResult.Success;
        FriendlyFireRuntime.BeforeHit(target, hit, ref result, out var scope);
        check(FriendlyFireRuntime.Scale(100) == 1, "Empty pool reserves a minimum-only hit");
        FriendlyFireRuntime.AfterHit(null, scope); // Native callback accepts no HP/MP damage.
        before = target.Hp; target.ApplyDamage(hit);
        check(before - target.Hp == 1, "Cancelled unfunded hit cannot refill the allowance");
        Time.time += .01f; // 0.7 damage credit, smaller than the final floor.
        FriendlyFireRuntime.BeforeHit(target, hit, ref result, out scope);
        check(FriendlyFireRuntime.Scale(100) == 1, "Partially funded hit retains final minimum");
        FriendlyFireRuntime.AfterHit(null, scope);
        before = target.Hp; target.ApplyDamage(hit); target.ApplyDamage(hit);
        check(before - target.Hp == 2, "Partial refund restores only paid credit, with no floor debt or free burst");

        var budget = new FriendlyFireDamageBudget();
        check(budget.Take(100, 100, 0, out double paid, minimumDamage: 1) == 70 && paid == 70,
            "Initial full allowance is funded exactly");
        check(budget.Take(100, 100, 0, out paid, minimumDamage: 1) == 1 && paid == 0,
            "Exhausted floor has zero refundable charge");
        check(budget.Take(100, 100, .01, out paid, minimumDamage: 1) == 1 && Math.Abs(paid - .7) < 1e-9,
            "Fractional refill never funds more than its actual credit");
        check(Math.Abs(budget.Take(100, 100, .11, out paid, minimumDamage: 1) - 7) < 1e-9,
            "Minimum-only hits accumulate no debt or delayed damage");
        foreach (double invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
            check(budget.Take(invalid, 100, 1, out paid, minimumDamage: 1) == 0 && paid == 0,
                "Invalid or zero damage cannot be promoted by exhaustion floor");

        Fresh(true); source = new(); target = new();
        hit = new() { origin = source, damage = 2, fromType = EDamageFromType.Magic };
        companionHit = new() { origin = new UnitAvatar { NetworkLeader = source }, damage = 2 };
        void Storm()
        {
            for (int i = 0; i < 1000; i++)
            {
                target.Hp = 100;
                target.ApplyDamage(hit); target.ApplyDamage(companionHit);
            }
        }
        Storm();
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        Storm();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        check(bytes == 0 && target.Hp == 98, "Warmed two-category exhausted hits allocate nothing and still land");
        Console.WriteLine($"Responsive PvP cadence: {bytes} bytes/2000 mixed hits");
        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
    }
}
