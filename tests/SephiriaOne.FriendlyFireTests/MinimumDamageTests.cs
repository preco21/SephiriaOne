using SephiriaOne;
using UnityEngine;

internal static class MinimumDamageTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Fresh(bool balance = false)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; Mirror.NetworkServer.active = true; Time.time = 10;
            SessionSettings.FriendlyFireForHit = new(true, 0.05m, balance: balance);
        }
        foreach (bool balance in new[] { false, true })
        {
            Fresh(balance);
            var source = new PlayerAvatar();
            DamageInstance Hit(float amount = 10) => new() { origin = source, damage = amount };
            var target = new PlayerAvatar(); target.ApplyDamage(Hit());
            check(target.Hp == 99, "Positive sub-one PvP damage has a one-point floor; balanced=" + balance);
            target = new PlayerAvatar { IsMpShield = true }; target.ApplyDamage(Hit());
            check(target.Hp == 100 && target.Mp == 99, "Minimum hit reaches native MP shield integer conversion");
            target = new PlayerAvatar { Shield = 0.5f }; target.ApplyDamage(Hit());
            check(target.Shield == 0 && target.Hp == 99.5f, "Shield absorption is never rounded back up to another minimum hit");
            foreach (float invalid in new[] { 0f, -1, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                target = new PlayerAvatar(); target.ApplyDamage(Hit(invalid));
                check(target.Hp == 100, "Zero/invalid raw damage stays harmless: " + invalid);
                target = new PlayerAvatar { OnCalculateDamage = d => d.damage = invalid }; target.ApplyDamage(Hit());
                check(target.Hp == 100, "Zero/invalid resolved damage stays harmless: " + invalid);
            }
            target = new PlayerAvatar { IsInvulnerable = true }; target.ApplyDamage(Hit());
            check(target.Hp == 100, "Minimum damage never bypasses invulnerability");
            target = new PlayerAvatar { IsGuarding = true }; target.ApplyDamage(Hit());
            check(target.Hp == 100, "Minimum damage never bypasses a native guard");
            var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(Hit());
            check(enemy.Hp == 90, "PvE never receives the PvP floor or scale");
            SessionSettings.FriendlyFireForHit = new(true, 0, balance: balance);
            target = new PlayerAvatar(); target.ApplyDamage(Hit());
            check(target.Hp == 100 && target.Hits == 0, "Explicit zero percent still blocks all damage");
        }
        Fresh(true);
        var attacker = new PlayerAvatar(); var victim = new PlayerAvatar();
        var tiny = new DamageInstance { origin = attacker, damage = 10 };
        for (int i = 0; i < 100; i++) victim.ApplyDamage(tiny);
        check(Math.Abs(victim.Hp - 80) < 0.001, "Minimum hits are charged in full and cannot bypass the same-frame budget");
        Time.time += 0.005f; victim.ApplyDamage(tiny);
        check(victim.Hp == 79, "A positive fractional refill allows one minimum hit");
        for (int i = 0; i < 100; i++) victim.ApplyDamage(tiny);
        check(victim.Hp == 79, "Rounding debt blocks further hits until refill repays it");
        Time.time += 0.005f; victim.ApplyDamage(tiny);
        check(victim.Hp == 79, "Partial refill cannot erase minimum-hit debt");
        Time.time += 0.01f; victim.ApplyDamage(tiny);
        check(victim.Hp == 78, "Further refill repays debt and restores positive hits");

        // Reservation precedes callbacks, including a nested artifact hitting
        // the same pair while an outer minimum hit already borrowed the refill.
        var proc = new Charm_FrostiumRing { NetworkAvatar = attacker, Target = victim, Damage = tiny };
        victim.OnHit = (_, _) => { victim.OnHit = null; proc.HandleAttackUnit(); };
        Time.time += 0.01f; victim.ApplyDamage(tiny);
        check(victim.Hp == 77, "Nested damage cannot reuse credit borrowed for an outer minimum hit");

        Fresh(true);
        SessionSettings.FriendlyFireForHit = new(true, 0.05m, balance: true, hitFrequency: true);
        attacker = new(); victim = new(); tiny = new() { origin = attacker, damage = 10 };
        for (int i = 0; i < 100; i++) victim.ApplyDamage(tiny);
        check(victim.Hp == 80, "Hit-frequency reductions share the minimum and its full budget charge");

        foreach (decimal incoming in new[] { 0m, 0.01m, 30m, 75m, 100m })
        {
            foreach (bool balance in new[] { false, true })
            {
                Fresh(balance); attacker = new(); victim = new();
                tiny = new() { origin = attacker, damage = 10 };
                CombatMatchRuntime.Chaser = victim;
                SessionSettings.PlayTagChaser = new(false, 2, true, incoming);
                CombatMatchRuntime.RoleSettingsChanged();
                try
                {
                    victim.ApplyDamage(tiny);
                    check(victim.Hp == (incoming == 0 ? 100 : 99), "Role reduction cannot lose a positive minimum or promote zero: " + incoming);
                    if (!balance || incoming == 0) continue;
                    for (int i = 0; i < 100; i++) victim.ApplyDamage(tiny);
                    double allowance = 20 * (double)incoming / 100;
                    double loss = 100 - victim.Hp;
                    check(loss >= allowance && loss < allowance + 1, "Role-reduced minimum charges pre-role units with less than one point of debt");
                    float before = victim.Hp;
                    CombatMatchRuntime.Chaser = null; CombatMatchRuntime.RoleSettingsChanged(); victim.ApplyDamage(tiny);
                    check(victim.Hp == before, "Removing role protection cannot erase existing minimum-hit debt");
                }
                finally { CombatMatchRuntime.Chaser = null; SessionSettings.PlayTagChaser = default; CombatMatchRuntime.RoleSettingsChanged(); }
            }
        }

        // Tiny real HP pools can still receive a hit, but cannot borrow once
        // per attempt forever when their entire burst is less than one point.
        foreach (double maxHp in new[] { 100d, 1710, 2300, 10000 })
        {
            var repeated = new FriendlyFireDamageBudget();
            double total = 0;
            for (int i = 0; i < 3000; i++) total += repeated.Take(0.005, maxHp, 0, minimumDamage: 1);
            check(total == maxHp * 0.2, "Repeated minimum hits cannot amplify accumulated precision dust at HP " + maxHp);
        }
        var budget = new FriendlyFireDamageBudget();
        check(budget.Take(0.1, 2, 0, minimumDamage: 1) == 1 && budget.Take(0.1, 2, 0, minimumDamage: 1) == 0,
            "A sub-one burst capacity permits one minimum hit then keeps its debt");
        check(budget.Take(0.1, 2, 0.25, minimumDamage: 1) == 0 && budget.Take(0.1, 2, 0.5, minimumDamage: 1) == 1,
            "Small-HP minimum debt is repaid at the actual configured refill rate");
        foreach (double invalid in new[] { -1, double.NaN, double.PositiveInfinity })
        {
            budget = default;
            check(budget.Take(10, 100, 0, minimumDamage: invalid) == 0 && budget.Take(100, 100, 0) == 20,
                "Invalid minimum cannot poison future budget state");
        }
        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
    }
}
