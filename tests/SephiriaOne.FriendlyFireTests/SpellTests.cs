using SephiriaOne;
using UnityEngine;

internal static class SpellTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var caster = new PlayerAvatar();
        var player = new PlayerAvatar { transform = { position = new(2, 0) } };
        var enemy = new UnitAvatar { faction = "enemy", transform = { position = new(1, 0) } };
        foreach (var identity in new (EDamageFromType Type, string Id, string Prefab)[]
        {
            (EDamageFromType.Magic, "Skill_FireBolt", null),
            (EDamageFromType.Magic, "Skill_AirBolt", null),
            (EDamageFromType.Magic, "Skill_EarthBolt", null),
            (EDamageFromType.Magic, "Skill_LightningBolt", null),
            (EDamageFromType.Magic, "Skill_IceBolt", null),
            (EDamageFromType.Magic, "Skill_FrostDagger", null),
            (EDamageFromType.Magic, "Skill_ArrowRain", null),
            (EDamageFromType.DirectAttack, "Weapon_SpecialAttack", "FuryMPBullet_Physical"),
            (EDamageFromType.DirectAttack, "Weapon_SpecialAttack", "FuryMPBullet_Fire"),
            (EDamageFromType.DirectAttack, "Weapon_SpecialAttack", "FuryMPBullet_Ice"),
            (EDamageFromType.DirectAttack, "Weapon_SpecialAttack", "FuryMPBullet_Lightning")
        })
        {
            string id = identity.Prefab ?? identity.Id;
            var bullet = new Bullet { FromType = identity.Type, damageId = identity.Id,
                spawnedPrefabName = identity.Prefab, NetworkOwner = caster,
                Candidates = new() { caster, player, enemy }, HomingTargetPos = new(2, 0) };
            bullet.Update();
            check(bullet.HomingTarget == player, id + " homes toward the hostile player closest to the aimed point");
            bullet.SetHomingTarget(null); bullet.HomingTargetPos = new(0, 0); bullet.Update();
            check(bullet.HomingTarget == enemy, id + " preserves native enemy ranking ahead of farther players");
            bullet.SetHomingTarget(null); bullet.Candidates.Remove(enemy); bullet.Update();
            check(bullet.HomingTarget == player, id + " acquires a nearby player without an enemy");
            foreach (var setting in new[] { default(FriendlyFireSettings), new FriendlyFireSettings(true, 0) })
            {
                SessionSettings.FriendlyFireForHit = setting; bullet.Update();
                check(bullet.HomingTarget == null, id + " clears live player target when off or zero");
                SessionSettings.FriendlyFireForHit = new(true, 50); bullet.Update();
                check(bullet.HomingTarget == player, id + " reacquires from current policy");
            }
            FriendlyFireParticipants.Select(caster); bullet.Update();
            check(bullet.HomingTarget == null, id + " clears a deselected target");
            FriendlyFireParticipants.Select(); bullet.Update();
            CombatMatchRuntime.PairRule = (_, _) => false; bullet.Update();
            check(bullet.HomingTarget == null, id + " respects countdowns and Tournament pair changes");
            CombatMatchRuntime.PairRule = null; bullet.Update();
            FriendlyFireParticipants.Depart(player); bullet.Update();
            check(bullet.HomingTarget == null, id + " drops disconnected target without borrowing an old connection");
            FriendlyFireParticipants.Clear();
            bullet.Update(); FriendlyFireParticipants.Depart(caster); bullet.Update();
            check(bullet.HomingTarget == null, id + " drops targets after its caster disconnects");
            FriendlyFireParticipants.Clear();
            bullet.SetHomingTarget(null); bullet.ignored.Add(player); bullet.Update();
            check(bullet.HomingTarget == null, id + " preserves ignored targets");
            bullet.ignored.Clear(); player.canBeTarget.Value = false; bullet.Update();
            check(bullet.HomingTarget == null, id + " preserves untargetability");
            player.canBeTarget.Value = true; player.IsDead = true; bullet.Update();
            check(bullet.HomingTarget == null, id + " preserves dead-target rejection");
            player.IsDead = false; bullet.homingDetectLimitRange = 1; bullet.Update();
            check(bullet.HomingTarget == null, id + " preserves homing range");
            bullet.homingDetectLimitRange = 2; bullet.Update();
            check(bullet.HomingTarget == player, id + " includes native range boundary");
            bullet.SetHomingTarget(null); bullet.autoDetectHomingTarget = false; bullet.Update();
            check(bullet.HomingTarget == null, id + " does not enable automatic acquisition");
            bullet.autoDetectHomingTarget = true; bullet.isHomingTargetEnabled = false; bullet.Update();
            check(bullet.HomingTarget == null, id + " does not grant missing homing ability");
            bullet.isHomingTargetEnabled = true; Mirror.NetworkServer.active = false; bullet.Update();
            check(bullet.HomingTarget == null, id + " does not modify guest-side targeting");
            Mirror.NetworkServer.active = true;
            bullet.Candidates.Remove(caster);
            bullet.NetworkOwner = new UnitAvatar { NetworkLeader = player }; bullet.Update();
            check(bullet.HomingTarget == null, id + " never homes onto the companion caster's owner");
            bullet.NetworkOwner = caster; bullet.FromType = EDamageFromType.None; bullet.Update();
            check(bullet.HomingTarget == null, id + " rejects a mismatched pooled damage type");
            bullet.FromType = identity.Type; bullet.damageId = "UnrelatedAttack"; bullet.Update();
            check(bullet.HomingTarget == null, id + " does not extend unaudited homing identities");
            if (identity.Prefab != null)
            {
                bullet.damageId = identity.Id;
                foreach (string other in new[] { null, "___", "OtherSpecialBullet", "FuryMPBullet_Fire_Unknown" })
                {
                    bullet.spawnedPrefabName = other; bullet.Update();
                    check(bullet.HomingTarget == null, id + " does not grant another special attack hostile homing: " + other);
                }
                bullet.spawnedPrefabName = identity.Prefab; bullet.Update();
                check(bullet.HomingTarget == player, id + " reads the current spawn identity without a stale eligibility cache");
                var damage = new DamageInstance { origin = caster, damage = 20, id = identity.Id,
                    fromType = identity.Type, damageType = EDamageType.Projectile,
                    targetFactionLayers = caster.GetHostileFactionLayers(EDamageFromType.None) };
                double hp = player.Hp;
                bullet.HomingTarget.ApplyDamage(damage);
                check(player.Hp == hp - 10 && damage.damage == 20, id + " uses existing friendly damage scaling exactly once");
            }
        }
        FriendlyFireParticipants.Clear();
        Selectors(check);
        WarmTargeting(check);
    }

    private static void WarmTargeting(Action<bool, string> check)
    {
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        var bullet = new Bullet { FromType = EDamageFromType.Magic, damageId = "Skill_FireBolt", NetworkOwner = source, Candidate = target };
        var liberation = new Bullet { FromType = EDamageFromType.DirectAttack, damageId = "Weapon_SpecialAttack",
            spawnedPrefabName = "FuryMPBullet_Physical", NetworkOwner = source, Candidate = target };
        var auto = new AutoMagicCaster { owner = source, Candidate = target, aimedEnemy = target };
        void Cycle()
        {
            target.IsDead = false; bullet.Update(); liberation.Update(); auto.SearchTarget(); auto.Update();
            target.IsDead = true; bullet.Update(); liberation.Update();
        }
        for (int i = 0; i < 100; i++) Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Cycle();
        check(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed spell/Liberation acquisition, loss and automatic caster checks allocate zero bytes");
        FriendlyFireParticipants.Clear();
    }

    private static void Selectors(Action<bool, string> check)
    {
        var source = new PlayerAvatar(); var player = new PlayerAvatar();
        var enemy = new UnitAvatar { faction = "enemy" };
        var volley = new ActiveSkill_FireBulletsToEnemies { NetworkCaster = source };
        var meteor = new ActiveSkill_MeteorShower { NetworkCaster = source };
        foreach (var spell in new ActiveSkill[] { volley, meteor })
        {
            bool Select()
            {
                var routine = spell == volley ? volley.FireCoroutine() : meteor.CreateMeteorCoroutine();
                while (routine.MoveNext()) { }
                return spell.Selected;
            }
            spell.Candidate = player;
            SessionSettings.FriendlyFireForHit = new(true, 50);
            check(Select(), spell.GetType().Name + " acquires a hostile player");
            SessionSettings.FriendlyFireForHit = default;
            check(!Select(), spell.GetType().Name + " obeys off");
            SessionSettings.FriendlyFireForHit = new(true, 50);
            CombatMatchRuntime.PairRule = (_, _) => false;
            check(!Select(), spell.GetType().Name + " obeys the current Tournament pair");
            CombatMatchRuntime.PairRule = null;
            spell.Candidate = enemy;
            check(Select(), spell.GetType().Name + " preserves native enemies");
        }
        var ball = new ActiveSkill_Ball { NetworkCaster = source };
        PlayerInputController.Candidates.Clear(); PlayerInputController.Candidates.Add(player);
        ball.Update();
        check(ball.Target == player, "Fire Circus scoped nearest search acquires players");
        check(PlayerInputController.SearchTargetNearestPoint(source, new(), 5) == null, "Fire Circus does not leak its scope to ordinary input aim");
        PlayerInputController.Candidates.Clear();
        var auto = new AutoMagicCaster { owner = source, Candidate = player };
        check(auto.SearchTarget() == player, "Automatic Bolt caster acquires players");
        auto.aimedEnemy = player; FriendlyFireParticipants.Select(source); auto.Update();
        check(!auto.aimedEnemy && !auto.Selected, "Automatic caster invalidates a cached player before its refresh timer");
        FriendlyFireParticipants.Select(); auto.aimedEnemy = enemy; auto.Update();
        check(auto.aimedEnemy == enemy && auto.Selected, "Automatic caster preserves native cached enemy");
        var dungeon = new DungeonManager { Candidate = player };
        var hit = new List<UnitAvatar>();
        check(dungeon.SearchTargetForChainLightning(hit, source, new(), enemy) == player, "Chain lightning acquires another player");
        hit.Add(player);
        check(dungeon.SearchTargetForChainLightning(hit, source, new(), enemy) == null, "Chain lightning preserves already-hit exclusion");
        hit.Clear();
        check(dungeon.SearchTargetForChainLightning(hit, source, new(), player) == null, "Chain lightning excludes its original victim");
        SessionSettings.FriendlyFireForHit = default;
        check(dungeon.SearchTargetForChainLightning(hit, source, new(), enemy) == null, "Chain lightning rechecks hostility for each hop");
        foreach (double percent in new[] { 0.1, 50.0, 200.0 })
        {
            SessionSettings.FriendlyFireForHit = new(true, (decimal)percent);
            player = new PlayerAvatar(); dungeon.Candidate = player;
            var selected = dungeon.SearchTargetForChainLightning(hit, source, new(), enemy);
            var damage = new DamageInstance { origin = source, damage = 20, id = "Skill_LightningBolt", fromType = EDamageFromType.Magic,
                damageType = EDamageType.Projectile, targetFactionLayers = source.GetHostileFactionLayers(EDamageFromType.None) };
            selected.ApplyDamage(damage);
            check(Math.Abs(player.Hp - (100 - 20 * percent / 100)) < 0.0001 && damage.damage == 20,
                "Magic chain impact uses native damage admission and scales once at " + percent + "%");
        }
        FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 50);
        volley.Candidate = null;
        volley.Collider = new() { Hitbox = new() { Target = player } };
        var fire = volley.FireCoroutine(); while (fire.MoveNext()) { }
        check(volley.Selected, "Spell overlap resolves PlayerAvatar through its costume hitbox");
        Mirror.NetworkServer.active = false;
        fire = volley.FireCoroutine(); while (fire.MoveNext()) { }
        check(!volley.Selected, "Guest collider resolution stays native");
        Mirror.NetworkServer.active = true;
        volley.Collider.Hitbox.Target = enemy;
        fire = volley.FireCoroutine(); while (fire.MoveNext()) { }
        check(!volley.Selected, "Scoped player hitbox fallback does not widen unrelated NPC lookup");
        volley.Collider.Avatar = enemy;
        fire = volley.FireCoroutine(); while (fire.MoveNext()) { }
        check(volley.Selected, "Direct native avatar component takes precedence over fallback");
    }
}
