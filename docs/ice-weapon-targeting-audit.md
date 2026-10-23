# Ice Weapon targeting recheck

Investigated 2026-10-24 against addon `0.92.0` and the installed game assembly.
The report concerns Deathmatch: an unidentified Ice Weapon effect remained near
its owner. This audit found **no confirmed missing hostile-player filter** in
the inspected family. Gameplay code and projectile settings are unchanged.

## Movement and activation are different paths

`FROST` (Ice Weapons / 얼음 무구) is an artifact category, not a promise that
every member automatically homes. The shipped scripts and serialized projectile
components distinguish the following behaviors, including converted fire variants:

| Artifact / native class | Native activation and movement | Existing PvP adapter |
| --- | --- | --- |
| Frozen Bow (`Charm_IceBow`, item 1247) | Native cast/charge and ready-arrow requirements; emitted ice/fire arrows automatically acquire homing targets. The bow body itself follows its owner. | Both cast and retrigger nearest-point searches use `OffensiveNearestPoint`; `Charm_IceBow` is in the audited `Bullet.Update` homing registry. |
| Voluspa (`Charm_IceSpear`, item 1137) | Ordinary and weapon-triggered launches use different searches. Ordinary spears travel horizontally at the selected target height; directional launches follow weapon aim. Neither prefab continuously homes. | Both `SearchTarget` and `SearchTargetByWeaponDirection` use shared hostility. The offense-only `IsInBattle` adapter permits activation around an eligible player. |
| Blizzard Hammer (`Charm_IceHammer`, item 1208) | Charged ordinary hammer activates on dash and selects its launch destination once. The scythe upgrade continually steers toward the owner's looking position, not an automatically selected enemy. Its resting artifact body follows the owner. | Launch `SearchTarget` is adapted. Scythe steering contains no separate hostile-faction filter to widen. |
| Scabbard of Exorcism (`Charm_AirSlash`, item 1014) | Charged weapon-swing activation, aimed along the native swing; ice/fire and full/mini projectiles are not homing. | Shared impact hostility applies; no automatic acquisition exists to patch. |
| Droplet of Plitvice (`Charm_IceSword` / `IceSword`, item 1248) | Another Frost relic triggers native MP/stock checks. Swords intentionally orbit the owner, then fly outward. | Existing `IceSword.Update` collision adapter admits hostile players during both motion phases. |
| Guillotine of Bitter Cold (`Charm_Guillotine`, item 1303) | Native nearby-target search creates a local blade effect at the selected target. | `FindTargetsInRange` uses shared hostility; damage enters the common PvP path. |

Frozen Bow's `IceArrow` and `IceArrow_Flame` have both
`isHomingTargetEnabled` and `autoDetectHomingTarget` enabled. In contrast,
`IceScytheBullet` and its fire variant enable homing **movement** but disable
automatic acquisition. `Charm_IceHammer.OnUpdate` writes
`NetworkAvatar.CurrentLookingPosition` into each active scythe's
`HomingTargetPos` every native 0.2 seconds; `BulletMoveModule_IceScythe.OnMove`
consumes that position. The server looking position comes from the player's
synchronized aim. This is manual steering even though a field is named homing.

Adding every Ice Weapon ID to an automatic-target registry would therefore not
be a justified repair: ordinary hammer/spear/slash prefabs do not enter that
branch, while making scythes auto-acquire would replace their native control.

## Deathmatch and changing participants

The family adapters already use `FriendlyFireRuntime.CanAffectTeam`, which checks
current friendly-fire policy, ownership/connection eligibility, participant
selection and `CombatMatchRuntime.AllowsPair`. No Ice-specific participant or
mode cache was found.

Deathmatch's effective friendly-fire policy is enabled during the valid active
match, not its preparation/countdown or after its deadline. Unlike Tournament's
current duel and Zombie Survival's opposing teams, Deathmatch adds no separate
pair exclusion. Play Tag and the other modes also use the shared pair policy.
An existing audited homing projectile rechecks that policy when retaining a
player target; death or lost eligibility drops the target, and native acquisition
can select again after revival/eligibility is restored. Native targetability,
range, height, cooldown, charge and aim prerequisites still apply.

## Projectile limits can resemble an activation failure

The [secondary emission controls](projectile-emission-controls.md) added in
`0.91.0` cover the Bow, Spear, Hammer and Scabbard bullet emitters. At an active
cap or exhausted creation-rate allowance, the new emission is skipped while
native charge, stock, loop and delay progression continues. The artifact's
owner-following or charged visual is separate from the emitted projectile.
Plitvice and Guillotine have creation-rate admission only.

For Hammer, denial skips the spawn-result configuration, `activeBullets.Add`
and `RpcCreateHammer` together, retaining the coroutine continuation. No admitted
projectile's target or movement is changed by these adapters. The installed-code
checks confirm this ordering, but they cannot prove a particular guest saw it.

The local host log inspected during this audit loaded addon `0.91.1` and had
both active-instance and creation-rate limiting enabled in a settings snapshot.
No Ice targeting/homing hook error was found in the reviewed log. This does not
establish that the log describes the reported event or that limiting caused it.

## Verification and evidence

- Added executable patched-fixture coverage for Frozen Bow acquisition and
  launch aim plus Hammer/Spear/Guillotine selectors with nine players, explicit
  selection, live pair changes, death/revival, connection retirement/rejoin and
  friendly-fire off/on. Tests also preserve manual scythe aim and non-homing
  projectile flags. These exercise the shared policy boundary; actual mode
  state machines are tested separately by the runtime suite.
- Added installed-assembly contracts for both spear launch searches, hammer
  launch/aim, Bow/Hammer/Scabbard activation, scythe movement, Plitvice outward
  motion and shared participant/mode-policy calls.
- Release build: no warnings/errors. Combat suite: 15,698 checks; runtime suite:
  14,193 checks. Full installed-game contracts passed. Projectile hook suite:
  468 checks in 59 cases; projectile runtime suite: 120 checks, no failures.
- No new per-frame/per-hit work, network messages, settings, deployment or
  gameplay-version change. No live Unity, Deathmatch or stock-guest reproduction
  was performed. The reported malfunction remains unconfirmed.

`Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`

Fresh ILSpy exports and UnityPy inspection of `sharedassets0.assets` were kept
outside the repository at `%TEMP%/sephiria-ice-targeting-20261024/`.
Selected charm component IDs: Bow 197287, Hammer 197288, Spear 203907,
Scabbard 195216, Plitvice 210149, Guillotine 194145. Bow bullet components
190853/190977 and scythe components 190836/190894 establish the distinct homing
flags. Metadata/asset inspection is not evidence of delivery to a live guest.

To resolve the remaining report, identify the artifact and upgrade (especially
Hammer versus its scythe upgrade), whether the stationary object is the held
artifact or an emitted attack, and whether a native activation occurred. In a
controlled repeat, compare the same setup with both projectile caps and creation
rates disabled, then enabled separately. Keep PvP participants/settings fixed.
An actual emitted Frozen Bow arrow that ignores a living eligible opponent is
a different failure from a resting Hammer body or an admission-denied launch.
