using System.Collections;
using SephiriaOne;
using UnityEngine;

internal static class ArtifactTests
{
    private static void Finish(IEnumerator iterator) { while (iterator.MoveNext()) { } }
    private static DamageInstance Hit(UnitAvatar source, float amount = 20) => new()
    { origin = source, damage = amount, targetFactionLayers = -1 };

    internal static void Run(Action<bool, string> check)
    {
        Selectors(check);
        CombatGates(check);
        Homing(check);
        Nearest(check);
        Procs(check);
        WarmAllocations(check);
        PlayerSpawner.MultiplayerList.Clear();
        PlayerInputController.Candidates.Clear();
        CombatManager.Instance.PeaceMode = false;
        Mirror.NetworkServer.active = true;
    }

    private static void WarmAllocations(Action<bool, string> check)
    {
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var source = new PlayerAvatar(); var target = new PlayerAvatar { transform = { position = new(1, 0) } };
        PlayerSpawner.MultiplayerList.Clear();
        PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = source });
        PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = target });
        var cloud = new ComboEffect_DarkCloud { NetworkAvatar = source };
        for (int i = 0; i < 100; i++) cloud.Update();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) cloud.Update();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated && cloud.Activated && !source.IsInBattle,
            "Warm artifact battle checks with two registered players allocate no per-tick memory");

        PlayerInputController.Candidates.Clear();
        PlayerInputController.Candidates.AddRange(new UnitAvatar[] { source, target });
        var guard = new Charm_GuardCounter { NetworkAvatar = source };
        for (int i = 0; i < 100; i++) guard.FireRipostelaser();
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) guard.FireRipostelaser();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated && guard.Target == target,
            "Warm scoped artifact nearest searches allocate no per-search memory");

        var bullet = new Bullet { NetworkOwner = source, Candidate = target, damageId = "Charm_IceBow" };
        void HomingCycle()
        {
            target.IsDead = false; bullet.Candidate = target; bullet.Update();
            target.IsDead = true; bullet.Candidate = null; bullet.Update();
        }
        for (int i = 0; i < 100; i++) HomingCycle();
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) HomingCycle();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated && bullet.HomingTarget == null,
            "Warm artifact homing acquisition and target loss allocate no per-cycle memory");
    }

    private static void Selectors(Action<bool, string> check)
    {
        var source = new PlayerAvatar { IsInBattle = true };
        var target = new PlayerAvatar();
        var iceBat = new Charm_IceBat();
        var earring = new Charm_ElectricEarring();
        var chim = new Charm_AttackChim();
        var glacier = new Charm_EchoOfTheGlacier();
        var parry = new Charm_GrowthParry();
        var guillotine = new Charm_Guillotine();
        var hammer = new Charm_IceHammer();
        var spear = new Charm_IceSpear();
        var greenBat = new GreenBat();
        var selectors = new (string Name, Charm_Basic Item, Func<bool> Select)[]
        {
            ("Ice Bat", iceBat, () => { iceBat.OnUpdate(); return iceBat.Selected; }),
            ("Electric Earring", earring, earring.SearchTarget),
            ("Attack Chim", chim, chim.SearchTarget),
            ("Echo of the Glacier", glacier, glacier.SearchTarget),
            ("Growth Parry", parry, parry.SearchNearestTarget),
            ("Guillotine", guillotine, guillotine.FindTargetsInRange),
            ("Ice Hammer", hammer, hammer.SearchTarget),
            ("Ice Spear", spear, spear.SearchTarget),
            ("Ice Spear direction", spear, spear.SearchTargetByWeaponDirection),
            ("Green Bat", greenBat, greenBat.SearchTarget)
        };
        foreach (var entry in selectors)
        {
            entry.Item.NetworkAvatar = source; entry.Item.Target = target;
            SessionSettings.FriendlyFireForHit = new(true, 50);
            check(entry.Select(), entry.Name + " selects another hostile player");
            foreach (var policy in new[] { default(FriendlyFireSettings), new FriendlyFireSettings(true, 0) })
            {
                SessionSettings.FriendlyFireForHit = policy;
                check(!entry.Select(), entry.Name + " observes off/zero on the existing artifact");
            }
            SessionSettings.FriendlyFireForHit = new(true, 50);
            Mirror.NetworkServer.active = false;
            check(!entry.Select(), entry.Name + " does not extend guest targeting");
            Mirror.NetworkServer.active = true;
            entry.Item.Target = source;
            check(!entry.Select(), entry.Name + " retains self exclusion");
            entry.Item.NetworkAvatar = new UnitAvatar { NetworkLeader = source, IsInBattle = true };
            check(!entry.Select(), entry.Name + " excludes companion owner");
            entry.Item.Target = target;
            check(entry.Select(), entry.Name + " admits another player for player-owned offense");
            entry.Item.NetworkAvatar = source;
            entry.Item.Target = new UnitAvatar { faction = "enemy" };
            SessionSettings.FriendlyFireForHit = default;
            check(entry.Select(), entry.Name + " preserves native enemy targeting while off");
            entry.Item.Target.IsDead = true;
            check(!entry.Select(), entry.Name + " preserves native dead-target rejection");
        }
        SessionSettings.FriendlyFireForHit = new(true, 50);
        iceBat.NetworkAvatar = source; iceBat.Target = target; iceBat.HasOwnedFrostbite = false;
        iceBat.OnUpdate();
        check(!iceBat.Selected, "Ice Bat still requires frostbite owned by its caster");
        iceBat.HasOwnedFrostbite = true; iceBat.OnUpdate();
        check(iceBat.Selected, "Ice Bat needs no magic book when owned frostbite is present");

        var dagger = new DaggerGrowthBullet { NetworkOwner = source, Target = target };
        dagger.HitCheck(); check(dagger.Selected, "Growth dagger forward faction filter admits another player");
        SessionSettings.FriendlyFireForHit = default;
        dagger.HitCheck(); check(!dagger.Selected, "Growth dagger observes off on existing projectile");
        dagger.Target = new UnitAvatar { faction = "enemy" };
        dagger.HitCheck(); check(dagger.Selected, "Growth dagger preserves native enemy hit checks");
    }

    private static void CombatGates(Action<bool, string> check)
    {
        var source = new PlayerAvatar();
        var target = new PlayerAvatar { transform = { position = new(10, 0) } };
        var spawner = new PlayerSpawner { PlayerAvatar = target };
        PlayerSpawner.MultiplayerList.Clear();
        PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = source });
        PlayerSpawner.MultiplayerList.Add(spawner);
        var iceBat = new Charm_IceBat();
        var earring = new Charm_ElectricEarring();
        var meteor = new Charm_FlameGround_Meteor();
        var spear = new Charm_IceSpear();
        var cloud = new ComboEffect_DarkCloud();
        var greenBat = new GreenBat();
        var gates = new (string Name, Charm_Basic Item, Func<bool> Tick)[]
        {
            ("Ice Bat", iceBat, () => { iceBat.OnUpdate(); return iceBat.Selected; }),
            ("Electric Earring", earring, () => { earring.OnUpdate(); return earring.Activated; }),
            ("Meteor", meteor, () => { meteor.OnUpdate(); return meteor.Activated; }),
            ("Ice Spear", spear, () => { spear.OnUpdate(); return spear.Activated; }),
            ("Dark Cloud", cloud, () => { cloud.Update(); return cloud.Activated; }),
            ("Green Bat", greenBat, () => { greenBat.Update(); return greenBat.Activated; })
        };
        foreach (var gate in gates)
        {
            gate.Item.NetworkAvatar = source; gate.Item.Target = target;
            SessionSettings.FriendlyFireForHit = new(true, 50);
            check(gate.Tick(), gate.Name + " activates at the inclusive 10-unit player boundary");
            check(!source.IsInBattle, gate.Name + " leaves global battle state unchanged");
            target.transform.position = new(10.01f, 0);
            check(!gate.Tick(), gate.Name + " does not wake for distant players");
            target.transform.position = new(10, 0);
            target.currentFloorGuid = "other-floor";
            check(!gate.Tick(), gate.Name + " does not wake for players on another floor");
            target.currentFloorGuid = source.currentFloorGuid;
            target.canBeTarget.Value = false;
            check(!gate.Tick(), gate.Name + " requires a targetable nearby player");
            target.canBeTarget.Value = true; target.gameObject.activeSelf = false;
            check(!gate.Tick(), gate.Name + " requires an active nearby player");
            target.gameObject.activeSelf = true; target.IsDead = true;
            check(!gate.Tick(), gate.Name + " ignores dead nearby players");
            target.IsDead = false;
            gate.Item.Ready = false;
            check(!gate.Tick(), gate.Name + " preserves the native cooldown prerequisite");
            gate.Item.Ready = true; CombatManager.Instance.PeaceMode = true;
            check(!gate.Tick(), gate.Name + " preserves peace mode");
            CombatManager.Instance.PeaceMode = false;
            foreach (var policy in new[] { default(FriendlyFireSettings), new FriendlyFireSettings(true, 0) })
            {
                SessionSettings.FriendlyFireForHit = policy;
                check(!gate.Tick(), gate.Name + " returns to native battle gating immediately for off/zero");
            }
            SessionSettings.FriendlyFireForHit = new(true, 50); Mirror.NetworkServer.active = false;
            check(!gate.Tick(), gate.Name + " does not activate from guest-only hostility");
            Mirror.NetworkServer.active = true;
            gate.Item.NetworkAvatar = new UnitAvatar();
            check(!gate.Tick(), gate.Name + " does not invent player ownership for an NPC");
            gate.Item.NetworkAvatar = new UnitAvatar { NetworkLeader = source };
            check(gate.Tick(), gate.Name + " activates for a living player-owned companion");
            spawner.PlayerAvatar = source;
            check(!gate.Tick(), gate.Name + " does not treat the companion owner as a hostile nearby player");
            spawner.PlayerAvatar = target;
            gate.Item.NetworkAvatar = source; source.IsInBattle = true;
            SessionSettings.FriendlyFireForHit = default;
            gate.Item.Target = new UnitAvatar { faction = "enemy" };
            check(gate.Tick(), gate.Name + " retains the native battle path while friendly fire is off");
            source.IsInBattle = false;
        }
        SessionSettings.FriendlyFireForHit = new(true, 50);
        cloud.Target = null; cloud.Update();
        check(cloud.Activated, "Dark Cloud combat gate needs neither a magic book nor an already selected candidate");
        PlayerSpawner.MultiplayerList.Clear(); cloud.Update();
        check(!cloud.Activated, "Dark Cloud stops qualifying when the other player departs");
        PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = new PlayerAvatar() }); cloud.Update();
        check(cloud.Activated, "Dark Cloud evaluates a replacement player without rebuilding the artifact");
        PlayerSpawner.MultiplayerList.Clear();
    }

    private static void Homing(Action<bool, string> check)
    {
        var source = new PlayerAvatar(); var target = new PlayerAvatar();
        foreach (string id in new[] { "Charm_PallasCard", "Charm_NearMagicBullet", "Charm_IceBow", "Charm_IcicleVine", "Charm_FireFeather",
            "Charm_Planet_Gray", "Charm_Planet_Red", "Charm_Planet_White" })
        {
            var bullet = new Bullet { NetworkOwner = source, Candidate = target, damageId = id };
            SessionSettings.FriendlyFireForHit = new(true, 50);
            bullet.Update(); check(bullet.HomingTarget == target, id + " acquires a hostile player");
            SessionSettings.FriendlyFireForHit = new(true, 0);
            bullet.Update(); check(bullet.HomingTarget == null, id + " loses its allied homing target at zero percent");
            SessionSettings.FriendlyFireForHit = new(true, 50);
            bullet.Update(); check(bullet.HomingTarget == target, id + " reacquires using the live policy");
            SessionSettings.FriendlyFireForHit = default;
            bullet.Update(); check(bullet.HomingTarget == null, id + " drops a living allied homing target when disabled");
            bullet.Candidate = new UnitAvatar { faction = "enemy" };
            bullet.Update(); check(bullet.HomingTarget == bullet.Candidate, id + " still acquires native enemy targets while off");
            bullet.HomingTarget.IsDead = true; bullet.Candidate = null;
            bullet.Update(); check(bullet.HomingTarget == null, id + " preserves native dead-target loss");
        }
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var pooled = new Bullet { NetworkOwner = source, Candidate = target, damageId = "Charm_IceBow" };
        pooled.Update(); check(pooled.HomingTarget == target, "Artifact bullet starts with eligible homing identity");
        pooled.SetHomingTarget(null); pooled.damageId = "UnrelatedWeapon";
        pooled.Update(); check(pooled.HomingTarget == null, "Pooled bullet cannot retain artifact admission after ID reuse");
        pooled.damageId = "Charm_IceBow"; pooled.FromType = EDamageFromType.Magic;
        pooled.Update(); check(pooled.HomingTarget == null, "Magic attack cannot borrow an artifact homing ID");
        pooled.FromType = EDamageFromType.None; Mirror.NetworkServer.active = false;
        pooled.Update(); check(pooled.HomingTarget == null, "Guest bullet retains native homing selection");
        Mirror.NetworkServer.active = true; pooled.Candidate = source;
        pooled.Update(); check(pooled.HomingTarget == null, "Homing artifact preserves self exclusion");
        pooled.NetworkOwner = new UnitAvatar { NetworkLeader = source };
        pooled.Update(); check(pooled.HomingTarget == null, "Companion homing artifact excludes its owner");
        pooled.Candidate = target; pooled.Update();
        check(pooled.HomingTarget == target, "Companion homing artifact can acquire another player");
    }

    private static void Nearest(Action<bool, string> check)
    {
        var source = new PlayerAvatar();
        var ally = new PlayerAvatar { transform = { position = new(1, 0) } };
        var enemy = new UnitAvatar { faction = "enemy", transform = { position = new(2, 0) } };
        PlayerInputController.Candidates.Clear();
        PlayerInputController.Candidates.AddRange(new UnitAvatar[] { source, ally, enemy });
        var guard = new Charm_GuardCounter { NetworkAvatar = source };
        var rock = new Charm_RockElephant { NetworkAvatar = source };
        var bow = new Charm_IceBow { NetworkAvatar = source };
        SessionSettings.FriendlyFireForHit = new(true, 50);
        check(PlayerInputController.SearchTargetNearestPoint(source, new(0, 0), 10) == enemy,
            "Ordinary nearest-point search retains native factions with friendly fire enabled");
        guard.FireRipostelaser(); rock.SpawnFlag(); bow.FireCastingServer();
        check(guard.Target == ally && rock.Target == ally && rock.SecondTarget == ally && bow.Target == ally && bow.SecondTarget == ally,
            "All audited direct nearest-point callsites select the closest hostile player");
        Finish(bow.FireCoroutine());
        check(bow.Target == ally && bow.SecondTarget == ally, "Both yielded Ice Bow nearest-point callsites enter the artifact scope");
        check(PlayerInputController.SearchTargetNearestPoint(source, new(0, 0), 10) == enemy,
            "Artifact nearest scope is restored before an ordinary caller runs");
        var pending = bow.FireCoroutine(); pending.MoveNext();
        SessionSettings.FriendlyFireForHit = default; Finish(pending);
        check(bow.Target == enemy && bow.SecondTarget == enemy, "Ice Bow reads off after yielding before target selection");
        SessionSettings.FriendlyFireForHit = new(true, 0); guard.FireRipostelaser();
        check(guard.Target == enemy, "Nearest artifact search preserves native enemies at zero percent");
        SessionSettings.FriendlyFireForHit = new(true, 50); Mirror.NetworkServer.active = false; guard.FireRipostelaser();
        check(guard.Target == enemy, "Guest artifact nearest search stays native");
        Mirror.NetworkServer.active = true;
        ally.transform.position = new(2, 0); guard.FireRipostelaser();
        check(guard.Target == ally, "Artifact search preserves first-candidate wins for equal distances");
        enemy.transform.position = new(1, 0); guard.FireRipostelaser();
        check(guard.Target == enemy, "Closer native enemy remains preferred over an eligible player");
        PlayerInputController.Candidates.Clear(); PlayerInputController.Candidates.Add(ally);
        check(PlayerInputController.SearchTargetNearestPoint(source, new(0, 0), 4) == null,
            "Unscoped boundary probe remains native");
        check(FriendlyFireRuntime.ArtifactNearestPoint(source, new(0, 0), 4) == ally,
            "Artifact nearest search includes the native squared-distance radius boundary");
        enemy.transform.position = ally.transform.position; PlayerInputController.Candidates.Add(enemy);
        check(PlayerInputController.SearchTargetNearestPoint(source, new(0, 0), 4) == enemy,
            "Ordinary nearest search also admits a native enemy exactly on the radius boundary");
        check(FriendlyFireRuntime.ArtifactNearestPoint(source, new(0, 0), 4) == ally,
            "Artifact nearest search keeps the first eligible candidate when both lie on the radius boundary");
        PlayerInputController.Candidates.Reverse();
        check(FriendlyFireRuntime.ArtifactNearestPoint(source, new(0, 0), 4) == enemy,
            "Reversing an equal-distance boundary tie preserves the new first candidate");
        check(FriendlyFireRuntime.ArtifactNearestPoint(source, new(0, 0), 3.99f) == null &&
            PlayerInputController.SearchTargetNearestPoint(source, new(0, 0), 3.99f) == null,
            "Both artifact and ordinary nearest searches reject candidates beyond the squared radius");
        PlayerInputController.Candidates.Clear();
    }

    private static void Procs(Action<bool, string> check)
    {
        var frostium = new Charm_FrostiumRing(); var typhoon = new Charm_TheTyphoonSheetmusic();
        var reddew = new Charm_Reddew(); var forks = new Charm_TuningForks();
        var glacier = new Charm_EchoOfTheGlacier(); var cloud = new ComboEffect_DarkCloud();
        var procs = new (string Name, Charm_Basic Item, Action Fire)[]
        {
            ("Frostium Ring", frostium, frostium.HandleAttackUnit), ("Typhoon Sheetmusic", typhoon, typhoon.HandleAttackUnit),
            ("Reddew", reddew, reddew.SummonDueFromVictim), ("Tuning Forks", forks, forks.CreateAttack),
            ("Glacier Echo", glacier, glacier.CreateFrostbite), ("Dark Cloud lightning", cloud, cloud.FireLightning)
        };
        foreach (var proc in procs)
        {
            foreach (int percent in new[] { 25, 100, 200 })
            {
                SessionSettings.FriendlyFireForHit = new(true, percent);
                var source = new PlayerAvatar(); var target = new PlayerAvatar(); var spread = new PlayerAvatar();
                proc.Item.NetworkAvatar = source; proc.Item.Target = spread; proc.Item.Damage = Hit(source);
                target.OnHit = (_, _) => proc.Fire();
                spread.OnHit = (_, _) => proc.Fire();
                target.ApplyDamage(Hit(source));
                check(target.Hp == 100 - 20 * percent / 100f && spread.Hp == 100 - 20 * percent / 100f && spread.Hits == 1,
                    proc.Name + " permits one scoped nested proc and scales it once at " + percent + "%");
                check(proc.Item.Damage.damage == 20, proc.Name + " leaves pooled damage unchanged");
            }
            SessionSettings.FriendlyFireForHit = new(true, 50);
            var attacker = new PlayerAvatar(); var victim = new PlayerAvatar(); var third = new PlayerAvatar();
            proc.Item.NetworkAvatar = victim; proc.Item.Target = attacker; proc.Item.Damage = Hit(victim);
            victim.OnHit = (_, _) => proc.Fire(); victim.ApplyDamage(Hit(attacker));
            check(attacker.Hp == 90 && victim.Hp == 90, proc.Name + " admits a native victim-owned retaliation once");
            proc.Item.NetworkAvatar = new PlayerAvatar(); proc.Item.Target = third; proc.Item.Damage = Hit(proc.Item.NetworkAvatar);
            victim.ApplyDamage(Hit(attacker));
            check(third.Hp == 100, proc.Name + " rejects an unrelated caster borrowing the active hit");
            proc.Item.NetworkAvatar = attacker; proc.Item.Damage = Hit(attacker);
            victim.OnHit = (_, _) => { SessionSettings.FriendlyFireForHit = default; proc.Fire(); };
            victim.ApplyDamage(Hit(attacker));
            check(third.Hp == 100, proc.Name + " observes off before nested proc dispatch");

            SessionSettings.FriendlyFireForHit = new(true, 50);
            third.OnHit = (_, _) => throw new InvalidOperationException("proc fixture failure");
            victim.OnHit = (_, _) =>
            {
                try { proc.Fire(); } catch (InvalidOperationException) { }
                third.OnHit = null;
                third.ApplyDamage(proc.Item.Damage);
            };
            victim.ApplyDamage(Hit(attacker));
            check(third.Hp == 90 && third.Hits == 1,
                proc.Name + " restores scope after exceptions before an unwrapped call reuses the same damage instance");
        }

        foreach (int percent in new[] { 25, 100, 200 })
        {
            SessionSettings.FriendlyFireForHit = new(true, percent);
            var source = new PlayerAvatar(); var victim = new PlayerAvatar(); var spread = new PlayerAvatar();
            var chim = new Charm_AttackChim { NetworkAvatar = source, Target = spread,
                Debuff = new() { OnApply = (caster, target) => target.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) } };
            victim.OnHit = (_, _) => chim.HandleAddedDebuffOnTarget();
            spread.OnHit = (_, _) => chim.HandleAddedDebuffOnTarget();
            victim.ApplyDamage(Hit(source));
            check(victim.Hp == 100 - 20 * percent / 100f && spread.Hp == 100 - 20 * percent / 100f && spread.Hits == 1,
                "Attack Chim spread admits electric damage once on another player at " + percent + "%");
        }
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var original = new PlayerAvatar(); var hurt = new PlayerAvatar(); var untouched = new PlayerAvatar();
        hurt.OnHit = (_, _) => untouched.ApplyDamage(Hit(original));
        hurt.ApplyDamage(Hit(original));
        check(untouched.Hp == 100, "Unwrapped arbitrary callbacks retain recursive allied-damage protection");

        var failedSpread = new Charm_AttackChim { NetworkAvatar = original, Target = untouched,
            Debuff = new() { OnApply = (_, _) => throw new InvalidOperationException("debuff fixture failure") } };
        hurt.OnHit = (_, _) =>
        {
            try { failedSpread.HandleAddedDebuffOnTarget(); } catch (InvalidOperationException) { }
            untouched.ApplyDebuff(new() { OnApply = (caster, target) => target.ApplyDamage(EffectTests.Tick(caster, "Debuff_Electric")) }, original);
        };
        hurt.ApplyDamage(Hit(original));
        check(untouched.Hp == 100 && untouched.DebuffApplications == 2,
            "Throwing Attack Chim application restores scope before an unrelated spread to the same player");
    }
}
