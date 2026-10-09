using SephiriaOne;

internal static class ReflectionTests
{
    private static DamageInstance Return(UnitAvatar source, string id, float amount) =>
        new() { origin = source, id = id, fromType = EDamageFromType.None, damage = amount, targetFactionLayers = -1 };

    internal static void Run(Action<bool, string> check)
    {
        foreach (string id in new[] { "Ability_Thorns", "Weapon_Reflect", "Charm_VenomSporePouch" })
        foreach (int percent in new[] { 25, 50, 100, 200, 300 })
        {
            SessionSettings.FriendlyFireForHit = new(true, percent);
            var a = new PlayerAvatar { Name = "A", Hp = 1000 };
            var b = new PlayerAvatar { Name = "B", Hp = 1000 };
            Action<DamageInstance> reflect = d => ((UnitAvatar)d.origin).ApplyDamage(Return(b, id, 20));
            // Model the native dispatch points: weapon reflection during guard,
            // Spore Pouch on parry, and Thorns at the end of successful damage.
            if (id == "Weapon_Reflect") { b.IsGuarding = true; b.OnGuardSucceeded = reflect; }
            else if (id == "Charm_VenomSporePouch") { b.IsParrying = true; b.OnParry = reflect; }
            else b.OnHit = (_, d) => reflect(d);
            var incoming = new DamageInstance { origin = a, damage = 10 };
            b.ApplyDamage(incoming);
            check(a.Hp == 1000 - 20 * percent / 100f, id + " returns scaled damage to original player at " + percent + "%");
            check(b.Hp == (id == "Ability_Thorns" ? 1000 - 10 * percent / 100f : 1000), id + " preserves native damage vs guard/parry");
            check(incoming.damage == 10, "Reflection does not rewrite incoming raw damage: " + id);
        }

        SessionSettings.FriendlyFireForHit = new(true, 50);
        var first = new PlayerAvatar { Name = "Attacker" };
        var second = new PlayerAvatar { Name = "Reflector" };
        int attempts = 0;
        void Reflect(UnitAvatar from, DamageInstance hit)
        {
            if (++attempts > 10) throw new Exception("Reflection recursion escaped its bound");
            ((UnitAvatar)hit.origin).ApplyDamage(Return(from, "Ability_Thorns", 20));
        }
        first.OnHit = (from, hit) => Reflect(from, hit);
        second.OnHit = (from, hit) => Reflect(from, hit);
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 90 && second.Hp == 95 && first.Hits == 1 && second.Hits == 1 && attempts == 2,
            "Mutual Thorns allows one return and rejects reflection of reflection");
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 80 && second.Hp == 90 && attempts == 4, "A later independent hit can reflect again");

        first = new PlayerAvatar { IsGuarding = true };
        second = new PlayerAvatar { IsGuarding = true };
        first.OnGuardSucceeded = d => ((UnitAvatar)d.origin).ApplyDamage(Return(first, "Weapon_Reflect", d.damage * 2));
        second.OnGuardSucceeded = d => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Weapon_Reflect", d.damage * 2));
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 100 && second.Hp == 100 && first.Mp == 90 && second.Mp == 90 && first.GuardHits == 1 && second.GuardHits == 1,
            "Mutual guard reflection is bounded before repeated MP costs");

        SessionSettings.FriendlyFireForHit = new(true, 25);
        first = new PlayerAvatar(); second = new PlayerAvatar { IsGuarding = true };
        second.OnGuardSucceeded = d => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Weapon_Reflect", d.damage * 2));
        second.ApplyDamage(new() { origin = first, damage = 20 });
        check(first.Hp == 90 && second.Hp == 100, "Native 200% weapon return uses raw incoming 20, then applies 25% once");
        SessionSettings.FriendlyFireForHit = new(true, 50);

        first = new PlayerAvatar(); second = new PlayerAvatar();
        second.OnHit = (_, d) =>
        {
            ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 10));
            ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Charm_VenomSporePouch", 20));
        };
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 85 && first.Hits == 2, "Separate native return effects share the parent safely without suppressing sibling effects");

        first = new PlayerAvatar(); second = new PlayerAvatar();
        second.OnHit = (_, d) =>
        {
            SessionSettings.FriendlyFireForHit = new(true, 200);
            ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 20));
        };
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(second.Hp == 95 && first.Hp == 60, "Return reads current scale if policy changes inside the incoming hit");
        SessionSettings.FriendlyFireForHit = new(true, 50);

        foreach (bool mpShield in new[] { false, true })
        {
            first = new PlayerAvatar { Defense = 4, TrueDamage = 2, Shield = mpShield ? 0 : 3, IsMpShield = mpShield, Mp = 3 };
            second = new PlayerAvatar();
            second.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 20));
            second.ApplyDamage(new() { origin = first, damage = 10 });
            check(first.Hp == 94 && (mpShield ? first.Mp == 0 : first.Shield == 0),
                "Return scales resolved defense/true damage once before shield overflow, MP=" + mpShield);
        }

        first = new PlayerAvatar { IsInvulnerable = true };
        second = new PlayerAvatar();
        second.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 20));
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 100 && first.Hits == 0, "Reflections retain victim invulnerability");
        first.IsInvulnerable = false;
        first.OnCalculateDamage = d => d.failed = EDamageFailType.Deny;
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 100, "Other damage vetoes still reject a reflected hit");

        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            first = new PlayerAvatar(); second = new PlayerAvatar();
            second.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", invalid));
            second.ApplyDamage(new() { origin = first, damage = 10 });
            check(first.Hp == 100 && first.Hits == 0, "Invalid reflected input rejected: " + invalid);
        }

        var third = new PlayerAvatar();
        first = new PlayerAvatar(); second = new PlayerAvatar();
        second.OnHit = (_, _) =>
        {
            third.ApplyDamage(Return(second, "Ability_Thorns", 20));
            first.ApplyDamage(Return(third, "Ability_Thorns", 20));
            first.ApplyDamage(Return(second, "UnknownProc", 20));
            var wrongType = Return(second, "Weapon_Reflect", 20); wrongType.fromType = EDamageFromType.Magic;
            first.ApplyDamage(wrongType);
        };
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 100 && third.Hp == 100, "A third avatar, unknown proc, or wrong effect type cannot borrow reflection admission");

        var enemy = new UnitAvatar { faction = "enemy" };
        enemy.OnHit = (_, _) => first.ApplyDamage(Return(second, "Ability_Thorns", 20));
        second.OnHit = (_, _) => enemy.ApplyDamage(new() { origin = second, damage = 10 });
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 100, "An enemy-mediated nested proc cannot restart the allied reflection chain");
        enemy.OnHit = null;
        second.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 20));
        second.ApplyDamage(new() { origin = enemy, targetFactionLayers = -1, damage = 10 });
        check(enemy.Hp == 70, "Reflection against a normal enemy remains unscaled");

        foreach (bool off in new[] { true, false })
        {
            first = new PlayerAvatar { IsGuarding = true }; second = new PlayerAvatar();
            SessionSettings.FriendlyFireForHit = new(true, 50);
            second.OnHit = (_, d) =>
            {
                SessionSettings.FriendlyFireForHit = off ? default : new(true, 0);
                ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Weapon_Reflect", 20));
            };
            second.ApplyDamage(new() { origin = first, damage = 10 });
            check(first.Hp == 100 && first.Mp == 100 && first.GuardHits == 0,
                "Off/zero during the original hit blocks the return before guard costs: off=" + off);
            first.ApplyDamage(Return(second, "Weapon_Reflect", 20));
            check(first.GuardHits == 0, "Off/zero also rejects independent reflected team hits");
        }

        SessionSettings.FriendlyFireForHit = new(true, 50);
        var owner = new PlayerAvatar();
        var pet = new UnitAvatar { NetworkLeader = owner };
        second = new PlayerAvatar();
        second.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 20));
        second.ApplyDamage(new() { origin = pet, damage = 10 });
        check(pet.Hp == 90 && owner.Hp == 100, "Reflect back to the attacking companion, not its credited owner");
        pet.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(pet, "Ability_Thorns", 20));
        pet.ApplyDamage(new() { origin = owner, damage = 10 });
        check(owner.Hp == 100, "Companion reflection cannot damage its own owner");

        first = new PlayerAvatar { Name = "Attacker", Hp = 5 }; second = new PlayerAvatar { Name = "Reflector" };
        int notices = DungeonManager.Instance.Messages.Count;
        second.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(Return(second, "Ability_Thorns", 20));
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.IsDead && DungeonManager.Instance.Messages.Count == notices + 1 &&
            DungeonManager.Instance.Messages[^1].Contains("Reflector(") && DungeonManager.Instance.Messages[^1].Contains(" killed Attacker("), "Reflected lethal damage credits the reflecting player");

        first = new PlayerAvatar(); second = new PlayerAvatar();
        var reused = Return(second, "Ability_Thorns", 20);
        first.OnHit = (_, _) => throw new InvalidOperationException("return callback failed");
        second.OnHit = (_, _) => first.ApplyDamage(reused);
        try { second.ApplyDamage(new() { origin = first, damage = 10 }); } catch (InvalidOperationException) { }
        first.OnHit = null;
        second.ApplyDamage(new() { origin = first, damage = 10 });
        check(first.Hp == 80 && reused.damage == 20, "Reflection exception restores nested context and does not mutate pooled damage");
        enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(reused);
        check(enemy.Hp == 80, "Reused reflection damage does not carry allied scaling to an enemy");
    }
}
