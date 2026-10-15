using SephiriaOne;

internal static class PlayTagAttributionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var completed = new List<(PlayerAvatar Killer, PlayerAvatar Victim)>();
        CombatMatchRuntime.KillCompleted = (killer, victim, token) =>
        {
            check(FriendlyFireParticipants.IsCurrent(killer) && FriendlyFireParticipants.IsCurrent(victim),
                "Completed Tag kill delivers current exact participant receipts");
            check(victim.Player.IsDead && FriendlyFireKda.For(killer.Player).Kills > 0 && token == CombatMatchRuntime.MatchToken,
                "Tag kill boundary runs after confirmed death and KDA completion with captured match identity");
            completed.Add((killer.Player, victim.Player));
        };
        void Reset()
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100);
            completed.Clear();
        }
        try
        {
            foreach (string source in new[] { "direct", "companion", "reflection", "burn", "electric" })
            {
                Reset(); var killer = new PlayerAvatar(); var victim = new PlayerAvatar { Hp = 1 };
                switch (source)
                {
                    case "direct": victim.ApplyDamage(new() { origin = killer, damage = 20 }); break;
                    case "companion": victim.ApplyDamage(new() { origin = new UnitAvatar { NetworkLeader = killer }, damage = 20 }); break;
                    case "reflection":
                        killer.OnHit = (_, d) => ((UnitAvatar)d.origin).ApplyDamage(new() { origin = killer, damage = 20,
                            id = "Ability_Thorns", fromType = EDamageFromType.None });
                        killer.ApplyDamage(new() { origin = victim, damage = 10 });
                        break;
                    case "burn":
                        var burn = new CharacterDebuff_Burn(); burn.InitializeAndSpawn(killer, victim);
                        burn.OnTick = burn.OnUpdate_Server; burn.Step(0.5f);
                        break;
                    case "electric":
                        victim.ApplyDebuff(new() { OnApply = (caster, target) => target.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, killer);
                        break;
                }
                check(completed.SequenceEqual(new[] { (killer, victim) }), "One completed Tag kill crosses real damage hooks: " + source);
                victim.Die(5, new() { origin = killer });
                check(completed.Count == 1, "Duplicate native death never repeats Tag transfer admission: " + source);
            }
            foreach (string invalid in new[] { "extra-life", "environment", "exception", "reset", "disconnect", "resurrection" })
            {
                Reset(); var killer = new PlayerAvatar(); var victim = new PlayerAvatar();
                switch (invalid)
                {
                    case "extra-life": victim.ExtraLife = true; break;
                    case "exception": victim.OnDeath = _ => throw new InvalidOperationException("native failure"); break;
                    case "reset": victim.OnDeath = _ => FriendlyFireKda.ResetScores(); break;
                    case "disconnect": victim.OnDeath = _ => FriendlyFireParticipants.Depart(killer); break;
                    case "resurrection": victim.OnDeath = _ => victim.Revive(100); break;
                }
                try
                {
                    if (invalid == "environment") victim.Die(5, new() { origin = new UnitAvatar { faction = "enemy" } });
                    else victim.ApplyDamage(new() { origin = killer, damage = 200 });
                }
                catch (InvalidOperationException) { }
                check(completed.Count == 0, "Invalid completed-death path never admits Tag transfer: " + invalid);
            }
            Reset();
            var a = new PlayerAvatar(); var b = new PlayerAvatar { Hp = 1 }; var c = new PlayerAvatar { Hp = 1 };
            b.OnDeath = _ => c.ApplyDamage(new() { origin = a, damage = 20, id = "Charm_BurnExplosion", damageType = EDamageType.Projectile });
            a.ApplyDamage(new() { origin = c, damage = 10 });
            long epoch = FriendlyFireKda.Epoch;
            b.ApplyDamage(new() { origin = a, damage = 20 });
            check(completed.SequenceEqual(new[] { (a, c), (a, b) }) && FriendlyFireKda.Epoch == epoch,
                "Nested lethal effect preserves outer completed-death receipt through shared transfer notification");
        }
        finally { CombatMatchRuntime.KillCompleted = null; Reset(); }
    }
}
