using System.Collections;
using SephiriaOne;

internal static class EffectTests
{
    internal static DamageInstance Tick(UnitAvatar source, string id = "Debuff_Burn", float amount = 20) =>
        new() { origin = source, id = id, damage = amount, targetFactionLayers = -1, damageType = EDamageType.ElementalEffectDamage };

    internal static void Run(Action<bool, string> check)
    {
        var source = new PlayerAvatar();
        var victim = new PlayerAvatar();
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var ring = new WeaponAddonCommon_BurnRing { NetworkAvatar = source, Target = victim };
        ring.DamageNearbyEnemies();
        check(ring.Selected, "Burn Ring includes a hostile player before damage dispatch");
        var feather = new Charm_FireFeather { NetworkAvatar = source, Target = victim };
        var meteor = new Charm_FlameGround_Meteor { NetworkAvatar = source, Target = victim };
        var cloud = new ComboEffect_DarkCloud { NetworkAvatar = source, Target = victim };
        var steps = new Charm_ThunderousSteps { NetworkAvatar = source, Target = victim };
        var chakram = new Charm_FireChakram { NetworkAvatar = source, Target = victim };
        void Finish(IEnumerator iterator) { while (iterator.MoveNext()) { } }
        bool[] SelectAll()
        {
            ring.DamageNearbyEnemies(); Finish(cloud.UseCloudCoroutine()); Finish(steps.CreateAttack()); chakram.OnUpdate();
            return new[] { ring.Selected, feather.SearchTarget(), meteor.SearchTarget(), cloud.Selected, steps.Selected, chakram.Selected };
        }
        check(SelectAll().All(x => x), "Audited burn/debuff item selectors agree on hostile players");
        foreach (var settings in new[] { default(FriendlyFireSettings), new FriendlyFireSettings(true, 0) })
        {
            SessionSettings.FriendlyFireForHit = settings;
            check(SelectAll().All(x => !x), "Off/zero restores native item selection");
            victim.ApplyDebuff(new(), source);
            victim.ApplyDamage(Tick(source));
            check(victim.DebuffApplications == 0 && victim.Hp == 100, "Off/zero blocks new team debuffs and all-faction existing DoT ticks");
        }
        SessionSettings.FriendlyFireForHit = new(true, 50);
        victim.OnHit = (_, damage) => { if (damage.damageType != EDamageType.ElementalEffectDamage) victim.ApplyDebuff(new(), source); };
        victim.ApplyDamage(new() { origin = source, damage = 20 });
        victim.ApplyDamage(Tick(source));
        check(victim.Hp == 80 && victim.DebuffApplications == 1, "On-hit burn and later damage ticks follow the same scale once");
        victim.DebuffImmune = true;
        victim.ApplyDebuff(new(), source);
        check(victim.DebuffApplications == 1, "Native debuff immunity remains authoritative");

        victim = new PlayerAvatar();
        var shock = new CharacterDebuff { OnApply = (caster, target) => target.ApplyDamage(Tick(caster, "Debuff_Electric")) };
        victim.OnHit = (_, d) => { if (d.damageType != EDamageType.ElementalEffectDamage) victim.ApplyDebuff(shock, source); };
        victim.ApplyDamage(new() { origin = source, damage = 20 });
        check(victim.Hp == 80, "Immediate electric/debuff stack damage is admitted inside its own target's successful hit");
        victim = new PlayerAvatar();
        victim.OnHit = (_, _) => victim.ApplyDebuff(shock, source);
        victim.ApplyDamage(new() { origin = source, damage = 20 });
        check(victim.Hp == 80 && victim.Hits == 2, "Debuff-on-debuff recursive damage remains bounded");

        var third = new PlayerAvatar();
        victim = new PlayerAvatar();
        victim.OnHit = (_, _) => victim.ApplyDebuff(new() { OnApply = (caster, target) => third.ApplyDamage(Tick(caster)) }, source);
        victim.ApplyDamage(new() { origin = source, damage = 20 });
        check(third.Hp == 100, "Another target cannot borrow scoped nested debuff permission");

        victim = new PlayerAvatar();
        third = new PlayerAvatar();
        victim.OnDeath = _ => third.ApplyDamage(new() { origin = source, damage = 20, id = "Charm_BurnExplosion", damageType = EDamageType.Projectile });
        victim.ApplyDamage(new() { origin = source, damage = 300 });
        check(third.Hp == 90, "A native burning-death explosion can hit another hostile player");
        victim = new PlayerAvatar(); third = new PlayerAvatar { Hp = 1 };
        var fourth = new PlayerAvatar();
        victim.OnDeath = _ => third.ApplyDamage(new() { origin = source, damage = 20, id = "Charm_BurnExplosion", damageType = EDamageType.Projectile });
        third.OnDeath = _ => fourth.ApplyDamage(new() { origin = source, damage = 20, id = "Charm_BurnExplosion", damageType = EDamageType.Projectile });
        victim.ApplyDamage(new() { origin = source, damage = 300 });
        check(third.IsDead && fourth.Hp == 100, "Recursive burning-death explosions cannot form an unbounded allied chain");

        ring.Target = source; ring.DamageNearbyEnemies(); check(!ring.Selected, "Item effects never add their own caster as a target");
        ring.NetworkAvatar = new UnitAvatar { NetworkLeader = source }; ring.DamageNearbyEnemies();
        check(!ring.Selected, "Companion effects cannot target their owner");
        ring.Target = new PlayerAvatar(); ring.DamageNearbyEnemies(); check(ring.Selected, "Companion effect follows its owner's hostility");
        ring.NetworkAvatar = source; ring.Target = new UnitAvatar { faction = "enemy" }; ring.DamageNearbyEnemies();
        check(ring.Selected, "Native enemy selection stays intact");
        ring.Target = new PlayerAvatar(); Mirror.NetworkServer.active = false; ring.DamageNearbyEnemies();
        check(!ring.Selected, "Guests do not extend native item selection"); Mirror.NetworkServer.active = true;
        var companion = new UnitAvatar { NetworkLeader = source };
        victim = new PlayerAvatar();
        var lingering = new CharacterDebuff(); victim.ApplyDebuff(lingering, companion);
        lingering.OnTick = () => victim.ApplyDamage(Tick(lingering.NetworkAttacker));
        companion.IsDead = true; companion.NetworkLeader = null;
        lingering.Update();
        check(victim.Hp == 90, "Companion burn retains its original friendly-fire scale after death clears the leader");
        SessionSettings.FriendlyFireForHit = default;
        lingering.Update();
        check(victim.Hp == 90, "Disabling friendly fire blocks lingering companion burn after leader cleanup");
        SessionSettings.FriendlyFireForHit = new(true, 50);
        victim = new PlayerAvatar(); source = new PlayerAvatar();
        lingering = new CharacterDebuff(); victim.ApplyDebuff(lingering, source);
        lingering.OnTick = () => victim.ApplyDamage(Tick(lingering.NetworkAttacker));
        lingering.OnExpire = () => victim.ApplyDamage(Tick(lingering.NetworkAttacker, "Debuff_Electric"));
        source.Destroyed = true; lingering.NetworkAttacker = null;
        lingering.Update();
        check(victim.Hp == 100 && lingering.IsEndBuff, "Disconnect ends owned debuff without ownerless burn or electric expiry damage");
        source = new PlayerAvatar(); victim = new PlayerAvatar();
        companion = new UnitAvatar { NetworkLeader = source };
        lingering = new CharacterDebuff(); victim.ApplyDebuff(lingering, companion);
        lingering.OnTick = () => victim.ApplyDamage(Tick(lingering.NetworkAttacker));
        lingering.OnExpire = lingering.OnTick;
        companion.NetworkLeader = new PlayerAvatar(); lingering.Update();
        check(lingering.IsEndBuff && victim.Hp == 100, "Ownership transfer cannot reuse the old companion debuff grant");

        foreach (int percent in new[] { 0, 25, 100, 200 })
        {
            SessionSettings.FriendlyFireForHit = new(true, percent);
            victim = new PlayerAvatar(); source = new PlayerAvatar();
            var tick = new CharacterDebuff();
            tick.InitializeAndSpawn(source, victim);
            tick.OnTick = () => victim.ApplyDamage(Tick(source));
            tick.OnExpire = tick.OnTick;
            tick.Update(); tick.AddStack(); tick.Destroy();
            check(victim.Hp == 100 - 3 * 20 * percent / 100f, "Tick, stacking and expiry each use current scale once: " + percent);
        }
        SessionSettings.FriendlyFireForHit = new(true, 50);
        source = new PlayerAvatar(); victim = new PlayerAvatar();
        victim.OnHit = (_, _) =>
        {
            try { victim.ApplyDebuff(new() { OnApply = (_, _) => throw new InvalidOperationException() }, source); }
            catch (InvalidOperationException) { }
            victim.ApplyDamage(Tick(source));
        };
        victim.ApplyDamage(new() { origin = source, damage = 20 });
        check(victim.Hp == 90, "Failed debuff application cannot leak nested-damage permission");
        victim = new PlayerAvatar();
        var failure = new CharacterDebuff { OnTick = () => throw new InvalidOperationException() };
        victim.ApplyDebuff(failure, source);
        try { failure.Update(); } catch (InvalidOperationException) { }
        source.Destroyed = true;
        victim.ApplyDamage(Tick(new UnitAvatar { faction = "enemy" }));
        check(victim.Hp == 80, "Failed update restores origin scope before an unrelated native monster tick");
        SessionSettings.FriendlyFireForHit = default;
        victim.ApplyDebuff(new(), new UnitAvatar { faction = "enemy" });
        check(victim.DebuffApplications == 2, "Off preserves monster-origin debuffs");
        foreach (bool destroy in new[] { false, true })
        {
            SessionSettings.FriendlyFireForHit = new(true, 50);
            source = new PlayerAvatar(); victim = new PlayerAvatar();
            failure = new CharacterDebuff { OnTick = () => throw new InvalidOperationException(), OnExpire = () => throw new InvalidOperationException() };
            victim.ApplyDebuff(failure, source);
            try { if (destroy) failure.Destroy(); else failure.AddStack(); } catch (InvalidOperationException) { }
            source.Destroyed = true; victim.ApplyDamage(Tick(new UnitAvatar { faction = "enemy" }));
            check(victim.Hp == 80, "Throwing debuff operation restores origin scope, destroy=" + destroy);
        }
        source = new PlayerAvatar(); victim = new PlayerAvatar();
        var tagged = new CharacterDebuff(); victim.ApplyDebuff(tagged, source);
        var untagged = new CharacterDebuff { OnTick = () => victim.ApplyDamage(Tick(new UnitAvatar { faction = "enemy" })) };
        tagged.OnTick = () => { untagged.Update(); victim.ApplyDamage(Tick(source)); };
        tagged.Update();
        check(victim.Hp == 70, "Nested untagged debuff keeps native enemy damage and restores the outer team scope");

        source = new PlayerAvatar(); victim = new PlayerAvatar();
        cloud.NetworkAvatar = source; cloud.Target = victim;
        SessionSettings.FriendlyFireForHit = new(true, 100);
        var pending = cloud.UseCloudCoroutine(); pending.MoveNext();
        SessionSettings.FriendlyFireForHit = default; Finish(pending);
        check(!cloud.Selected, "Coroutine reads off after yielding rather than retaining stale hostility");
        cloud.Target = new PlayerAvatar(); SessionSettings.FriendlyFireForHit = new(true, 100); Finish(cloud.UseCloudCoroutine());
        check(cloud.Selected, "A replacement/rejoining player is selected with current policy");

        var active = new CharacterDebuff(); victim.ApplyDebuff(active, source);
        for (int i = 0; i < 100; i++) { active.Update(); _ = FriendlyFireRuntime.ItemTarget(victim, EDamageFromType.None, source); }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { active.Update(); _ = FriendlyFireRuntime.ItemTarget(victim, EDamageFromType.None, source); }
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warm item targeting and tracked-debuff updates allocate no per-tick memory");
    }
}
