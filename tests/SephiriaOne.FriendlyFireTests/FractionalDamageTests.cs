using SephiriaOne;

internal static class FractionalDamageTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (decimal percent in new[] { 0.01m, 0.1m, 0.2m, 0.3m, 0.4m, 0.5m, 0.6m, 0.7m, 0.8m, 0.9m, 1.25m })
        {
            SessionSettings.FriendlyFireForHit = new(true, percent);
            var source = new PlayerAvatar(); var target = new PlayerAvatar();
            DamageInstance Hit(UnitAvatar caster) => new() { origin = caster, damage = 1000 };
            float loss = (float)(1000 * percent / 100);
            bool Near(float a, float b) => Math.Abs(a - b) < 0.00001f;
            var hit = Hit(source); target.ApplyDamage(hit);
            check(Near(target.Hp, 100 - loss) && target.Hits == 1, "Fractional percent scales HP without integer truncation: " + percent);
            check(FriendlyFireRuntime.ItemTarget(target, EDamageFromType.None, source), "Sub-one scale keeps offensive targeting enabled");
            var enemy = new UnitAvatar { faction = "enemy", Hp = 2000 }; enemy.ApplyDamage(hit);
            check(enemy.Hp == 1000 && hit.damage == 1000, "Fractional scale neither mutates pooled damage nor scales enemy hits");
            target = new PlayerAvatar { Shield = loss / 2 }; target.ApplyDamage(hit);
            check(target.Shield == 0 && Near(target.Hp, 100 - loss / 2), "Fractional shield overflow preserves scaled HP loss");
            target = new PlayerAvatar();
            target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, id = "Ability_Thorns", damage = 1000 });
            target.ApplyDamage(hit);
            check(Near(source.Hp, 100 - loss), "Reflection uses the fractional percentage exactly once");
            target = new PlayerAvatar();
            target.ApplyDebuff(new() { OnApply = (caster, receiver) => receiver.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, source);
            check(Near(target.Hp, 100 - (float)(20 * percent / 100)), "Electric damage retains positive sub-one scaling");
            target = new PlayerAvatar(); var third = new PlayerAvatar();
            var artifact = new Charm_FrostiumRing { NetworkAvatar = source, Target = third, Damage = Hit(source) };
            target.OnHit = (_, _) => artifact.HandleAttackUnit(); target.ApplyDamage(hit);
            check(Near(third.Hp, 100 - loss) && third.Hits == 1, "Nested artifact damage uses same fractional scale without multiplying it twice");
            target = new PlayerAvatar(); SessionSettings.FriendlyFireForHit = new(false, percent); target.ApplyDamage(hit);
            check(target.Hp == 100 && target.Hits == 0, "Off still blocks positive fractional hits");
        }
        SessionSettings.FriendlyFireForHit = new(true, 0.1m);
        var attacker = new PlayerAvatar(); var victim = new PlayerAvatar { IsMpShield = true, Mp = 1 };
        victim.ApplyDamage(new() { origin = attacker, damage = 2000 });
        check(victim.Mp == 0 && victim.Hp == 99, "0.1% scales before native MP shield conversion and overflow");
        victim = new PlayerAvatar(); victim.ApplyDamage(new() { origin = attacker, damage = 1 });
        check(victim.Hp < 100 && victim.Hp > 99.99f, "Sub-one damage is not rounded back to one HP by the addon");
        SessionSettings.FriendlyFireForHit = new(true, 0);
        victim = new PlayerAvatar(); victim.ApplyDamage(new() { origin = attacker, damage = 1000 });
        check(victim.Hp == 100 && victim.Hits == 0, "Exact zero still rejects damage and callbacks");
    }
}
