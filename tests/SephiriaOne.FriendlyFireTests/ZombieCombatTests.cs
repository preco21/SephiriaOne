using SephiriaOne;
using UnityEngine;

internal static class ZombieCombatTests
{
    internal static void Run(Action<bool, string> check)
    {
        var players = new[] { new PlayerAvatar(), new PlayerAvatar(), new PlayerAvatar() };
        var zombie = players[0]; var human = players[1]; var other = players[2];
        var round = new ZombieSurvivalRound(3, 0);
        DamageInstance Hit(UnitAvatar source, float damage = 20) => new() { origin = source, damage = damage, targetFactionLayers = -1 };
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 100);
        SessionSettings.ZombieModifiers = default; CombatMatchRuntime.Chaser = null;
        CombatMatchRuntime.Humans.Add(human); CombatMatchRuntime.Humans.Add(other);
        CombatMatchRuntime.Zombies.Add(zombie); CombatMatchRuntime.RoleSettingsChanged();
        CombatMatchRuntime.PairRule = (a, b) => Array.IndexOf(players, a) is int x && x >= 0 && Array.IndexOf(players, b) is int y && y >= 0 && round.AllowsPair(x, y);
        try
        {
            other.IsGuarding = true;
            check(other.ApplyDamage(Hit(human)) == EApplyDamageResult.Fail_Absolute && other.Mp == 100 && other.GuardHits == 0,
                "Zombie mode blocks same-team hits before guard costs");
            human.ApplyDamage(Hit(zombie)); zombie.ApplyDamage(Hit(human));
            check(human.Hp == 80 && zombie.Hp == 85, "Human damage is native scale; Zombie incoming is 75 percent");
            var helper = new UnitAvatar { NetworkLeader = human };
            check(new UnitAI_NewBasic { Avatar = helper, CurrentTarget = zombie }.ShouldAttack &&
                !new UnitAI_NewBasic { Avatar = helper, CurrentTarget = other }.ShouldAttack, "Companions share owner's live Zombie-team hostility");
            var burn = new CharacterDebuff(); other.ApplyDebuff(burn, human);
            check(burn.NetworkTarget == null && FriendlyFireRuntime.ItemTarget(zombie, EDamageFromType.Magic, human) &&
                !FriendlyFireRuntime.ItemTarget(other, EDamageFromType.Magic, human), "Debuff and artifact filters share team policy");
            var bolt = new Bullet { NetworkOwner = human, FromType = EDamageFromType.Magic, damageId = "Skill_FireBolt" };
            check(FriendlyFireRuntime.HomingTargetAllowed(human, bolt.FromType, zombie, bolt) &&
                !FriendlyFireRuntime.HomingTargetAllowed(human, bolt.FromType, other, bolt), "Bolt homing only acquires opposing teams");
            round.Infect(1);
            check(!FriendlyFireRuntime.ItemTarget(zombie, EDamageFromType.Magic, human) &&
                FriendlyFireRuntime.HomingTargetLost(zombie, bolt), "Conversion revokes old homing and targeting immediately");
            round.Revived(1); CombatMatchRuntime.Zombies.Add(human); CombatMatchRuntime.Humans.Remove(human); CombatMatchRuntime.RoleSettingsChanged();
            check(!FriendlyFireRuntime.ItemTarget(zombie, EDamageFromType.Magic, human) &&
                FriendlyFireRuntime.ItemTarget(other, EDamageFromType.Magic, human), "Revived Zombie acquires its former Human teammates");
            other.IsGuarding = false;
            other.OnHit = (_, _) => human.ApplyDamage(new() { origin = other, damage = 20, id = "Ability_Thorns" });
            other.ApplyDamage(Hit(human));
            check(human.Hp == 65 && other.Hp == 80, "Reflection uses opposite teams and Zombie incoming modifier");
            other.OnHit = null;
            var enemy = new UnitAvatar { faction = "enemy" };
            human.ApplyDamage(Hit(enemy)); enemy.ApplyDamage(Hit(human));
            check(human.Hp == 45 && enemy.Hp == 80, "Zombie modifiers preserve incoming and outgoing PvE");

            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, refillPercent: 10);
            other.Hp = 100; Time.time = 0;
            other.ApplyDamage(Hit(human, float.MaxValue)); Time.time = .1f; other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(other.Hp - 78) < .001, "Zombie x2 multiplies global DPS refill, preserving shared initial burst");
            SessionSettings.ZombieModifiers = SessionSettings.ZombieModifiers.WithRefill(enabled: false); CombatMatchRuntime.RoleSettingsChanged();
            Time.time = .2f; other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(other.Hp - 77) < .001, "Disabling Zombie refill preserves spent budget and restores native rate immediately");

            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            SessionSettings.ZombieModifiers = default; SessionSettings.ZombieDynamicBalance = true;
            Time.time = Time.unscaledTime = 0; CombatMatchRuntime.DynamicZombieRatio(1, 5); CombatMatchRuntime.RoleSettingsChanged();
            other.Hp = 100;
            other.ApplyDamage(Hit(human, float.MaxValue));
            Time.time = Time.unscaledTime = .1f;
            other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(other.Hp - (80 - 37d / 27)) < .001, "Modest dynamic refill shares ordinary burst and damage-budget ledger");
            float hpBefore = other.Hp;
            CombatMatchRuntime.DynamicZombieRatio(2, 4); CombatMatchRuntime.RoleSettingsChanged();
            other.ApplyDamage(Hit(human, float.MaxValue));
            check(other.Hp == hpBefore - 1, "Conversion retains only the floor without refilling spent budget");
            hpBefore = other.Hp;
            Time.time = Time.unscaledTime = .2f; other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(other.Hp - (hpBefore - CombatMatchRuntime.RefillRoleScale(human))) < .001,
                "Observed conversion retains tokens and smoothly updates refill rate");
            hpBefore = other.Hp;
            Time.time = Time.unscaledTime = .3f;
            double scale = CombatMatchRuntime.RefillRoleScale(human);
            other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(other.Hp - (hpBefore - scale)) < .001, "Following hits use smooth refill without repeated role invalidation");
            SessionSettings.FriendlyFireForHit = new(true, 100);
            human.Hp = 100; human.ApplyDamage(Hit(other));
            check(Math.Abs(human.Hp - (100 - 20 * CombatMatchRuntime.IncomingRoleScale(human))) < .001,
                "Dynamic incoming scale follows existing opposite-team damage path");
            human.Hp = 100; enemy.Hp = 100;
            human.ApplyDamage(Hit(enemy)); enemy.ApplyDamage(Hit(human));
            check(human.Hp == 80 && enemy.Hp == 80, "Dynamic policy cannot change PvE in either direction");

            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            Time.time = Time.unscaledTime = 10;
            CombatMatchRuntime.DynamicZombieRatio(5, 1);
            Time.time = Time.unscaledTime = 40; // Settle composition; no extra budget or match.
            human.Hp = other.Hp = 100;
            human.ApplyDamage(Hit(other)); other.ApplyDamage(Hit(human));
            check(Math.Abs(human.Hp - (100 - 20 * 13d / 9)) < .001 &&
                Math.Abs(other.Hp - (100 - 20 * 19d / 27)) < .001,
                "Human minority deals stronger direct hits and takes reduced damage independently of balancing");
            float priorHumanHp = other.Hp;
            other.ApplyDamage(Hit(enemy));
            check(other.Hp == priorHumanHp - 20, "Minority-Human protection never applies to incoming PvE");
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, refillPercent: 10);
            human.Hp = other.Hp = 100;
            human.ApplyDamage(Hit(other, float.MaxValue)); other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(human.Hp - (100 - 20 * 13d / 9)) < .001 &&
                Math.Abs(other.Hp - (100 - 20 * 19d / 27)) < .001,
                "Both minority modifiers compose with the existing burst budget");
            priorHumanHp = other.Hp;
            float priorZombieHp = human.Hp;
            human.ApplyDamage(Hit(other, float.MaxValue)); other.ApplyDamage(Hit(human, float.MaxValue));
            check(human.Hp == priorZombieHp - 1 && other.Hp == priorHumanHp - 1,
                "Exhausted budgets retain only the final minimum for both dynamic teams");
            Time.time = Time.unscaledTime = 40.5f;
            priorHumanHp = other.Hp; priorZombieHp = human.Hp;
            human.ApplyDamage(Hit(other, float.MaxValue)); other.ApplyDamage(Hit(human, float.MaxValue));
            check(Math.Abs(human.Hp - (priorZombieHp - 5 * 13d / 9)) < .001 &&
                Math.Abs(other.Hp - (priorHumanHp - 5 * 19d / 27 * 19d / 27)) < .001,
                "Vulnerability/refunds preserve funded units and Human defense combines with lower Zombie refill");
        }
        finally
        {
            CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.Humans.Clear(); CombatMatchRuntime.PairRule = null; CombatMatchRuntime.RoleSettingsChanged();
            SessionSettings.ZombieModifiers = default; SessionSettings.ZombieDynamicBalance = false; SessionSettings.FriendlyFireForHit = default;
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
        }
    }
}
