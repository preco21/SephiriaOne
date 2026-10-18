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
    ZombieRoleVisuals.Shutdown();
    NetworkServer.active = true; NetworkServer.spawned.Clear(); NetworkServer.Spawns = NetworkServer.Destroys = 0;
    NetworkServer.FailBeforeSpawn = NetworkServer.FailAfterSpawn = NetworkServer.FailDestroy = false; NetworkServer.DuringSpawn = null;
    UO.Created.Clear(); UO.FailInstantiate = false;
    CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.MatchToken++;
    FriendlyFireParticipants.Receipts.Clear();
    ZombieVisualNative.FailContract = ZombieVisualNative.FailUpdate = false; ZombieVisualNative.Validations = 0;
    Debug.Warnings = 0;
}
Reset();
var zombie = New(); var human = New(); CombatMatchRuntime.Zombies.Remove(human);
ZombieRoleVisuals.Update(human); Check(NetworkServer.Spawns == 0, "Humans never get a carrier");
zombie.transform.position = new(12);
ZombieRoleVisuals.Update(zombie);
var effect = UO.Created.Single();
Check(NetworkServer.Spawns == 1 && effect.Visual && !effect.enabled && effect.ServerStarts == 0, "Owned host startup suppressed but client visual starts");
Check(ReferenceEquals(effect.NetworkTarget, zombie) && effect.NetworkAttacker == null && effect.NetworkCurrentStack == 0 &&
    effect.damage == 0 && effect.debuffResistDecrease == 0, "Only target is transmitted; no gameplay attribution or damage");
effect.Frame(); Check(effect.ServerUpdates == 0, "Disabled clone cannot execute server update");
Check(ZombieVisualNative.Prefab.enabled && ZombieVisualNative.Prefab.damage == 20, "Original registered prefab remains unchanged for guests/real poison");
var ordinary = new CharacterDebuff_Poison(); ordinary.OnStartServer(); ordinary.Frame();
Check(ordinary.ServerStarts == 1 && ordinary.ServerUpdates == 1, "Ordinary Poison is untouched by owned prefix");
zombie.transform.position = new(25); zombie.IsDead = true; ZombieRoleVisuals.Update(zombie);
Check(effect.transform.position.x == 25 && effect.Visual && NetworkServer.Spawns == 1, "Same indicator follows while dead/pending respawn");
zombie.IsDead = false; ZombieRoleVisuals.Update(zombie);
Check(NetworkServer.Spawns == 1 && ZombieVisualNative.Validations == 1, "Respawn and stable updates reuse carrier and validation");
for (int i = 0; i < 10000; i++) ZombieRoleVisuals.Update(zombie);
long allocated = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++) ZombieRoleVisuals.Update(zombie);
long updateBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
Check(updateBytes == 0, $"Stable visual update allocates zero bytes (observed {updateBytes})");
NetworkServer.Destroy(effect.gameObject); ZombieRoleVisuals.Update(zombie);
Check(NetworkServer.Spawns == 2 && UO.Created.Last().Visual, "Native network cleanup is reconciled with one replacement");
Check(ZombieVisualNative.Validations == 2, "Replacement revalidates current prefab registration");
effect = UO.Created.Last();
FriendlyFireParticipants.Receipts.Remove(zombie); ZombieRoleVisuals.Update(zombie);
Check(!effect && NetworkServer.spawned.Count == 0, "Retired receipt destroys old carrier");
CombatMatchRuntime.Zombies.Remove(zombie);
FriendlyFireParticipants.Receipts[zombie] = new() { Player = zombie }; ZombieRoleVisuals.Update(zombie);
Check(NetworkServer.Spawns == 2, "Reconnected player cannot inherit a prior role visual");

Reset(); zombie = New(); ZombieRoleVisuals.Update(zombie); var second = New(); ZombieRoleVisuals.Update(second);
ZombieRoleVisuals.Clear(); ZombieRoleVisuals.Clear();
Check(NetworkServer.spawned.Count == 0 && UO.Created.All(e => !e && !e.Visual), "Idempotent clear destroys all independent markers");
ordinary.OnStartServer(); Check(ordinary.ServerStarts == 2, "Real Poison startup remains available after match clear");

foreach (string failure in new[] { "contract", "instantiate", "before spawn", "after spawn", "update" })
{
    Reset(); zombie = New();
    ZombieVisualNative.FailContract = failure == "contract"; UO.FailInstantiate = failure == "instantiate";
    NetworkServer.FailBeforeSpawn = failure == "before spawn"; NetworkServer.FailAfterSpawn = failure == "after spawn";
    ZombieVisualNative.FailUpdate = failure == "update";
    ZombieRoleVisuals.Update(zombie);
    int attempts = NetworkServer.Spawns;
    for (int i = 0; i < 10; i++) ZombieRoleVisuals.Update(zombie);
    Check(NetworkServer.Spawns == attempts && NetworkServer.spawned.Count == 0 && UO.Created.All(e => !e), "No retry or orphan after " + failure);
    Check(Debug.Warnings == 1 && CombatMatchRuntime.MatchToken != 0, "Visual failure is contained once without ending match: " + failure);
}

Reset(); zombie = New(); bool shutdownDeferred = false;
NetworkServer.DuringSpawn = e =>
{
    CombatMatchRuntime.MatchToken = 0;
    ZombieRoleVisuals.Clear();
    Check(e && !e.enabled, "Reentrant stop defers destruction until spawn returns");
    try { ZombieRoleVisuals.Shutdown(); } catch (InvalidOperationException) { shutdownDeferred = true; }
};
ZombieRoleVisuals.Update(zombie);
Check(shutdownDeferred && UO.Created.Single().ServerStarts == 0 && NetworkServer.spawned.Count == 0 && !UO.Created.Single(),
    "Guard remains installed through reentrant unload; completed spawn is destroyed");
Reset(); zombie = New(); NetworkServer.DuringSpawn = _ => { CombatMatchRuntime.Zombies.Remove(zombie); };
ZombieRoleVisuals.Update(zombie);
Check(NetworkServer.spawned.Count == 0 && !UO.Created.Single(), "Role loss inside spawn retires without explicit Clear callback");
Reset(); zombie = New(); NetworkServer.DuringSpawn = _ => { ZombieRoleVisuals.Clear(); throw new Exception("callback failure"); };
ZombieRoleVisuals.Update(zombie);
Check(NetworkServer.spawned.Count == 0 && !UO.Created.Single(), "Deferred clear survives exceptional spawn callback");
Reset(); zombie = New(); ZombieRoleVisuals.Update(zombie); NetworkServer.active = false;
ZombieRoleVisuals.Clear(); Check(!UO.Created.Single(), "Lost authority still removes host-local loop");
Reset(); zombie = New(); ZombieRoleVisuals.Update(zombie); NetworkServer.FailDestroy = true;
ZombieRoleVisuals.Clear(); Check(!UO.Created.Single(), "Exceptional native destruction still removes disabled host loop");
Reset(); ordinary.OnStartServer(); Check(ordinary.ServerStarts == 3, "Unload restores original startup");
Console.WriteLine($"Passed {checks} Zombie visual lifecycle/owned-hook checks; 0 bytes/10000 stable updates. Native guest transport checked separately.");
