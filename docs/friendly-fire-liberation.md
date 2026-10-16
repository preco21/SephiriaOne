# Liberation projectile homing

Implemented: 2026-10-17 (Asia/Seoul), addon `0.60.3`.

Academy Magic Sword - Liberation's special-attack projectiles now acquire
eligible hostile players through the existing friendly-fire homing adapter.
Normal enemy targeting, projectile motion, MP use, damage and spawn cadence
remain native. No new option, preset field or guest installation is required.

## Native resource and identity evidence

Installed English localization maps `Weapon_Dagger_FreeParry_MP_Name` to
**Academy Magic Sword - Liberation**, and `WeaponAction_FuryMP` to **Liberation**.
The weapon prefab's `WeaponAddonCommon_ChangeWeaponAction` overrides `FURY` with
`Weapon_Dagger_FuryMP(SP)`, a `NewWeaponFireData_SpecialProjectile` that creates
`SpecialProjectile_FuryMP`. Its host emitter selects one of four bullet prefabs:

| Prefab | Bullet component path ID in `sharedassets0.assets` |
| --- | ---: |
| `FuryMPBullet_Physical` | 190822 |
| `FuryMPBullet_Fire` | 190891 |
| `FuryMPBullet_Ice` | 190803 |
| `FuryMPBullet_Lightning` | 190823 |

All four have native homing and automatic acquisition enabled, detection range
45, `BulletMoveModule_UniformVector2` and server-to-client
`NetworkTransformReliable` position replication. Native `Bullet.Update` acquires
targets on the server before movement; `OnMove` steers toward `HomingTargetPos`
and publishes `NetworkmoveAngle`. Stock clients already have these resources
and receive native movement; no custom homing state needs to be sent to them.

`WeaponSimple.CreateSpecialAttackProjectile` passes `DirectAttack` and
`Weapon_SpecialAttack` through fire data and the FuryMP emitter to the shared
bullet spawner. That damage ID is shared with unrelated weapon special attacks.
`ProjectileSpawner<Bullet>.Spawn` records `spawnedPrefabName = key.name` on each
spawn, so the adapter requires **both** this damage identity and one of the four
exact prefab names. It does not infer identity from the owner's current weapon,
Unity's clone name, or a cached eligibility flag.

## Shared policy and boundaries

The existing `FriendlyFireTargeting` / `FriendlyFireTargetingHooks` acquisition
and target-loss checks now include this narrow identity. They consult the same
live participant/connection and match-pair rules as spell and artifact homing,
including casual friendly fire, Deathmatch, Tournament and Play Tag. Off/zero,
selection changes, countdowns and disconnected owners/targets invalidate hostile
player targets using the native clear/reacquire branch.

Native range, target ranking, ignored/dead/untargetable candidates and enabled
homing flags are retained. Native enemies remain eligible. Hits use the existing
central friendly-fire damage and balancing paths. Unrelated special attacks,
arbitrary future prefabs, ordinary input aim and guest authority are unchanged.
No new hook, scene scan, update loop, packet, collection or persistent state is
introduced; the adapter reads existing managed fields during the native scan.

## Verification and remaining live checks

The executable regression failed before the fix when Liberation selected a
normal enemy instead of a nearer hostile player. All four variants now pass
acquisition/loss, enemy ranking, native gates, authority, connection/selection,
match-pair, unknown identity and once-only damage scaling cases. Warmed spell,
Liberation and automatic-caster targeting allocates zero bytes over 1,000 fixture
cycles. These are fixture measurements, not a Unity frame-time benchmark.

Release build passed with zero warnings/errors; 4,166 executable combat checks
and the full portable/installed-game contract suite passed, including the new
FuryMP/spawner identity and movement contracts and the 33-reference Harmony 2.9
API audit. Installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Native code and extracted asset data remain outside the repository.

Live Unity/guest behavior is not yet verified. With an unmodified guest, exercise
Liberation from both host and guest, compare nearby players with monsters, and
toggle friendly fire/selection while bullets are flying. Include a Tournament
pair transition, reconnect and another run. All builds used `-p:DeployMod=false`;
the task did not deploy or launch the game.
