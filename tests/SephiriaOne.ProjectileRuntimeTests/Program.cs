using Mirror;
using SephiriaOne;
using UnityEngine;

int checks = 0, failures = 0;
var pool = new ProjectileSpawner<Bullet>();
var key = new GameObject("Secondary");
PlayerAvatar player = null;

void Check(bool value, string message)
{
    checks++;
    if (!value) throw new Exception(message);
}
PlayerAvatar Connect(int id)
{
    var go = new GameObject("Player " + id);
    var spawner = go.AddComponent<PlayerSpawner>();
    var avatar = go.AddComponent<PlayerAvatar>();
    var identity = NetworkServer.Register(go);
    var connection = new NetworkConnectionToClient { connectionId = id, identity = identity };
    spawner.netIdentity = identity;
    spawner.connectionToClient = connection;
    spawner.PlayerAvatar = avatar;
    avatar.spawner = spawner;
    NetworkServer.connections[id] = connection;
    return avatar;
}
void Configure(bool enabled = true, int total = 2, int source = 2)
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(enabled, total, source);
    ProjectileLimitRuntime.Refresh();
}
Bullet Spawn(string source = "kunai", UnitAvatar owner = null, string damageId = "Charm_Kunai", GameObject prefab = null) =>
    ProjectileLimitRuntime.Spawn(pool, prefab ?? key, default, true, EDamageFromType.None, damageId,
        1, 0, 0, owner ?? player, 0, 0, default, default, null, null, 0,
        EDamageElementalType.Physical, true, source);
Bullet Native(string damageId = "Weapon_Attack", UnitAvatar owner = null) =>
    pool.Spawn(key, default, true, EDamageFromType.None, damageId, 1, 0, 0, owner ?? player,
        0, 0, default, default, null, null, 0, EDamageElementalType.Physical, true);
