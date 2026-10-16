# Weapon and artifact projectile hostility audit

Implemented 2026-10-17 (Asia/Seoul), addon `0.60.5`.

This extends the [Liberation fix](friendly-fire-liberation.md) and existing
[artifact adapters](friendly-fire-artifacts.md). The Evasion cap is a separate
damage-resolution rule and cannot repair projectile target acquisition.

## Confirmed missing homing identities

These attacks already have native homing. Their player exclusion came from the
shared `Bullet.Update` faction filter not recognizing their damage/prefab identity.
They now use the existing friendly-fire acquisition and target-loss policy.

| Installed weapon/artifact | Native projectile | Required damage identity |
| --- | --- | --- |
| Colossal Crossbow: Dual Rails / 대형 석궁: 이중 레일 | `CrossbowArrow_Special_BigHoming` | DirectAttack, Weapon_SpecialAttack |
| Formless Dance | `ThrowingSpear_Homing` | DirectAttack, Weapon_BasicAttack |
| Gaze of Flames, Breath of Ice, Wings of Lightning | `MagicalMissile_SweepBullet_Fire`, `_Ice`, `_Lightning` | DirectAttack, Weapon_SpecialAttack |
| Magic Wand, Prismatic Magic Wand | `MagicalMissile_SweepBullet` and its Fire/Ice/Lightning/Melee/Chaos variants | DirectAttack, Weapon_SpecialAttack |
| Dark Veil | `ShurikenWeapon_Comp_Last`; `ShurikenWeapon_Comp` | DirectAttack, Weapon_BasicAttack; Weapon_DashAttack respectively |
| Colossal Crossbow: Equipped with Guidance Module / 대형 석궁: 유도 모듈 장착 | `CrossbowMine_Drone`, `CrossbowMine_DroneEnhanced` | DirectAttack, Weapon_SpecialAttack |
| Adamah's Oath | `MagicBlade` | Magic, KatanaMagicBlade |
| Enhanced Mini Ballista shots | `MiniBallistaArrow` | DirectAttack, BasicAttack |

Four Liberation/FuryMP prefabs retain their previous matching. Weapon IDs are
generic, so **both the exact prefab key and damage identity** must match.
`ProjectileSpawner<Bullet>.Spawn` assigns `spawnedPrefabName` from the original
prefab key on each spawn, after initialization. Reading current bullet identity
avoids guessing from an owner's equipment after they switch weapons.

The Mini Ballista arrow starts with homing disabled. Native
`Unit_MiniBallista.Attack` enables it only with `ENHANCEDMINIBALLISTA`, after a
0.125-second delay. The addon retains this gate, and the summon uses the existing
companion owner/participant rules. It cannot acquire its own player owner.

## Separate selectors repaired

- `WeaponSimple_Katana.OnCreateMagicServerside`: Adamah's Oath initially searches
  near the aiming point and calls `SetHomingTarget`. The scoped nearest-point
  adapter preserves its native squared radius of 5 and `useMagicBlade` gate.
- `WeaponSimple_SwordAndShield.Update`: Solis Missio's automatic Flame Sword has
  its own nearest search in addition to `ComboEffect_FlameSword.SearchTarget`.
  Keep the native host check, enabled weapon feature, guard, active-time/stack,
  combo, interval and configured distance requirements.
- `WeaponAddonKatana_Nastrond.SearchTarget`: Fragment of Nastrond's secondary
  explosion selects a target through a reverse faction mask. Reuse `ItemTarget`;
  preserve overlap bounds, costume hitbox resolution, nearest ordering, charging
  requirements and native explosion/RPC creation. This is auto-targeting, not a
  newly homing projectile.
- `BulletMoveModule_CrossbowMine.OnMove` and
  `BulletMoveModule_CrossbowMineHoming.OnMove`: native proximity checks use a
  faction mask cached by `SetMotionData`. The adapter keeps that mask for native
  targets and checks current PvP eligibility for each player/companion candidate.
  Plain, Fire and Poison mines use **Weapon_DashAttack**; Drone and DroneEnhanced
  use **Weapon_SpecialAttack**. Ordinary crossbow dash mines gain correct player
  proximity triggers, not homing. Keep the 0.2-second native polling cadence,
  ground/height, active-object, life, targetability, distance, and explosion/stop
  rules. Existing committed motion/pathfinding and explosion state remain native;
  the shared damage boundary separately rechecks permission at impact.

