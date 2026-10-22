using SephiriaOne;
using UnityEngine;

// Native GreenBat damage IDs verified against the installed serialized prefabs.
internal static class PlanetCadenceTests
{
    private static readonly string[] Planets = { "Blue", "Yellow", "Sky", "White", "Gray", "Black", "Red" };

    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        void Fresh(bool frequency = true, decimal scale = 100, float hp = 10000)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.Humans.Clear();
            CombatMatchRuntime.ResetDynamicZombieBalance();
            SessionSettings.ZombieDynamicBalance = false;
            SessionSettings.FriendlyFireForHit = new(true, scale, balance: true, hitFrequency: frequency);
            Time.time = Time.unscaledTime = 10; Mirror.NetworkServer.active = true;
            source = new(); target = new() { Hp = hp, MaxHp = hp };
        }
        DamageInstance Planet(string name, float amount = 10) => new()
        {
            origin = source, damage = amount, id = "Charm_Planet_" + name,
            fromType = EDamageFromType.None, damageType = EDamageType.Projectile
        };
        float Strike(DamageInstance hit)
        {
            float before = target.Hp; target.ApplyDamage(hit); return before - target.Hp;
        }

        Fresh();
        // One volley from every different Planet is not seven shots from one weapon.
        foreach (string name in Planets)
            check(Strike(Planet(name)) == 10, "Independent native Planet cadence: " + name);
        var blue = Planet("Blue");
        for (int i = 0; i < 3; i++) Strike(blue);
        check(Math.Abs(Strike(Planet("Blue")) - 10 * Math.Sqrt(4d / 5)) < .001,
            "Copies, enhanced bullets and recreated instances retain the same Planet-type counter");
        check(Strike(Planet("Red")) == 10, "Busy Blue Planet cannot penalize a slow Red Planet");

        Fresh();
        var incidental = new DamageInstance { origin = source, damage = 10, id = "Charm_PlitviceWaterDrop" };
        for (int i = 0; i < 40; i++) Strike(incidental);
        check(Strike(Planet("Blue")) == 10, "Other artifacts cannot inflate Planet cadence");
        for (int i = 0; i < 30; i++) Strike(Planet("Blue"));
        check(Math.Abs(Strike(incidental) - 10 * Math.Sqrt(4d / 41)) < .001,
            "Planet traffic cannot inflate the remaining incidental history");
        check(Math.Abs(Strike(Planet("Unknown")) - 10 * Math.Sqrt(4d / 42)) < .001,
            "Unknown Planet-like IDs stay in the bounded incidental fallback");
        var disguised = Planet("Red"); disguised.damageType = EDamageType.ElementalEffectDamage;
        check(Math.Abs(Strike(disguised) - 10 * Math.Sqrt(4d / 43)) < .001,
            "Status damage cannot borrow a Planet projectile counter");

        Fresh();
        incidental.origin = source;
        for (int i = 0; i < 4; i++) Strike(incidental);
        var companionPlanet = Planet("Blue"); companionPlanet.origin = new UnitAvatar { NetworkLeader = source };
        check(Math.Abs(Strike(companionPlanet) - 10 * Math.Sqrt(4d / 5)) < .001,
            "Companion-origin Planet-like damage remains incidental");
        float beforeProc = target.Hp;
        new Charm_FrostiumRing { NetworkAvatar = source, Target = target, Damage = Planet("Blue") }.HandleAttackUnit();
        check(Math.Abs(beforeProc - target.Hp - 10 * Math.Sqrt(4d / 6)) < .001,
            "Synchronous artifact proc cannot borrow a primary Planet history");
        check(Strike(Planet("Blue")) == 10, "Primary Planet history excludes companion and proc lookalikes");

        Fresh();
        blue = Planet("Blue");
        for (int i = 0; i < 20; i++) Strike(blue);
        blue.id = "Skill_FireArrow"; blue.fromType = EDamageFromType.Magic;
        check(Strike(blue) == 10, "Pooled projectile ID is classified from its current metadata");
        blue.id = "Charm_Planet_Blue"; blue.fromType = EDamageFromType.None;
        check(Math.Abs(Strike(blue) - 10 * Math.Sqrt(4d / 21)) < .001,
            "Changing pooled identity cannot reset the original Planet counter");

        // Every cadence history still pays into exactly one directed damage budget.
        Fresh(hp: 100);
        check(Strike(Planet("Blue", 100)) == 75, "Planet can spend the ordinary shared reserve");
        foreach (string name in Planets)
            check(Strike(Planet(name, 100)) == 1, "Another Planet gets no extra funded burst: " + name);
        check(Strike(new() { origin = source, damage = 100, fromType = EDamageFromType.Magic, id = "Skill_FireArrow" }) == 1,
            "Fire Arrow shares the same exhausted Planet allowance");
        Time.time += .1f;
        check(Math.Abs(Strike(Planet("Red", 100)) - 7) < .001, "Planet refill is shared, not multiplied by source count");

        // Existing lifecycle boundaries retire all per-source cadence, with no saved state.
        for (int reset = 0; reset < 7; reset++)
        {
            Fresh(); blue = Planet("Blue");
            for (int i = 0; i < 20; i++) Strike(blue);
            switch (reset)
            {
                case 0: FriendlyFireKda.ResetScores(); break;
                case 1: source.IsDead = true; source.Revive(100); break;
                case 2: target.currentFloorGuid = "next"; break;
                case 3: FriendlyFireParticipants.Depart(source); FriendlyFireParticipants.Rejoin(source); break;
                case 4: FriendlyFireParticipants.Select(source, target); break;
                case 5: SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: true,
                    rapid: new PvpRapidSettings(hitsPerSecond: 6)); break;
                case 6: FriendlyFireRuntime.Clear(); break;
            }
            check(Strike(blue) == 10, "Planet histories follow the existing lifecycle reset " + reset);
        }

        // Equal resolved inputs must have equal first-hit scaling, including Zombie->Human.
        foreach (decimal scale in new[] { .3m, .5m })
        foreach (bool frequency in new[] { false, true })
        {
            Fresh(frequency, scale);
            SessionSettings.ZombieDynamicBalance = true; SessionSettings.ZombieModifiers = default;
            CombatMatchRuntime.Zombies.Add(source); CombatMatchRuntime.Humans.Add(target);
            CombatMatchRuntime.DynamicZombieRatio(5, 1); CombatMatchRuntime.RoleSettingsChanged();
            Time.time = Time.unscaledTime = 40; // Let the existing dynamic-role curve settle.
            double incoming = CombatMatchRuntime.IncomingRoleScale(target);
            double expected = 5000 * (double)scale / 100 * incoming;
            foreach (var hit in new[] { Planet("Blue", 5000),
                new DamageInstance { origin = source, damage = 5000, id = "Charm_PlitviceWaterDrop", damageType = EDamageType.Projectile },
                new DamageInstance { origin = source, damage = 5000, id = "Skill_FireArrow", fromType = EDamageFromType.Magic } })
                check(Math.Abs(Strike(hit) - expected) < .002, "No duplicate scale or Zombie-only Planet penalty: " + hit.id);
        }

        Fresh(false);
        for (int i = 0; i < 50; i++) check(Strike(Planet("Blue")) == 10, "Frequency-off keeps native Planet cadence");
        Fresh();
        SessionSettings.FriendlyFireForHit = new(true, 100, hitFrequency: true);
        for (int i = 0; i < 20; i++) check(Strike(Planet("Blue")) == 10, "Balancing-off keeps native Planet damage");
        Fresh();
        var enemy = new UnitAvatar { faction = "enemy", Hp = 1000, MaxHp = 1000 };
        for (int i = 0; i < 20; i++) enemy.ApplyDamage(Planet("Blue"));
        check(enemy.Hp == 800, "PvE Planet hits retain native damage with PvP balancing enabled");
        Fresh();
        var packets = Planets.Select(name => Planet(name, 2)).ToArray();
        void Storm()
        {
            for (int i = 0; i < 2000; i++)
            { target.Hp = target.MaxHp; target.ApplyDamage(packets[i % packets.Length]); }
        }
        Storm(); long allocation = GC.GetAllocatedBytesForCurrentThread(); Storm();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        check(bytes == 0, "Warmed seven-Planet hits allocate nothing");
        Console.WriteLine($"Planet cadence: {bytes} bytes/2000 mixed Planet hits");
        SessionSettings.FriendlyFireForHit = default; SessionSettings.ZombieDynamicBalance = false;
        CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.Humans.Clear(); CombatMatchRuntime.RoleSettingsChanged();
        CombatMatchRuntime.ResetDynamicZombieBalance();
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
    }
}
