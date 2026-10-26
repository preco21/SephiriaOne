using SephiriaOne;

internal static class BalanceTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        var damage = new DamageInstance { origin = source, damage = 20 };
        target.ApplyDamage(damage);
        check(target.Hp == 80, "Balancing preserves a native hit below the shared reserve");
        check(damage.damage == 20 && target.MaxHp == 100, "Balancing never rewrites pooled damage or maximum HP");
        bool Near(double left, double right) => Math.Abs(left - right) <= Math.Max(0.000001, Math.Abs(right) * 0.000001);
        foreach (double hp in new[] { 0.01, 10, 100, 150, 200, 1000, 10000000 })
        {
            foreach (double fraction in new[] { 0.0001, 0.01, 0.05, 0.2, 0.75, 1, 100, 10000000 })
            {
                var budget = new FriendlyFireDamageBudget();
                check(Near(budget.Take(hp * fraction, hp, 0), hp * Math.Min(fraction, .7)),
                    "Fresh burst preserves funded damage and bounds extremes at HP " + hp);
            }
        }
        foreach (double invalid in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var budget = new FriendlyFireDamageBudget();
            check(budget.Take(20, invalid, 0) == 0, "Invalid max HP never produces invalid damage");
            check(budget.Take(invalid, 100, 0) == 0, "Invalid damage never produces invalid damage");
        }

        // The shared budget funds damage once, before native shields and HP accounting.
        target = new PlayerAvatar(); target.ApplyDamage(damage); target.ApplyDamage(damage);
        check(target.Hp == 60 && target.Hits == 2, "Repeated attacks retain native impacts but share the balanced burst budget");
        target = new PlayerAvatar { Hp = 30, MaxHp = 200 }; target.ApplyDamage(damage);
        check(Near(target.Hp, 10), "Near-death hits use maximum HP, never shrinking current HP");
        target = new PlayerAvatar(); target.ApplyDamage(new() { origin = source, damage = 50 });
        target.MaxHp = 200; target.Hp = 100;
        target.ApplyDamage(new() { origin = source, damage = float.MaxValue });
        check(Near(target.Hp, 60), "Maximum-HP doubling turns the remaining 20% reserve into 40 HP, without resetting credit");
        target = new PlayerAvatar { Shield = 10 }; target.ApplyDamage(damage);
        check(target.Shield == 0 && target.Hp == 90, "Shields absorb funded damage with native HP overflow");
        target = new PlayerAvatar { IsMpShield = true, Mp = 10 }; target.ApplyDamage(damage);
        check(target.Mp == 0 && target.Hp == 90, "MP shields retain native spending and overflow after budgeting");
        target = new PlayerAvatar { Defense = 10, TrueDamage = 10 }; target.ApplyDamage(damage);
        check(target.Hp == 80, "Shared reserve accounts for resolved defense/true damage exactly once");
        target = new PlayerAvatar { Defense = 15 }; target.ApplyDamage(damage);
        check(target.Hp == 95, "A defensive build retains its native reduction before the shared reserve");
        target = new PlayerAvatar { IsInvulnerable = true }; target.ApplyDamage(damage);
        check(target.Hp == 100, "Balancing cannot create damage through native invulnerability");
        target = new PlayerAvatar { MaxHp = float.NaN }; target.ApplyDamage(damage);
        check(target.Hp == 100, "Invalid live maximum HP fails safely without a SyncVar poison");

        target = new PlayerAvatar();
        target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, id = "Ability_Thorns", damage = 20 });
        target.ApplyDamage(damage);
        check(source.Hp == 80 && target.Hp == 80, "Native reflected player damage uses the same shared reserve once");
        var companion = new UnitAvatar { NetworkLeader = source };
        target = new PlayerAvatar(); target.ApplyDamage(new() { origin = companion, damage = 20 });
        check(target.Hp == 80, "Owned companions receive the same player-victim balancing");
        target = new PlayerAvatar();
        target.ApplyDebuff(new() { OnApply = (caster, receiver) => receiver.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, source);
        check(target.Hp == 80 && target.DebuffApplications == 1, "Debuffs retain application and have their damage budgeted once");
        var other = new PlayerAvatar(); target = new PlayerAvatar();
        var proc = new Charm_FrostiumRing { NetworkAvatar = source, Target = other, Damage = damage };
        target.OnHit = (_, _) => proc.HandleAttackUnit(); target.ApplyDamage(damage);
        check(target.Hp == 80 && other.Hp == 80, "Nested offensive artifact keeps its proc and separate victim calculation");

        var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(damage);
        check(enemy.Hp == 80, "Balance enabled leaves outgoing PvE damage untouched");
        target = new PlayerAvatar(); target.ApplyDamage(new() { origin = enemy, damage = 20, targetFactionLayers = -1 });
        check(target.Hp == 80, "Balance enabled leaves incoming PvE damage untouched");
        var ally = new UnitAvatar { faction = "friends" }; ally.ApplyDamage(damage);
        check(ally.Hp == 80, "Non-player friendly NPC health is not normalized");
        foreach (decimal percent in new[] { 0m, 0.1m, 25m, 100m, 300m })
        {
            SessionSettings.FriendlyFireForHit = new(true, percent, balance: true);
            target = new PlayerAvatar(); target.ApplyDamage(damage);
            check(Near(target.Hp, 100 - (percent == 0 ? 0 : Math.Max(1, Math.Min(20 * (double)percent / 100, 70)))),
                "Existing percentage is applied once with a positive-only final minimum: " + percent);
        }
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        target = new PlayerAvatar(); target.ApplyDamage(new() { origin = source, damage = float.MaxValue });
        check(target.Hp == 30, "Float-scale extreme hit reaches the shared 70% reserve instead of one-shot damage");
        target = new PlayerAvatar();
        // Before-attack callbacks belong to the attacking avatar.
        source.OnAttackUnitBeforeOperation = (_, _) => SessionSettings.FriendlyFireForHit = new(true, 100, balance: false);
        target.ApplyDamage(damage); source.OnAttackUnitBeforeOperation = null;
        check(target.Hp == 80, "Disabling balancing during native callbacks applies immediately");
        SessionSettings.FriendlyFireForHit = new(false, 100, balance: true);
        target = new PlayerAvatar(); target.ApplyDamage(damage);
        check(target.Hp == 100, "Balance preference alone never enables friendly fire");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        FriendlyFireParticipants.Select(source); target = new PlayerAvatar(); target.ApplyDamage(damage);
        check(target.Hp == 100, "Unselected players do not enter balancing or damage paths");
        FriendlyFireParticipants.Clear();
        CombatMatchRuntime.PairRule = (_, _) => false;
        target = new PlayerAvatar(); target.ApplyDamage(damage);
        check(target.Hp == 100, "Tournament non-opponents remain protected with balancing enabled");
        CombatMatchRuntime.PairRule = null;

        for (int i = 0; i < 1000; i++) Measure();
        check(Measure() == 0, "Warm per-hit balance arithmetic allocates no memory");
        SessionSettings.FriendlyFireForHit = default;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long Measure()
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        var budget = new FriendlyFireDamageBudget();
        for (int i = 0; i < 1000; i++) sum += budget.Take(1000000 + i, 150, i);
        return sum > 0 ? GC.GetAllocatedBytesForCurrentThread() - allocated : -1;
    }
}
