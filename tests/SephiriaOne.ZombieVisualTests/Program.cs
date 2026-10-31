using SephiriaOne;
using Mirror;
using UnityEngine;
using UO = UnityEngine.Object;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
PlayerAvatar New()
{
    var player = new PlayerAvatar();
    FriendlyFireParticipants.Receipts[player] = new() { Player = player };
    CombatMatchRuntime.Zombies.Add(player); return player;
}
void Reset()
{
    CombatRoleVisuals.Shutdown();
    NetworkServer.active = true; NetworkServer.spawned.Clear(); NetworkServer.Spawns = NetworkServer.Destroys = 0;
    NetworkServer.FailBeforeSpawn = NetworkServer.FailAfterSpawn = NetworkServer.FailDestroy = false; NetworkServer.DuringSpawn = null;
    UO.Created.Clear(); UO.FailInstantiate = false;
    CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.MatchToken++;
    FriendlyFireParticipants.Receipts.Clear();
    CombatRoleVisualNative.FailContract = CombatRoleVisualNative.FailUpdate = false; CombatRoleVisualNative.Validations = 0;
    Debug.Warnings = 0;
}
Reset();
var zombie = New(); var human = New(); CombatMatchRuntime.Zombies.Remove(human);
CombatRoleVisuals.Update(human); Check(NetworkServer.Spawns == 0, "Humans never get a carrier");
zombie.transform.position = new(12);
CombatRoleVisuals.Update(zombie);
var effect = UO.Created.Single();
Check(NetworkServer.Spawns == 1 && effect.Visual && !effect.enabled && effect.ServerStarts == 0, "Owned host startup suppressed but client visual starts");
effect.Destroy();
Check(effect.GameplayEnds == 0 && effect.Visual, "Owned marker rejects gameplay expiry instead of invoking native damage/end callbacks");
Check(ReferenceEquals(effect.NetworkTarget, zombie) && effect.NetworkAttacker == null && effect.NetworkCurrentStack == 0 &&
    effect.bonusDamageId == string.Empty && !effect.damageFxPrefab && effect.chimDamageEvent.IsNull, "Only target is transmitted; unused impact hooks cleared on clone");
effect.Frame(); Check(effect.ServerUpdates == 0, "Disabled clone cannot execute server update");
Check(CombatRoleVisualNative.Prefab.enabled && CombatRoleVisualNative.Prefab.damageFxPrefab &&
    !CombatRoleVisualNative.Prefab.chimDamageEvent.IsNull && CombatRoleVisualNative.Prefab.bonusDamageId == "CHIMDAMAGEBONUS", "Original registered Chim prefab remains unchanged for guests/real debuffs");
var ordinary = new CharacterDebuff_ChimDamage { NetworkTarget = human }; ordinary.OnStartServer(); ordinary.Frame();
ordinary.Destroy();
Check(ordinary.ServerStarts == 1 && ordinary.ServerUpdates == 1 && ordinary.GameplayEnds == 1 && ordinary.DamageApplications == 1 && ordinary.Sounds == 1,
    "Ordinary Chim keeps native startup, updates and damage/sound on expiry");
var poison = new CharacterDebuff_Poison(); poison.OnStartServer(); poison.Frame(); poison.Destroy();
Check(poison.ServerStarts == 1 && poison.ServerUpdates == 1 && poison.GameplayEnds == 1, "Ordinary Poison stays native too");
zombie.transform.position = new(25); zombie.IsDead = true; CombatRoleVisuals.Update(zombie);
Check(effect.transform.position.x == 25 && effect.Visual && NetworkServer.Spawns == 1, "Same indicator follows while dead/pending respawn");
zombie.IsDead = false; CombatRoleVisuals.Update(zombie);
Check(NetworkServer.Spawns == 1 && CombatRoleVisualNative.Validations == 1, "Respawn and stable updates reuse carrier and validation");
for (int i = 0; i < 10000; i++) CombatRoleVisuals.Update(zombie);
long allocated = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++) CombatRoleVisuals.Update(zombie);
long updateBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
Check(updateBytes == 0, $"Stable visual update allocates zero bytes (observed {updateBytes})");
NetworkServer.Destroy(effect.gameObject); CombatRoleVisuals.Update(zombie);
Check(NetworkServer.Spawns == 2 && UO.Created.Last().Visual, "Native network cleanup is reconciled with one replacement");
Check(CombatRoleVisualNative.Validations == 2, "Replacement revalidates current prefab registration");
effect = UO.Created.Last();
FriendlyFireParticipants.Receipts.Remove(zombie); CombatRoleVisuals.Update(zombie);
Check(!effect && NetworkServer.spawned.Count == 0, "Retired receipt destroys old carrier");
CombatMatchRuntime.Zombies.Remove(zombie);
FriendlyFireParticipants.Receipts[zombie] = new() { Player = zombie }; CombatRoleVisuals.Update(zombie);
Check(NetworkServer.Spawns == 2, "Reconnected player cannot inherit a prior role visual");

