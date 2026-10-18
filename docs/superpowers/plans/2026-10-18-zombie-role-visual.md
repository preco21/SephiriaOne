# Zombie role visual implementation plan

> Execute inline with regression checks; no deployment or game launch.

**Goal:** A sustained native infected indicator for every Zombie, visible to host
and unmodified guests, with no damage, stats, HUD status or debuff registration.

**Architecture:** Reuse a separately owned stock Poison network prefab solely as
a client presentation carrier. Suppress its owned server startup, disable its
host component's automatic Update, and manually execute the native client-only
visual update from the existing match loop. Stock clients instantiate the
original enabled prefab and follow the synchronized target. Destroy the network
object on role/session loss; never call ApplyDebuff or InitializeAndSpawn.

**Tech stack:** Existing Unity/Mirror, Harmony, native contract guards, C# fixtures.

## Evidence and alternatives

Native Poison has a silent, small `Poison_Charm` particle loop, no startup FX,
and independent server gameplay/client visual paths. Its stock asset ID is
1290287067. `SpriteFx`/`BuffFx` alone are local only. The spell cast FX RPCs own
shared casting handles and would interfere with normal casting, so do not use
them. Preserve original prefab/assets and use native spawn/destroy transport.
Exact assembly/asset evidence and live limitations belong in the feature doc.

## Tasks

- [x] Add owned-carrier tests and initially failing Zombie lifecycle tests: follow while dead or
  pending conversion, no server startup/update, unchanged ordinary Poison,
  deduplicated spawn, immediate cleanup, reentrant stop and failed spawn safety.
- [x] Implement `ZombieRoleVisuals` ownership and `ZombieVisualNative` audited
  native/prefab checks. Keep server clones disabled through destruction.
- [x] Integrate role creation/refresh and clearing into `CombatMatchZombie` and
  shutdown into the existing match feature. Do not add another update controller.
- [x] Validate stock initial/late observer payload, disabled-component behavior,
  no client gameplay, native destroy cleanup and compatibility failure handling.
- [x] Run non-deploying release, visual fixture, runtime, combat and installed
  compatibility checks. Record zero-allocation steady state and remaining live
  host/guest visual validation. Review, version and document; finish with the
  repository's authorized conventional commit and push workflow.

User has delegated effect choice and implementation. Keep existing mode controls
and defaults, normal poisoning, character appearance and PvE unchanged.

Completed as 0.65.0. Native/asset research and live limitations are recorded in
[Zombie role visual](../../zombie-role-visual.md). Independent read-only review
found no actionable issue. The runtime assertion failed before lifecycle wiring;
the completed adapter and integration suites pass. No deployment or game launch.