`FriendlyFireWeaponHooks` holds the five audited selector entries.
`FriendlyFireProjectileIdentity` holds the exact projectile identities.
All adapters delegate to existing player/companion ownership, live connection
receipts, selection, damage-scale and match-pair rules. No new polling loop,
state cache, session setting, faction mutation or network message is introduced.
Patch installation validates exact IL shapes and uses the existing rollback on
failure. The adapters are installed with the other friendly-fire hooks.

## Multiplayer, lifetimes and performance

Native `Bullet.Update` acquires targets on the server before movement. All newly
admitted bullet prefabs use `BulletMoveModule_UniformVector2` except the two
guidance mines, which use `BulletMoveModule_CrossbowMineHoming`. Those movers
already synchronize direction/velocity. Native spawning, collision, HP and
despawn transport remain in control, so guests need no addon or new assets.

Player eligibility is rechecked on acquisition/loss and mine proximity checks.
Off/zero damage, selection changes, inactive match pairs/countdowns, disconnected
owners/targets and companion-owner protection use the shared policy. Existing
Deathmatch, Tournament and Play Tag semantics therefore apply automatically.
Native enemy ranking/range, ignored targets, untargetability, homing prerequisites
and attack balance remain unchanged. No live player stats are modified.

New identity checks use constant string comparisons without allocations.
Existing native searches are reused; no extra creature scans are added. Warmed
mine proximity checks and shared homing paths allocate zero bytes in executable
fixtures. This is not a Unity profiler or live guest performance measurement.

## Audit scope and exclusions

Evidence: installed `Assembly-CSharp.dll` fingerprint recorded in
[domain findings](domain-findings.md), decompiled native methods, and
`resources.assets` / `sharedassets0.assets` references. Weapon resource traversal
includes `NewWeaponFireData` subclasses, elemental sets, enhanced attacks and
secondary emitters. Runtime `TurnOnHoming` / `SetHomingTarget` callers were checked
too, which is necessary to find Mini Ballista's conditional homing.

Existing adapters already cover Bolt/Frost Dagger/Arrow Rain, automatic Bolt
casters, chain lightning, Pallas/near-magic bullets, Frozen Bow, Ice Vine,
Phoenix Wing, three planet families and Flame Sword/Plitvice paths. No additional
confirmed missing artifact homing identity was found beyond Mini Ballista.

Do not infer completeness from asset traversal alone: nine combo/artifact
component trees failed parsing, and thirteen non-artifact/null item references
could not be validated. Cached item references were used only when matching ID,
localization key and a unique pointer byte sequence in the installed resource;
malformed generated typetrees were not trusted. Native combo/caller inspection
supplements the graph. Private extracted assets/source stay outside Git.

Intentionally unchanged:

- Monster-only projectiles without an active playable weapon/artifact path.
- Blizzard Hammer's Ice Scythe: automatic acquisition is disabled in its prefab;
  do not turn a manually directed effect into automatic homing.
- `SpecialProjectile_PlanetaryZone` and `WeaponAddonDagger_FlameSwordThrow`: native
  search implementations exist, but this audit found no active playable resource
  consumer. Keep them native until such a path is verified.
- Drifting Ash's automatic melee dash/movement is outside projectile homing.
- Global player input aim, healing, buffs and unrelated projectile identities.

## Verification and live follow-up

Non-deploying Release build (zero warnings/errors), 4,858 executable combat checks,
the portable suite and installed-game IL contracts pass. Coverage includes every added homing identity, native enemy
ranking, pool reuse, disabled homing, off/zero, connection/selection/mode changes,
companion ownership, mine masks/range/readiness, changed-IL rejection and native
movement/emission contracts. Tests execute production Harmony rewrites against
fixtures; native contracts inspect the installed assembly separately.

Still requires a host plus unmodified guest playtest: exercise one attack from
each table group, enhanced Mini Ballista, Solis Missio, Nastrond and all three dash
mine variants. Verify both host- and guest-owned attacks acquire eligible players
and move visibly on both clients. Toggle off, change selection, advance Tournament
pairs and reconnect while projectiles are alive; verify damage permission changes
immediately and native enemies remain targetable. No game launch or deployment
was performed for this change.