SpecialProjectile_FuryMP Scheduler()
{
    var prefab = new GameObject("FuryMP");
    prefab.AddComponent<SpecialProjectile_FuryMP>();
    var data = new NewWeaponFireData_SpecialProjectile { projectilePrefab = prefab };
    if (!ProjectileLimitRuntime.BeginEmitter(data, player, out var admission)) return null;
    try
    {
        var result = new GameObject("FuryMP(Clone)").AddComponent<SpecialProjectile_FuryMP>();
        ProjectileLimitRuntime.SpawnEmitter(result);
        result.Networkowner = player;
        NetworkServer.Register(result.gameObject);
        result.spawnedPrefabName = prefab.name;
        return result;
    }
    finally { ProjectileLimitRuntime.EndEmitter(admission); }
}
Bullet Emit(SpecialProjectile_FuryMP emitter)
{
    var previous = ProjectileLimitRuntime.BeginLiberation(emitter);
    try { return Spawn("liberation", emitter.Networkowner, "Weapon_SpecialAttack", new GameObject("FuryMPBullet_Physical")); }
    finally { ProjectileLimitRuntime.EndLiberation(previous); }
}
void Despawn(Bullet bullet)
{
    ProjectileLimitRuntime.Despawned(bullet);
    bullet.IsSpawned = false;
    NetworkServer.spawned.Remove(bullet.gameObject.GetComponent<NetworkIdentity>().netId);
}
BulletDestroyModule Module(Bullet bullet)
{
    var module = bullet.gameObject.AddComponent<BulletDestroyModule>();
    module.Bullet = bullet;
    return module;
}
void Test(string name, Action run)
{
    Time.unscaledTimeAsDouble = 0;
    Time.timeAsDouble = 0;
    ProjectileLimitRuntime.Clear();
    NetworkServer.connections.Clear();
    NetworkServer.spawned.Clear();
    NetworkServer.active = true;
    ProjectileLimitFeature.Available = true;
    DungeonManager.Instance = new GameObject("Dungeon").AddComponent<DungeonManager>();
    SessionSettings.ResourceGeneration = 0;
    pool = new ProjectileSpawner<Bullet>();
    player = Connect(1);
    Configure();
    try { run(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}

Test("Caps reject before native instantiation, separately by source and player", () =>
{
    Configure(total: 2, source: 1);
    Check(Spawn(), "First Kunai admitted");
    int calls = pool.NativeCalls;
    Check(!Spawn() && pool.NativeCalls == calls, "Source denial never enters native spawn");
    Check(Spawn("meteor", damageId: "Charm_Meteor"), "Other source has capacity");
    Check(!Spawn("ice-vine", damageId: "Charm_IcicleVine"), "Total also caps a fresh source");
    Check(Spawn(owner: Connect(2)), "A second connection has its own budget");
});
Test("Transport diagnostics read cached counts without reconciling live state", () =>
{
    Configure(total: 1, source: 1);
    var connection = player.spawner.connectionToClient;
    var bullet = Spawn();
    Scheduler();
    int calls = pool.NativeCalls;
    SessionSettings.ResourceGeneration++;
    string snapshot = ProjectileLimitRuntime.DescribeDiagnostics(connection);
    Check(snapshot.Contains("playerBullets=1;") && snapshot.Contains("playerEmitters=1;"), "Snapshot separates both limiter pools");
    Check(pool.NativeCalls == calls && bullet, "Reading does not spawn, despawn or destroy objects");
    var replacement = new NetworkConnectionToClient { connectionId = connection.connectionId };
    Check(ProjectileLimitRuntime.DescribeDiagnostics(replacement).Contains("playerBullets=0;"), "Snapshot cannot attribute an old receipt to a reused ID");
    ProjectileLimitRuntime.RetireConnection(connection);
    Check(ProjectileLimitRuntime.DescribeDiagnostics(connection).Contains("playerBullets=0;"), "Retired receipts are not live counts");
    ProjectileLimitRuntime.Clear();
    Check(ProjectileLimitRuntime.DescribeDiagnostics(connection).Contains("enabled=False;"), "Reading cannot re-enable or reseed cleared state");
    Check(bullet && pool.NativeCalls == calls, "Teardown diagnostics preserve existing native objects");
});
Test("Lower limits retain live instances and wait for actual releases", () =>
{
    var first = Spawn();
    var second = Spawn();
    Configure(total: 1, source: 1);
    Check(first && second && !Spawn(), "Lower cap keeps both live objects");
    Despawn(first);
    Check(!Spawn(), "One remaining object still occupies the lowered cap");
    second.gameObject.DestroyNow();
    Check(Spawn(), "Actual destruction frees the final occupied slot");
    first.gameObject.DestroyNow();
    Check(!Spawn(), "Late duplicate release cannot debit a replacement");
});
Test("Disabling and re-enabling adopts both tracked and off-period live bullets", () =>
{
    Configure(total: 3, source: 3);
    Spawn();
    Configure(false, 3, 3);
    Spawn();
    Spawn();
    Configure(true, 3, 3);
    Check(!Spawn(), "All three pre-existing bullets are counted");
    Despawn(pool.Created[0]);
    Check(Spawn(), "Native despawn returns an adopted slot");
});
Test("A delayed owner-null bullet keeps capacity across re-enabling", () =>
{
    Configure(total: 1, source: 1);
    var delayed = Spawn();
    delayed.NetworkOwner = null; // Bullet.DestroySelf clears owner before delayed despawn.
    Configure(false, 1, 1);
    Configure(true, 1, 1);
    Check(!Spawn(), "Recorded connection provenance adopts delayed live object");
    delayed.gameObject.DestroyNow();
    Check(Spawn(), "Only actual delayed destruction frees its slot");
});
Test("Inherited source survives parent despawn and reserves children independently", () =>
{
    Configure(total: 1, source: 1);
    var parent = Spawn();
    var previous = ProjectileLimitRuntime.BeginChildren(Module(parent));
    try
    {
        Despawn(parent); // Native CreateBulletSpread despawns before spawning its spread.
        Check(Spawn(null, damageId: "DifferentChildId"), "Child inherits the released parent source");
        Check(!Spawn(null, damageId: "DifferentChildId"), "Second child obeys cap");
    }
    finally { ProjectileLimitRuntime.EndChildren(previous); }
    parent.gameObject.DestroyNow();
    Check(!Spawn(), "Parent OnDestroy cannot debit the child lease");
});
Test("Native OnSpawn reentrancy cannot claim or overspend the parent lease", () =>
{
    Configure(total: 1, source: 1);
    Bullet nested = null;
    pool.OnSpawn = parent =>
    {
        pool.OnSpawn = null;
        Check(!Spawn(), "Reserved parent slot denies recursive eligible spawn");
        nested = Native();
    };
    var parent = Spawn();
    Check(parent && nested, "Unclassified primary is still created inside callback");
    nested.gameObject.DestroyNow();
    Check(!Spawn(), "Primary destruction did not steal or release parent's lease");
    parent.gameObject.DestroyNow();
    Check(Spawn(), "Parent owns and releases the reservation");
});
Test("Null and pre-instantiation native failures roll back reservation", () =>
{
    Configure(total: 1, source: 1);
    var result = ProjectileLimitRuntime.Spawn(pool, null, default, true, default, "Charm_Kunai",
        1, 0, 0, player, 0, 0, default, default, null, null, 0, default, true, "kunai");
    Check(result == null, "Native null result is preserved");
    pool.BeforeInstantiation = () => throw new InvalidOperationException("Fixture native failure");
    try { Spawn(); throw new Exception("Expected fixture exception"); }
    catch (InvalidOperationException) { }
    pool.BeforeInstantiation = null;
    Check(Spawn(), "Neither failed attempt consumes capacity");
});
Test("Initialization failure retains its still-live object until destruction", () =>
{
    Configure(total: 1, source: 1);
    pool.OnInitialize = _ => throw new InvalidOperationException("Fixture native initialization failure");
    try { Spawn(); throw new Exception("Expected fixture exception"); }
    catch (InvalidOperationException) { }
    pool.OnInitialize = null;
    Check(!Spawn(), "Native orphan is still alive and owns its reservation");
    pool.Created[0].gameObject.DestroyNow();
    Check(Spawn(), "Destroying native orphan releases reservation");
});
Test("Disconnect and reused network id cannot spend the new connection's budget", () =>
{
    Configure(total: 1, source: 1);
    var oldPlayer = player;
    var oldBullet = Spawn();
    var oldConnection = player.spawner.connectionToClient;
    ProjectileLimitRuntime.RetireConnection(oldConnection);
    player = Connect(oldConnection.connectionId);
    Check(Spawn(), "Replacement connection receives fresh capacity");
    Check(!Spawn(owner: oldPlayer), "Retired avatar cannot create further eligible roots");
    var previous = ProjectileLimitRuntime.BeginChildren(Module(oldBullet));
    try { Check(!Spawn(null, oldPlayer), "Retired parent cannot create descendants"); }
    finally { ProjectileLimitRuntime.EndChildren(previous); }
    oldBullet.gameObject.DestroyNow();
    Check(!Spawn(), "Old lifetime cleanup cannot debit replacement connection");
});
Test("Literal connection object reuse creates fresh receipts and rejects old descendants", () =>
{
    Configure(total: 1, source: 1);
    var old = Spawn();
    var connection = player.spawner.connectionToClient;
    ProjectileLimitRuntime.RetireConnection(connection);
    ProjectileLimitRuntime.ConnectionAdded(connection);
    Check(Spawn(), "Re-added connection object receives a fresh budget");
    var previous = ProjectileLimitRuntime.BeginChildren(Module(old));
    try { Check(!Spawn(null), "Old provenance remains retired despite identical native objects"); }
    finally { ProjectileLimitRuntime.EndChildren(previous); }
    old.gameObject.DestroyNow();
    Check(!Spawn(), "Old cleanup cannot debit a literal object's new receipt");
});
Test("Re-enabling never adopts a retired projectile into a reused connection lifetime", () =>
{
    Configure(total: 1, source: 1);
    var old = Spawn();
    var connection = player.spawner.connectionToClient;
    ProjectileLimitRuntime.RetireConnection(connection);
    ProjectileLimitRuntime.ConnectionAdded(connection);
    Configure(false, 1, 1);
    Configure(true, 1, 1);
    var fresh = Spawn();
    Check(fresh, "Retired projectile must not occupy the new connection budget after reseed");
    fresh.gameObject.DestroyNow();
    var previous = ProjectileLimitRuntime.BeginChildren(Module(old));
    try { Check(!Spawn(null), "Reseeding does not revive retired descendant provenance"); }
    finally { ProjectileLimitRuntime.EndChildren(previous); }
});
Test("Reseeding retains retired descendant denial after an ordinary disconnect", () =>
{
    Configure(total: 1, source: 1);
    var oldPlayer = player;
    var old = Spawn();
    ProjectileLimitRuntime.RetireConnection(player.spawner.connectionToClient);
    player = Connect(1);
    Configure(false, 1, 1);
    Configure(true, 1, 1);
    var previous = ProjectileLimitRuntime.BeginChildren(Module(old));
    try { Check(!Spawn(null, oldPlayer), "Retired tracked parent must not become an unclassified native child source"); }
    finally { ProjectileLimitRuntime.EndChildren(previous); }
    Check(Spawn(), "New connection's first slot remains available");
});
Test("Connection object reuse while limits are disabled cannot revive old provenance", () =>
{
    Configure(total: 1, source: 1);
    var old = Spawn();
    var connection = player.spawner.connectionToClient;
    Configure(false, 1, 1);
    ProjectileLimitRuntime.RetireConnection(connection);
    ProjectileLimitRuntime.ConnectionAdded(connection);
    Configure(true, 1, 1);
    Check(Spawn(), "Off-period disconnect still retires old connection's recorded provenance");
});
Test("Ordinary refresh and owner avatar replacement do not reset connection counters", () =>
{
    Configure(total: 1, source: 1);
    var existing = Spawn();
    ProjectileLimitRuntime.Refresh(); // Equipment/settings presentation refresh.
    Check(!Spawn(), "Unchanged configuration preserves occupied count");
    var replacement = new GameObject("New avatar").AddComponent<PlayerAvatar>();
    replacement.spawner = player.spawner;
    player.spawner.PlayerAvatar = replacement;
    player = replacement;
    Check(!Spawn(), "Same connection keeps old projectile accounting through avatar replacement");
    existing.gameObject.DestroyNow();
    Check(Spawn(), "Lifetime completion restores the shared slot");
});
Test("Run generation reseeds live instances and retires stale leases", () =>
{
    Configure(total: 1, source: 1);
    var old = Spawn();
    SessionSettings.ResourceGeneration++;
    Check(!Spawn(), "First new-run admission reseeds already-live projectile");
    old.gameObject.DestroyNow();
    Check(Spawn(), "Rebound lifetime releases new run's adopted slot");
});
Test("Companion emissions count against the player's aggregate", () =>
{
    Configure(total: 1, source: 1);
    var companion = new GameObject("Companion").AddComponent<UnitAvatar>();
    companion.NetworkLeader = player;
    var bullet = Spawn(owner: companion);
    Check(bullet && !Spawn(), "Player and companion share total");
    companion.NetworkLeader = null; // Ownership loss does not erase existing count.
    Check(!Spawn(), "Existing companion projectile retains captured provenance");
    bullet.gameObject.DestroyNow();
    Check(Spawn(), "Actual destruction frees captured player slot");
});
Test("Recorded source ownership survives native owner transfer and re-enabling", () =>
{
    Configure(total: 1, source: 1);
    var firstPlayer = player;
    var original = Spawn();
    var secondPlayer = Connect(2);
    original.NetworkOwner = secondPlayer;
    Configure(false, 1, 1);
    Configure(true, 1, 1);
    Check(!Spawn(owner: firstPlayer), "Original connection keeps the existing projectile charge");
    Check(Spawn(owner: secondPlayer), "Native owner change cannot move the old charge into another budget");
    original.gameObject.DestroyNow();
    Check(Spawn(owner: firstPlayer), "Actual destruction releases the originally captured owner");
});
Test("Separate planet artifacts share settings and retain independent source counters", () =>
{
    Configure(total: 3, source: 1);
    Check(Spawn("planets", damageId: "Charm_Planet_Red"), "Red planet admitted");
    Check(Spawn("planets", damageId: "Charm_Planet_White"), "White planet has its own source");
    Check(!Spawn("planets", damageId: "Charm_Planet_Red"), "Same planet remains capped");
    Configure(false, 3, 1);
    Configure(true, 3, 1);
    Check(!Spawn("planets", damageId: "Charm_Planet_White"), "Adoption preserves planet identity");
});
Test("Liberation scheduler uses independent bounded capacity from its emitted bullets", () =>
{
    Configure(total: 1, source: 1);
    var prefab = new GameObject("FuryMP");
    prefab.AddComponent<SpecialProjectile_FuryMP>();
    var data = new NewWeaponFireData_SpecialProjectile { projectilePrefab = prefab };
    Check(ProjectileLimitRuntime.BeginEmitter(data, player, out var admission), "First scheduler admitted");
    var emitter = new GameObject("FuryMP(Clone)").AddComponent<SpecialProjectile_FuryMP>();
    emitter.Networkowner = player;
    try { ProjectileLimitRuntime.SpawnEmitter(emitter); NetworkServer.Register(emitter.gameObject); }
    finally { ProjectileLimitRuntime.EndEmitter(admission); }
    Check(!ProjectileLimitRuntime.BeginEmitter(data, player, out _), "Second scheduler denied");
    Check(Spawn("liberation", damageId: "Weapon_SpecialAttack", prefab: new GameObject("FuryMPBullet_Physical")),
        "Scheduler does not consume its own bullet capacity");
    Check(!Spawn("liberation"), "Emitted missile cap remains enforced");
    Configure(false, 1, 1);
    Configure(true, 1, 1);
    Check(!ProjectileLimitRuntime.BeginEmitter(data, player, out _), "Existing scheduler is adopted on re-enable");
    emitter.gameObject.DestroyNow();
    Check(ProjectileLimitRuntime.BeginEmitter(data, player, out var replacement), "Destroying scheduler frees its own slot");
    ProjectileLimitRuntime.EndEmitter(replacement);
    Check(!Spawn("liberation"), "Scheduler cleanup cannot release emitted bullet slot");
});
Test("Retired Liberation schedulers cannot emit through reused avatars before or after reseeding", () =>
{
    Configure(total: 1, source: 1);
    var old = Scheduler();
    Check(old, "Old connection has a scheduler");
    var connection = player.spawner.connectionToClient;
    ProjectileLimitRuntime.RetireConnection(connection);
    ProjectileLimitRuntime.ConnectionAdded(connection);
    Check(!Emit(old), "Stale scheduler is denied even when its native avatar is reused");
    var fresh = Scheduler();
    Check(fresh, "New connection has independent scheduler capacity");
    var missile = Emit(fresh);
    Check(missile, "New scheduler can emit");
    missile.gameObject.DestroyNow();
    Configure(false, 1, 1);
    Configure(true, 1, 1);
    Check(!Emit(old), "Reseeding retains stale scheduler rejection");
    Check(Emit(fresh), "Reseeding preserves current scheduler admission");
    old.gameObject.DestroyNow();
    Check(!Scheduler(), "Stale scheduler destruction cannot debit current scheduler");
});
Test("Unknown primary attacks, native nonplayers, disabled and nonhost paths stay native", () =>
{
    Configure(total: 1, source: 1);
    Spawn();
    Check(Spawn(null, damageId: "Weapon_Attack"), "Primary bypasses occupied secondary cap");
    var npc = new GameObject("Native NPC").AddComponent<UnitAvatar>();
    Check(Spawn(owner: npc), "Native NPC does not need a player connection");
    Configure(false, 1, 1);
    Check(Spawn() && Spawn(), "Disabled path stays native");
    Configure(true, 1, 1);
    NetworkServer.active = false;
    Check(Spawn(), "Nonhost path stays native");
});
Test("Warmed runtime denial path allocates no managed memory", () =>
{
    Configure(total: 4, source: 1);
    Check(Spawn(), "Source capacity is occupied while total capacity remains");
    for (int i = 0; i < 2_000; i++) Spawn();
    int nativeCalls = pool.NativeCalls;
    int admitted = 0;
    long before = GC.GetAllocatedBytesForCurrentThread();
    // The existing helper supplies cached objects, literals and value types.
    // Measure the production Capture/CWT/settings/ledger denial path without
    // assertions, delegates, formatting or native object creation in the loop.
    for (int i = 0; i < 10_000; i++)
        if (Spawn()) admitted++;
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Check(admitted == 0 && pool.NativeCalls == nativeCalls, "All measured attempts are denied before native spawn");
    Check(allocated == 0, $"Warmed 10,000 runtime denials allocated {allocated} bytes");
    Console.WriteLine($"Runtime denial allocation: {allocated} bytes / 10,000 calls.");
});

Test("Rate-only mode gates creation before native work and keeps active instances", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, 1, 1, rateEnabled: true, playerRate: 3, sourceRate: 2);
    ProjectileLimitRuntime.Refresh();
    var first = Spawn(); var second = Spawn();
    Check(first && second, "Rate-only mode does not enforce disabled active cap");
    int calls = pool.NativeCalls;
    Check(!Spawn() && pool.NativeCalls == calls, "Rate exhausted source never enters native creation");
    Check(Spawn("meteor", damageId: "Charm_Meteor"), "Other source can consume shared remaining credit");
    Check(!Spawn("ice-bow", damageId: "Charm_IceBow"), "Shared total limits a new source");
    Check(first.IsSpawned && second.IsSpawned, "Existing projectiles are not evicted");
    Despawn(first);
    Check(!Spawn(), "Actual despawn frees active count but never refunds creation credit");
    Time.timeAsDouble = 1000;
    Check(!Spawn(), "Game time acceleration does not refill a real-time performance budget");
    Time.unscaledTimeAsDouble = .51;
    Check(Spawn(), "Unscaled continuous refill admits a later attempt");
    Check(Spawn(null, damageId: "Weapon_Attack"), "Primary attacks stay native when secondary rate is exhausted");
});
Test("Cap denial does not consume rate credit; settings edits do not refill it", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(true, 1, 1, rateEnabled: true, playerRate: 2, sourceRate: 2);
    ProjectileLimitRuntime.Refresh();
    var first = Spawn();
    Check(first && !Spawn(), "Instance cap still applies");
    Despawn(first);
    var second = Spawn();
    Check(second, "Denied instance attempt did not spend creation credit");
    Despawn(second);
    Check(!Spawn(), "Both admitted creations spent credit");
    SessionSettings.ProjectilesForUse = SessionSettings.ProjectilesForUse.WithSourceLimit("kunai", 8).WithPlayerRate(10);
    ProjectileLimitRuntime.Refresh();
    Check(!Spawn(), "Live edits do not mint credit or reset rate counters");
    SessionSettings.ProjectilesForUse = SessionSettings.ProjectilesForUse.WithRateEnabled(false);
    ProjectileLimitRuntime.Refresh();
    Check(Spawn(), "Rate off immediately restores active-cap-only admission");
});
Test("Rate reservation precedes native reentrancy and resets across run and connection lifetimes", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, rateEnabled: true, playerRate: 1, sourceRate: 1);
    ProjectileLimitRuntime.Refresh();
    bool nestedRejected = false;
    pool.BeforeInstantiation = () => { pool.BeforeInstantiation = null; nestedRejected = !Spawn(); };
    Check(Spawn() && nestedRejected, "Nested initialization cannot spend the same rate allowance");
    ProjectileLimitRuntime.RetireConnection(player.spawner.connectionToClient);
    Check(!Spawn(), "Old connection cannot emit after retirement");
    player = Connect(2);
    Check(Spawn(), "New connection starts with its own credit");
    SessionSettings.ResourceGeneration++;
    Check(Spawn(), "Fresh run resets rate allowance despite retained avatar");
});
Test("RPC effects share missile rates and never receive an active-cap-only restriction", () =>
{
    Configure(total: 1, source: 1);
    Check(Spawn() && ProjectileLimitRuntime.AdmitEffect(player, "ice-sword"), "Instance cap does not claim to count local effects");
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, rateEnabled: true, playerRate: 3, sourceRate: 1);
    ProjectileLimitRuntime.Refresh();
    Check(ProjectileLimitRuntime.AdmitEffect(player, "ice-sword"), "First local effect admitted");
    Check(!ProjectileLimitRuntime.AdmitEffect(player, "ice-sword"), "Repeated local effect rate limited");
    Check(Spawn() && ProjectileLimitRuntime.AdmitEffect(player, "ice-guillotine"), "Different effects share remaining player credit");
    Check(!ProjectileLimitRuntime.AdmitEffect(player, "frost-flame-sword"), "A separate callback cannot bypass total rate");
    SessionSettings.ProjectilesForUse = SessionSettings.ProjectilesForUse.WithEnabled(true);
    ProjectileLimitRuntime.Refresh();
    Check(!ProjectileLimitRuntime.AdmitEffect(player, "frost-flame-sword"), "Enabling instance caps does not reset rate credit");
});
Test("Rate invalid clock is contained and active-cap operation remains available", () =>
{
    Time.unscaledTimeAsDouble = double.NaN;
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(true, 1, 1, rateEnabled: true, playerRate: 2, sourceRate: 2);
    ProjectileLimitRuntime.Refresh();
    Check(!Spawn(), "Invalid creation clock fails closed when rate enabled");
    SessionSettings.ProjectilesForUse = SessionSettings.ProjectilesForUse.WithRateEnabled(false);
    ProjectileLimitRuntime.Refresh();
    Check(Spawn() && !Spawn(), "Active caps continue independently with invalid rate clock");
    Time.unscaledTimeAsDouble = 1;
    SessionSettings.ProjectilesForUse = SessionSettings.ProjectilesForUse.WithEnabled(false).WithRateEnabled(true);
    ProjectileLimitRuntime.Refresh();
    Check(Spawn(), "Finite clock recovers normal admission");
});
Test("Ice families are adopted on activation and share elemental source identities", () =>
{
    Configure(false);
    var ids = new[] { "Charm_AirSlash", "Charm_IceSpear", "Charm_IceBow", "Charm_IceHammer" };
    var sources = new[] { "ice-air-slash", "ice-spear", "ice-bow", "ice-hammer" };
    foreach (string id in ids) Native(id);
    Configure(true, 64, 1);
    for (int i = 0; i < ids.Length; i++)
        Check(ProjectileLimitRuntime.SourceOf(ids[i], "AnyIceOrFireVariant") == sources[i] && !Spawn(sources[i], damageId: ids[i]),
            "Existing Ice/fire family counts prevent excess new admissions: " + sources[i]);
});
Test("All Bolt elements share one limit and adopt existing casts without swallowing other magic", () =>
{
    Configure(false);
    var ids = new[] { "Skill_FireBolt", "Skill_AirBolt", "Skill_EarthBolt", "Skill_LightningBolt", "Skill_IceBolt" };
    foreach (string id in ids)
    {
        Check(ProjectileLimitRuntime.SourceOf(id, "Bolt") == "bolt-magic", "Recognize only audited Bolt IDs: " + id);
        Native(id);
    }
    Native("Skill_FireArrow");
    Configure(true, 64, 1);
    Check(!Spawn("bolt-magic", damageId: ids[0]), "Already-flying bolts consume the same source limit on activation");
    foreach (var bolt in pool.Created.Take(5).ToArray()) Despawn(bolt);
    var accepted = Spawn("bolt-magic", damageId: ids[1]);
    Check(accepted && !Spawn("bolt-magic", damageId: ids[3]), "Switching elements does not bypass the cap");
    Check(Spawn("kunai"), "Unrelated source remains available");
    accepted.gameObject.DestroyNow();
    Check(Spawn("bolt-magic", damageId: ids[4]), "Natural destruction releases Bolt capacity");
    Check(ProjectileLimitRuntime.SourceOf("Skill_FireArrow", "Bolt") == null &&
        ProjectileLimitRuntime.SourceOf("Skill_FrostDagger", "Bolt") == null, "Do not classify unrelated magic from prefab names");
});
Test("Bolt descendants, rate credit and connection retirement share existing lifecycle", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(true, 64, 1, rateEnabled: true, playerRate: 64, sourceRate: 2);
    ProjectileLimitRuntime.Refresh();
    var parent = Spawn("bolt-magic", damageId: "Skill_FireBolt");
    var previous = ProjectileLimitRuntime.BeginChildren(Module(parent));
    try
    {
        Despawn(parent);
        var childBolt = Spawn(null, damageId: "Child");
        Check(childBolt && !Spawn(null), "Child uses the same Bolt instance cap");
        Despawn(childBolt);
        Check(!Spawn(null), "Despawn does not refund Bolt rate credit");
    }
    finally { ProjectileLimitRuntime.EndChildren(previous); }
    Time.unscaledTimeAsDouble = 0.5;
    var old = Spawn("bolt-magic", damageId: "Skill_IceBolt");
    Check(old, "Shared rate refill admits another element");
    var oldConnection = player.spawner.connectionToClient;
    ProjectileLimitRuntime.RetireConnection(oldConnection);
    player = Connect(oldConnection.connectionId);
    Check(Spawn("bolt-magic", damageId: "Skill_LightningBolt"), "Rejoined player receives fresh Bolt capacity");
    old.gameObject.DestroyNow();
    Check(!Spawn("bolt-magic"), "Stale destruction never releases the replacement's occupied slot");
});
Test("Warmed exhausted rate path is allocation-free before instance reservations", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, rateEnabled: true, playerRate: 1, sourceRate: 1);
    ProjectileLimitRuntime.Refresh();
    Check(Spawn(), "Initial rate credit is available");
    for (int i = 0; i < 2000; i++) Spawn();
    int native = pool.NativeCalls, accepted = 0;
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++) if (Spawn()) accepted++;
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Check(accepted == 0 && pool.NativeCalls == native && allocated == 0, $"Exhausted rate path: {allocated} bytes / 10000 attempts");
});
Test("Rate-only tracking survives cap enable and stale descendants", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, 1, 1, rateEnabled: true, playerRate: 3, sourceRate: 3);
    ProjectileLimitRuntime.Refresh();
    var parent = Spawn(); var module = Module(parent);
    SessionSettings.ProjectilesForUse = SessionSettings.ProjectilesForUse.WithEnabled(true);
    ProjectileLimitRuntime.Refresh();
    Check(!Spawn(), "Enabling caps sees instances already tracked under rate-only mode");
    Despawn(parent);
    var prior = ProjectileLimitRuntime.BeginChildren(module);
    try { Check(Spawn(null), "A child keeps parent source after native parent despawn"); }
    finally { ProjectileLimitRuntime.EndChildren(prior); }
    ProjectileLimitRuntime.RetireConnection(player.spawner.connectionToClient);
    player = Connect(1);
    prior = ProjectileLimitRuntime.BeginChildren(module);
    try { Check(!Spawn(null), "Reused ID cannot give retired child provenance fresh credit"); }
    finally { ProjectileLimitRuntime.EndChildren(prior); }
    Check(Spawn(), "Replacement connection remains independent");
});
Test("Low-rate Liberation keeps schedulers independent from damage emission credit", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, rateEnabled: true, playerRate: 1, sourceRate: 1);
    ProjectileLimitRuntime.Refresh();
    var emitter = Scheduler();
    Check(emitter && Emit(emitter), "One credit allows a scheduler's first missile");
    Check(!Emit(emitter), "Further missiles still obey one creation per second");
    Time.unscaledTimeAsDouble = 1;
    Check(Emit(emitter), "Later scheduler emission uses replenished credit");
});
Test("Partial native creation failure cannot refund rate work already admitted", () =>
{
    SessionSettings.ProjectilesForUse = new ProjectileLimitSettings(false, rateEnabled: true, playerRate: 1, sourceRate: 1);
    ProjectileLimitRuntime.Refresh();
    pool.BeforeInstantiation = () => throw new InvalidOperationException("Native failure");
    try { Spawn(); } catch (InvalidOperationException) { }
    pool.BeforeInstantiation = null;
    Check(!Spawn(), "Failure cannot trigger repeated free creation attempts");
    Time.unscaledTimeAsDouble = 1;
    Check(Spawn(), "Budget naturally recovers after failure");
});

Console.WriteLine($"{checks} checks, {failures} failed cases.");
return failures == 0 ? 0 : 1;