Reset(); zombie = New(); CombatRoleVisuals.Update(zombie); var second = New(); CombatRoleVisuals.Update(second);
CombatRoleVisuals.Clear(); CombatRoleVisuals.Clear();
Check(NetworkServer.spawned.Count == 0 && UO.Created.All(e => !e && !e.Visual), "Idempotent clear destroys all independent markers");
Check(UO.Created.All(e => e.GameplayEnds == 0 && e.DamageApplications == 0 && e.Sounds == 0), "Network cleanup never expires Chim or plays its impact sound");
ordinary.OnStartServer(); Check(ordinary.ServerStarts == 2, "Real Chim startup remains available after match clear");

foreach (string failure in new[] { "contract", "instantiate", "before spawn", "after spawn", "update" })
{
    Reset(); zombie = New();
    CombatRoleVisualNative.FailContract = failure == "contract"; UO.FailInstantiate = failure == "instantiate";
    NetworkServer.FailBeforeSpawn = failure == "before spawn"; NetworkServer.FailAfterSpawn = failure == "after spawn";
    CombatRoleVisualNative.FailUpdate = failure == "update";
    CombatRoleVisuals.Update(zombie);
    int attempts = NetworkServer.Spawns;
    for (int i = 0; i < 10; i++) CombatRoleVisuals.Update(zombie);
    Check(NetworkServer.Spawns == attempts && NetworkServer.spawned.Count == 0 && UO.Created.All(e => !e), "No retry or orphan after " + failure);
    Check(Debug.Warnings == 1 && CombatMatchRuntime.MatchToken != 0, "Visual failure is contained once without ending match: " + failure);
}

Reset(); zombie = New(); bool shutdownDeferred = false;
NetworkServer.DuringSpawn = e =>
{
    CombatMatchRuntime.MatchToken = 0;
    CombatRoleVisuals.Clear();
    e.Destroy();
    Check(e.GameplayEnds == 0, "Gameplay expiry stays guarded while reentrant spawn retirement is pending");
    Check(e && !e.enabled, "Reentrant stop defers destruction until spawn returns");
    try { CombatRoleVisuals.Shutdown(); } catch (InvalidOperationException) { shutdownDeferred = true; }
};
CombatRoleVisuals.Update(zombie);
Check(shutdownDeferred && UO.Created.Single().ServerStarts == 0 && NetworkServer.spawned.Count == 0 && !UO.Created.Single(),
    "Guard remains installed through reentrant unload; completed spawn is destroyed");
Reset(); zombie = New(); NetworkServer.DuringSpawn = _ => { CombatMatchRuntime.Zombies.Remove(zombie); };
CombatRoleVisuals.Update(zombie);
Check(NetworkServer.spawned.Count == 0 && !UO.Created.Single(), "Role loss inside spawn retires without explicit Clear callback");
Reset(); zombie = New(); NetworkServer.DuringSpawn = _ => { CombatRoleVisuals.Clear(); throw new Exception("callback failure"); };
CombatRoleVisuals.Update(zombie);
Check(NetworkServer.spawned.Count == 0 && !UO.Created.Single(), "Deferred clear survives exceptional spawn callback");
Reset(); zombie = New(); CombatRoleVisuals.Update(zombie); NetworkServer.active = false;
CombatRoleVisuals.Clear(); Check(!UO.Created.Single(), "Lost authority still removes host-local loop");
Reset(); zombie = New(); CombatRoleVisuals.Update(zombie); NetworkServer.FailDestroy = true;
CombatRoleVisuals.Clear(); Check(!UO.Created.Single(), "Exceptional native destruction still removes disabled host loop");
Reset(); ordinary.OnStartServer(); ordinary.Destroy();
Check(ordinary.ServerStarts == 3 && ordinary.GameplayEnds == 2 && ordinary.DamageApplications == 2 && ordinary.Sounds == 2,
    "Unload restores ordinary Chim startup and expiry");
Console.WriteLine($"Passed {checks} Zombie visual lifecycle/owned-hook checks; 0 bytes/10000 stable updates. Native guest transport checked separately.");
