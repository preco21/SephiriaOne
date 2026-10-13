using SephiriaOne;
using UnityEngine;

internal static class RapidHitTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        Time.time = 10;
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        var hit = new DamageInstance { origin = source, damage = 20 };
        int callbacks = 0;
        target.OnHit = (_, _) => callbacks++;
        for (int i = 0; i < 20; i++) target.ApplyDamage(hit);
        check(target.Hp == 80, "Rapid same-pair impacts share one 20%-max-HP burst instead of each receiving a full allowance");
        check(target.Hits == 20 && callbacks == 20 && hit.damage == 20,
            "Budget preserves native attack callbacks and pooled source damage");
        target = new PlayerAvatar { IsMpShield = true, Mp = 100 };
        hit.damage = float.MaxValue;
        target.ApplyDamage(hit);
        for (int i = 1; i <= 100; i++) { Time.time = 10 + i / 100f; target.ApplyDamage(hit); }
        check(target.Mp >= 49 && target.Mp <= 51 && target.Hp == 100,
            "Rapid refill is not lost to native integer MP shield rounding; actual shield damage spends the budget");
        Arithmetic(check);

        void Fresh()
        {
            FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            Time.time = 10; source = new(); target = new();
            hit = new() { origin = source, damage = float.MaxValue };
        }
        bool Near(double a, double b) => Math.Abs(a - b) < 0.001;
        void Strike() => target.ApplyDamage(hit);
        Fresh(); Strike(); Time.time += 0.1f; Strike();
        check(Near(target.Hp, 77), "Continuous refill admits 3% max HP after 0.1 game seconds");
        Time.time += 1; Strike();
        check(Near(target.Hp, 57), "Idle refill stops at burst capacity, never saves an unlimited future burst");

        Fresh(); Strike();
        hit.origin = new UnitAvatar { NetworkLeader = source }; Strike();
        hit.origin = source; hit.id = "Charm_PallasCard"; Strike();
        target.ApplyDebuff(new() { OnApply = (caster, receiver) => receiver.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, source);
        check(target.Hp == 80 && target.DebuffApplications == 1, "Companions, card projectiles and debuff ticks share the owner's budget");
        var otherVictim = new PlayerAvatar(); otherVictim.ApplyDamage(hit);
        check(otherVictim.Hp == 80, "One attacker has an independent budget for another opponent");
        hit.origin = new PlayerAvatar(); Strike();
        check(target.Hp == 60, "Another attacker can damage the same victim independently");

        Fresh();
        var proc = new Charm_FrostiumRing { NetworkAvatar = source, Target = target, Damage = hit };
        target.OnHit = (_, _) => { target.OnHit = null; proc.HandleAttackUnit(); };
        Strike();
        check(target.Hp == 80 && target.Hits == 2, "Nested same-pair artifact reserves from the same budget before native callbacks");
        Fresh(); target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, id = "Ability_Thorns", damage = float.MaxValue });
        Strike(); Strike();
        check(source.Hp == 80 && target.Hp == 80, "Reflection has its own reverse-direction budget and cannot bypass repeated-hit limits");

        Fresh(); target.Shield = 15; Strike(); Strike();
        check(target.Shield == 0 && target.Hp == 95, "Shield absorption and HP overflow spend the same allowance exactly once");
        Fresh(); target.SuperArmor = true; Strike();
        check(target.Hp == 87, "Post-scale super armor preserves native reduction on the first hit");
        for (int i = 0; i < 20; i++) Strike();
        check(Near(target.Hp, 80), "Saturating super armor is bounded by actual HP damage, not pre-mitigation reservations");
        Fresh(); target.ClampHpDamage = true; target.Hp = 5; target.ExtraLife = true; Strike();
        check(target.Hp == 60 && !target.IsDead, "Native additional life remains native and does not call the dead-player revival hook");
        Strike();
        check(target.Hp == 45, "Native HP cap refunds overkill, while additional life does not reset the pair's damage budget");

        Fresh(); target.IsMpShield = true; hit.damage = 20;
        target.OnHit = (_, _) =>
        {
            target.OnHit = null;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: false);
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            target.ApplyDamage(new() { origin = source, damage = float.MaxValue });
        };
        Strike(); Time.time += 0.02f; hit.damage = float.MaxValue; Strike();
        check(target.Mp == 68, "An outer hit's fractional refund cannot replenish a nested replacement budget generation");
        Fresh(); target.IsInvulnerable = true; Strike(); target.IsInvulnerable = false; Strike();
        check(target.Hp == 80, "Native rejected hits never consume the first damaging allowance");
        Fresh(); target.OnHit = (_, _) => throw new InvalidOperationException("budget fixture");
        try { Strike(); } catch (InvalidOperationException) { }
        target.OnHit = null; Strike();
        check(target.Hp == 80, "Exceptions preserve already-consumed damage and restore the surrounding hit context");

        Fresh(); Strike();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false); Strike();
        check(target.IsDead, "Balance off restores unrestricted native friendly-fire damage immediately");
        Fresh(); Strike();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); Strike();
        check(target.Hp == 60, "Off/on without intervening impacts starts a fresh budget");
        Fresh(); Strike(); SessionSettings.FriendlyFireForHit = new(true, 50, balance: true); Strike();
        check(target.Hp == 80, "Editing damage percentage does not refill a depleted budget");
        FriendlyFireKda.ResetScores(); Strike();
        check(target.Hp == 60, "KDA reset clears previous combat allowance state");
        FriendlyFireParticipants.Select(source, target); Strike();
        check(target.Hp == 40, "Participant/match epoch changes start fresh budgets");
        target.currentFloorGuid = "floor-2"; Strike();
        check(target.Hp == 20, "Floor transitions retire the prior floor's allowance");

        Fresh(); Strike(); FriendlyFireParticipants.Depart(target); Strike();
        check(target.Hp == 80, "Departed victims cannot consume or receive damage");
        FriendlyFireParticipants.Rejoin(target); Strike();
        check(target.Hp == 60, "Rejoining the same avatar object uses a fresh connection receipt");
        FriendlyFireParticipants.Depart(source); Strike();
        check(target.Hp == 60, "Departed attack sources cannot keep damaging guests");
        FriendlyFireParticipants.Rejoin(source); Strike();
        check(target.Hp == 40, "Rejoined attack source does not inherit the previous connection's budget");
        Fresh(); Strike(); FriendlyFireParticipants.Clear(); Strike();
        check(target.Hp == 60, "A new session/run scope cannot reuse an earlier budget");
        Fresh(); Strike(); Time.time = 0; Strike();
        check(target.Hp == 60, "Game-clock restart cannot leave a permanently exhausted budget");
        Fresh(); Strike(); target.MaxHp = 200; Strike();
        check(target.Hp == 80, "Changing maximum HP never manufactures an immediate new allowance");
        Time.time += 0.1f; Strike();
        check(Near(target.Hp, 74), "Future refill scales from the victim's current maximum HP");

        Fresh(); Strike(); source.IsDead = true; Strike();
        check(target.Hp == 80, "Queued projectiles from a dead owner retain the existing budget");
        source.Revive(100); Strike();
        check(target.Hp == 60, "Immediate source revival starts a new combat life");
        target.IsDead = true; target.Revive(100); Strike();
        check(target.Hp == 80, "Immediate victim revival does not inherit pre-death allowance debt");
        Fresh(); Strike();
        var enemy = new UnitAvatar { faction = "enemy", Hp = 1000 };
        var pveHit = new DamageInstance { origin = source, damage = 100 };
        enemy.ApplyDamage(pveHit); enemy.ApplyDamage(pveHit);
        target.ApplyDamage(new() { origin = enemy, damage = 10, targetFactionLayers = -1 });
        Strike();
        check(enemy.Hp == 800 && target.Hp == 70, "Exhausted PvP allowance neither limits nor is refilled by outgoing/incoming PvE");

        Fresh(); hit.damage = 20;
        for (int i = 0; i < 100; i++) { Time.time++; Strike(); target.Hp = 100; }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Time.time++; Strike(); target.Hp = 100; }
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed full balanced damage hooks allocate no memory per hit");
        SessionSettings.FriendlyFireForHit = default;
    }

    private static void Arithmetic(Action<bool, string> check)
    {
        bool Near(double a, double b) => Math.Abs(a - b) <= Math.Max(1e-8, Math.Abs(b) * 1e-6);
        foreach (double hp in new[] { 0.01, 100, 150, 200, 10000, 10000000 })
        {
            var budget = new FriendlyFireDamageBudget();
            double sum = 0;
            for (int i = 0; i < 100; i++) sum += budget.Take(hp, hp, 10);
            check(Near(sum, 0.2 * hp), "Same-time storm respects maximum-HP-relative burst at " + hp);
            for (int i = 1; i <= 100; i++) sum += budget.Take(hp, hp, 10 + i / 100d);
            check(Near(sum, 0.5 * hp), "One second of continuous attacks admits burst plus refill at " + hp);
            check(Near(budget.Take(hp, hp, 100), 0.2 * hp), "Long inactivity cannot accumulate extra capacity");
            budget = default;
            for (int i = 0; i < 10; i++)
                check(Near(budget.Take(hp * 0.1, hp, i), hp * 0.1), "Ordinary spaced hits remain unchanged");
            budget = default;
            check(Near(budget.Take(hp, hp, 0.999), hp * 0.2) && budget.Take(hp, hp, 1.001) < hp * 0.001,
                "No fixed-second boundary permits a double burst");
        }
        var invalidBudget = new FriendlyFireDamageBudget();
        foreach (double invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
        {
            check(invalidBudget.Take(invalid, 100, 0) == 0 && invalidBudget.Take(20, invalid, 0) == 0,
                "Invalid amounts or maximum HP never poison budget state");
        }
        check(invalidBudget.Take(100, 100, double.NaN) == 0 && invalidBudget.Take(100, 100, double.PositiveInfinity) == 0,
            "Invalid clocks never manufacture damage");
        check(invalidBudget.Take(100, 100, 0) == 20, "Invalid inputs consume no credit before a valid hit");
        var huge = new FriendlyFireDamageBudget();
        double enormous = huge.Take(double.MaxValue, double.MaxValue, 0);
        check(double.IsFinite(enormous) && enormous <= double.MaxValue * 0.2, "Finite extreme arithmetic remains bounded");
    }
}
