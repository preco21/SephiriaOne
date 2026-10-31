using SephiriaOne;
using UnityEngine;

internal static class ArtifactRangeTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear(); Time.time = 100;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, artifactRate: new(true));
        DamageInstance Hit(UnitAvatar owner, string id) => new() { origin = owner, id = id, damage = 2,
            fromType = EDamageFromType.None, damageType = EDamageType.Projectile, targetFactionLayers = -1 };
        string[] ids = { "Charm_Guillotine", "Charm_ElectricEarring", "Charm_Meteor", "Charm_FireFeather",
            "Charm_Chakram", "Charm_EchoOfTheGlacier", "Charm_IceBat", "Ability_DarkCloud" };
        foreach (string id in ids)
        {
            var attacker = new PlayerAvatar();
            var victim = new PlayerAvatar { transform = { position = new(5.01f, 0) }, IsGuarding = true };
            var hit = Hit(attacker, id); int mp = victim.Mp;
            check(victim.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && victim.Hp == 100 && victim.Mp == mp && victim.Hits == 0,
                id + " rejects outside owner radius before guard MP, callbacks and cooldown reservation");
            victim.IsGuarding = false; victim.transform.position = new(3, 4);
            victim.ApplyDamage(hit);
            check(victim.Hp == 98, id + " includes exact 5 m boundary without spending cadence on rejected hits");
        }
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        var owner = new PlayerAvatar { IsInBattle = true };
        var target = new PlayerAvatar { transform = { position = new(6, 0) } };
        var feather = new Charm_FireFeather { NetworkAvatar = owner, Target = target };
        var meteor = new Charm_FlameGround_Meteor { NetworkAvatar = owner, Target = target };
        var earring = new Charm_ElectricEarring { NetworkAvatar = owner, Target = target };
        var echo = new Charm_EchoOfTheGlacier { NetworkAvatar = owner, Target = target, ApplyAutonomousStatus = true };
        var guillotine = new Charm_Guillotine { NetworkAvatar = owner, Target = target };
        var bat = new Charm_IceBat { NetworkAvatar = owner, Target = target };
        var chakram = new Charm_FireChakram { NetworkAvatar = owner, Target = target, ApplyAutonomousStatus = true };
        var cloud = new ComboEffect_DarkCloud { NetworkAvatar = owner, Target = target, Damage = Hit(owner, "Ability_DarkCloud") };
        cloud.OnAttackUnit += t => t.ApplyDebuff(new(), owner);
        void Select(bool expected)
        {
            check(feather.SearchTarget() == expected && meteor.SearchTarget() == expected && earring.SearchTarget() == expected &&
                echo.SearchTarget() == expected && guillotine.FindTargetsInRange() == expected, "Audited native selectors share range");
            bat.OnUpdate(); chakram.OnUpdate(); var routine = cloud.UseCloudCoroutine(); while (routine.MoveNext()) { }
            check(bat.Selected == expected && chakram.Selected == expected && cloud.Selected == expected, "Bat, Chakram and Cloud selector paths share range");
        }
        Select(false);
        cloud.FireLightning(target);
        check(target.Hp == 100 && target.DebuffApplications == 0 && cloud.Strikes == 0 && cloud.Effects == 0,
            "Out-of-range Dark Cloud skips whole strike including burn callback and FX");
        owner.transform.position = new(1, 0); Select(true);
        int cloudDebuffsBefore = target.DebuffApplications; cloud.FireLightning(target);
        check(target.Hp == 98 && cloud.Effects == 1 && target.DebuffApplications == cloudDebuffsBefore + 1, "Moving owner within range immediately admits cloud without stale cooldown");
        owner.transform.position = new(0, 0);
        var homing = new Bullet { NetworkOwner = owner, Candidate = target, damageId = "Charm_FireFeather", FromType = EDamageFromType.None };
        check(!FriendlyFireRuntime.HomingTargetAllowed(owner, EDamageFromType.None, target, homing), "Feather cannot acquire distant player");
        owner.transform.position = new(1, 0);
        check(FriendlyFireRuntime.HomingTargetAllowed(owner, EDamageFromType.None, target, homing), "Feather acquires player within owner radius");
        owner.transform.position = new(0, 0);
        check(FriendlyFireRuntime.HomingTargetLost(target, homing), "Cached homing drops player leaving owner radius");
        homing.damageId = "Charm_IceBow";
        check(FriendlyFireRuntime.HomingTargetAllowed(owner, EDamageFromType.None, target, homing) && !FriendlyFireRuntime.HomingTargetLost(target, homing),
            "Excluded Ice Weapons retain existing homing radius");
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, artifactRangeMeters: 7m));
        Select(true);
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, artifactRangeMeters: 0.5m));
        Select(false);
        target.transform.position = new(0.5f, 0); Select(true);
        target.transform.position = new(6, 0);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false);
        Select(true);
        target.ApplyDamage(Hit(owner, ids[0])); check(target.Hp < 98, "Balancing off immediately restores native artifact range");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        var enemy = new UnitAvatar { faction = "enemy", transform = { position = new(100, 0) } };
        check(FriendlyFireRuntime.AutonomousArtifactTarget(enemy, EDamageFromType.None, owner), "Distant native enemy remains targetable");
        enemy.ApplyDamage(Hit(owner, ids[0])); check(enemy.Hp == 98, "Outgoing PvE is unchanged");
        target.Hp = 100; target.ApplyDamage(Hit(enemy, ids[0])); check(target.Hp == 98, "Incoming PvE is unchanged");
        foreach (string id in new[] { "Weapon_Liberation", "Skill_FireBolt", "Charm_PallasCard", "Charm_PlitviceWaterDrop", "Ability_Planet", "Charm_RedDew" })
        {
            var victim = new PlayerAvatar { transform = { position = new(20, 0) } };
            victim.ApplyDamage(Hit(owner, id)); check(victim.Hp < 100, "Excluded source keeps native range: " + id);
        }
        var companion = new UnitAvatar { NetworkLeader = owner };
        target.Hp = 100; target.ApplyDamage(Hit(companion, ids[0])); check(target.Hp < 100, "Companions excluded even with matching artifact ID");
        foreach (var from in new[] { EDamageFromType.BasicAttack, EDamageFromType.DirectAttack, EDamageFromType.Magic })
        {
            var damage = Hit(owner, ids[0]); damage.fromType = from; target.Hp = 100;
            target.ApplyDamage(damage); check(target.Hp < 100, "Direct weapon and magic tags are excluded");
        }
        var direct = new Charm_IceHammer { NetworkAvatar = owner, Target = target };
        check(direct.SearchTarget() && new Charm_GrowthParry { NetworkAvatar = owner, Target = target }.SearchNearestTarget(),
            "Armament and retaliation selectors keep native range");
        FriendlyFireParticipants.Select(owner);
        target.transform.position = new(1, 0); check(!guillotine.FindTargetsInRange(), "Range does not bypass participant selection");
        FriendlyFireParticipants.Select(owner, target); check(guillotine.FindTargetsInRange(), "Selected pairs retain normal eligibility");
        var before = Hit(owner, ids[0]); target.OnCalculateDamage = _ => owner.transform.position = new(-10, 0);
        target.Hp = 100; target.ApplyDamage(before); target.OnCalculateDamage = null;
        check(target.Hp == 100, "Movement in native callbacks cannot bypass the final hit recheck");
        owner.transform.position = new(0, 0); target.transform.position = new(1, 0); Time.time += 10;
        target.OnCalculateDamage = _ => owner.transform.position = new(-10, 0);
        int debuffs = target.DebuffApplications;
        cloud.FireLightning(target); target.OnCalculateDamage = null;
        check(target.DebuffApplications == debuffs, "Cloud callback cannot apply a fresh debuff after the owner leaves range during impact");
        owner.transform.position = new(0, 0); target.transform.position = new(6, 0);
        debuffs = target.DebuffApplications;
        feather.OnAttackFeather(); meteor.HandleAttack(); earring.HandleAttack();
        echo.Damage = Hit(owner, "Charm_EchoOfTheGlacier"); echo.CreateFrostbite();
        check(target.DebuffApplications == debuffs, "Cached impacts cannot apply fresh Feather/Meteor/Earring/Echo statuses outside range");
        target.transform.position = new(1, 0);
        target.OnCalculateDamage = _ => owner.transform.position = new(-10, 0);
        echo.CreateFrostbite(); target.OnCalculateDamage = null;
        check(target.DebuffApplications == debuffs, "Echo rechecks status admission after damage callback moves the owner");
        feather.Target = enemy; feather.OnAttackFeather();
        check(enemy.DebuffApplications == 1, "Native PvE artifact debuffs remain unrestricted");
        owner.transform.position = new(0, 0); target.transform.position = new(1, 0);
        earring.CachedTargets.Add(target);
        var volley = earring.AttackCoroutine(3);
        check(volley.MoveNext() && earring.Emissions == 1, "Initial in-range Earring volley emits normally");
        target.transform.position = new(6, 0);
        check(!volley.MoveNext() && earring.Emissions == 1, "Earring prunes cached target before next delayed emission");
        earring.CachedTargets.Add(enemy); volley = earring.AttackCoroutine(1);
        check(volley.MoveNext() && earring.Emissions == 2, "Mixed cached targets preserve distant native enemies");
        guillotine.RpcCreateGuillotine(target, target.transform.position, 2, false);
        check(guillotine.Emissions == 0, "Cached Guillotine target outside range emits no RPC or new blade");
        owner.transform.position = new(1, 0); guillotine.RpcCreateGuillotine(target, target.transform.position, 2, true);
        guillotine.RpcCreateGuillotine(enemy, enemy.transform.position, 2, false);
        check(guillotine.Emissions == 2, "Guillotine includes boundary/fire variant and retains native enemy emissions");
        owner.transform.position = new(0, 0); target.transform.position = new(6, 0);
        for (int i = 0; i < 100; i++) { guillotine.FindTargetsInRange(); target.ApplyDamage(before); }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { guillotine.FindTargetsInRange(); target.ApplyDamage(before); }
        check(GC.GetAllocatedBytesForCurrentThread() == bytes, "Range filtering adds no warmed selector/impact allocations");
        Console.WriteLine("Autonomous artifact range: 0 bytes/1000 selector and impact checks");
        FriendlyFireRuntime.Clear(); SessionSettings.FriendlyFireForHit = default;
        FriendlyFireParticipants.Clear();
    }
}
