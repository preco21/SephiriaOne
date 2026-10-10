# Offensive artifact hostility audit

Updated: 2026-10-11, addon `0.43.2`. This extends [friendly fire](friendly-fire.md)
using its existing host toggle and damage percentage; no additional setting,
preset field, asset or guest addon is introduced.

## Findings and reported cases

Three independent gates excluded players before the central damage hook:

1. Offensive selectors used native faction masks which consider players allies.
2. Some automatic effects required native `IsInBattle`. Nearby players do not
   make `PlayerBattleChecker` enter combat, even when addon friendly fire is on.
3. Native on-hit/parry callbacks can apply artifact damage synchronously inside
   the original hit. The existing recursion guard rejected these nested hits.

The reported bat, cloud and earring paths now use the shared hostility policy at
their offensive gates, retaining native cooldowns, geometry and prerequisites:

| User-facing effect | Installed identity | Relevant native paths |
| --- | --- | --- |
| Snow Mountain Long-eared Bat / 설산 큰귀박쥐 (also described as Big-Eared Bat) | Item 1293, `Charm_IceBat` | `OnUpdate`: battle gate, reverse faction selector, owned frostbite, native projectile RPC |
| Storm Cloud / 먹구름 (Dark Clouds) | `DARKCLOUD` category effect, `ComboEffect_DarkCloud` | `Update` battle gate; `UseCloudCoroutine.MoveNext` selection; `FireLightning` synchronous proc |
| Thunder's Earring / 썬더의 귀걸이 | Item 1239, `Charm_ElectricEarring` | `OnUpdate` battle gate and `SearchTarget` faction selector |

**The bat still requires frostbite applied by its owner.** Its native loop checks
`FROSTBITE` and `debuff.NetworkAttacker == NetworkAvatar`, plus its own range and
timer. There is no Ice Magic Book ownership test. Echo of the Glacier was another
blocked source of that frostbite; repairing its selection makes a non-book route
available without removing the bat's native prerequisite. Bat projectiles still
use the native RPC and central `ApplyDamage` impact path.

## Coverage and implementation

`FriendlyFireEffectHooks.Selectors` describes reverse faction checks. It now has
15 entries (including two separate spear selectors and two coroutine bodies).
`FriendlyFireArtifactHooks` groups combat readers, nearest-search callers and
synchronous damage calls. `FriendlyFireArtifacts` holds the small shared runtime
adapters. All use current policy and ownership rather than retained hostility.

| Path | Audited consumers / items |
| --- | --- |
| Added reverse faction selectors | Bat, Earring, Green Ink Bottle (`Charm_AttackChim`, 1132), Echo of the Glacier (1178), Trainee Duelist's/Duelist's Epaulette (`Charm_GrowthParry`, 1317/1318), Guillotine of Bitter Cold (1303), Blizzard Hammer (1208), Voluspa (`Charm_IceSpear`, 1137, ordinary and weapon-direction searches), `GreenBat` planet search |
| Existing reverse/mask selectors retained | Burn Ring, Phoenix Wing / Fire Feather, Flame Ground Meteor, Storm Cloud, Thunderous Steps, Fire Chakram |
| Offensive battle readers | Bat, Earring, Flame Ground Meteor, Voluspa, Storm Cloud, `GreenBat` |
| Forward hit filter | `DaggerGrowthBullet.HitCheck` for native growth/parry daggers |
| Scoped nearest-point calls | `Charm_GuardCounter.FireRipostelaser`, `Charm_RockElephant.SpawnFlag`, Frozen Bow (`Charm_IceBow`, 1247) `FireCastingServer` and `FireCoroutine.MoveNext`; seven calls in four methods |
| Artifact homing | Pallas's Cards (1172), Robe of Yearning (1184), Frozen Bow (1247), Ice Vine (1139), Phoenix Wing (1214), Ashen Planet (1242), Red Planet (1106), White Planet (1220) |
| Synchronous damage calls | `Charm_FrostiumRing.HandleAttackUnit`, `Charm_TheTyphoonSheetmusic.HandleAttackUnit`, `Charm_Reddew.SummonDueFromVictim`, `Charm_TuningForks.CreateAttack`, `Charm_EchoOfTheGlacier.CreateFrostbite`, `ComboEffect_DarkCloud.FireLightning` |
| Synchronous status spread | `Charm_AttackChim.HandleAddedDebuffOnTarget` preserves exact source/application admission when electric stacking damages a different nearby player |

The `Charm_FrostiumRing` script is shared by Ohia Lehua (1101, serialized damage
ID `Charm_FireDamageGround`), Lightning-Struck Branch (1141, `Charm_Electric`) and
Frostium Ring (1175, `Charm_FrostiumRing`). A script name alone does not reliably
identify a displayed item. The callsite wrapper covers those native variants
without a guessed damage-ID list.

Additional verified names: `Charm_GuardCounter` backs Riposte Blade Fragment
(1051, 리포스테 검 조각) and Riposte Staff Fragment (1288, 리포스테 봉 파편).
`Charm_RockElephant` is Rock Elephant (1246, 바위코끼리), `Charm_Reddew` is Red Mist
(1016, 붉은 이슬), and `Charm_TuningForks` is Tuning Fork (1117, 소리굽쇠).
The common `GreenBat` selector serves Blue (1015), Yellow (1105), Red (1106),
Sky Blue (1107), White (1220), Dark (1241) and Ashen (1242) Planet. Only Red,
White and Ashen have the inspected automatic homing projectile variants.

### Scope, safety and lifecycle

- Offensive battle readers return native combat state first. If it is false,
  only the server with positive enabled friendly fire may qualify through a
  living, active, targetable other player on the same floor within 10 units.
  The companion's owner is excluded. This never writes global `IsInBattle`;
  out-of-combat healing, movement and world-map travel retain their native checks.
- Scoped nearest searches call the original `SearchTargetNearestPoint` body and
  change only its faction mask. Native nearest ordering, self/dead/targetable
  checks and **squared-radius** parameter are retained. Unwrapped calls, including
  player input/aim assistance, remain native. A `finally` restores the scope.
- Homing eligibility reads current `Bullet.damageId` and requires `fromType=None`.
  The verified IDs are `Charm_PallasCard`, `Charm_NearMagicBullet`, `Charm_IceBow`,
  `Charm_IcicleVine`, `Charm_FireFeather`, `Charm_Planet_Gray`, `Charm_Planet_Red`,
  and `Charm_Planet_White`. Native homing selection retains all other filters.
  A living allied target is released when off/zero or ownership makes it invalid,
  using the native target-clear/reacquire branch. There is no pooled-bullet cache.
- Audited native `ApplyDamage` calls establish an exact `DamageInstance` receipt.
  One artifact proc layer is admitted from the original source or parrying victim
  inside a friendly hit. Another artifact layer, a reflection-origin artifact
  chain or an unrelated callback cannot borrow that permission. Artifact damage
  still uses the central scale once after its own defenses; native reflection
  and same-target immediate electric processing retain their separate bounds.
- Green Ink Bottle's spread receipt is source/target-specific and restored by
  `finally`. It admits immediate electric damage on the spread recipient without
  allowing recursive spread damage from an existing artifact/debuff layer.
- No native damage object, faction, stat, timer or item requirement is rewritten.
  Off/zero, owner immunity, safe areas, invulnerability, resistance and stock
  death/HP/status replication still use the existing boundaries. Stock guests
  need no new state, scripts or assets. Existing projectiles read current policy.
- Roster and current floor identities are read when needed. Departed/replaced
  avatars are not cached; reconnects and later runs need no artifact replay.
  Harmony unload removes all added consumers. Unknown native method shapes fail
  compatibility validation, and the friendly-fire installer removes its hooks.

### Performance

No new central update, scene search, combat packet or periodic reconciliation was
added. A false native battle check can perform a linear scan of the existing
connected-player roster at the six offensive consumers; native-true, off/zero,
guest and peace-mode paths return immediately. Generic nearest and homing loops
reuse the game's existing scan. Proc/nearest/debuff scopes use thread-local fields
and `try/finally`, without closures or per-hit collections. Startup IL validation
and registries allocate only during installation.

## Broader audit and deliberate boundaries

The installed source audit covered 207 `Charm_*` classes, `ComboEffect_*`,
`GreenBat`, bullet/impact modules and shared target utilities. Asset inspection
covered 362 charm components and 308 bullet components; a 1,743-node reference
walk followed artifact fire data, bullets and move/destroy/child-bullet paths.

- Generic collision/explosion modules, `IceGuillotine.OnGuillotineHit` and native
  flame-ground damage already reach the central damage rules. No extra selector
  repair was found in those paths. Water Spirit (`Charm_LakeSpirit`, 1296) had no
  additional faction/homing gap.
- Blizzard Hammer's manually aimed scythe has automatic target finding disabled;
  it is not added to the automatic homing list. Pallas enables homing at runtime;
  its asset flag alone would have missed it.
- No player-type exclusion was found in the inspected charm class source.
  `Charm_CritAndRanged` has a nearby-hostile conditional self-stat penalty, not an
  offensive target selector; it remains native. `UnitElemental_DarkCloud` and
  `UnitElemental_FlameSword` contain no additional logic in this build.
- `DungeonManager.SearchTargetForChainLightning`, `MoveModule_CrossbowMine` and
  `MoveModule_CrossbowMineHoming` contain separate weapon target assumptions. No
  inspected artifact reference path reached a chain-enabled bullet or those mine
  modules; these weapon-specific selectors remain unchanged. General weapons,
  arbitrary future/modded bullets and every spell are not implicitly covered.
- Supporting battle readers such as peaceful movement, MP-loss buffs and
  MP-consumption shields are deliberately not changed.

The asset walk does not prove completeness across arbitrary intermediary types
or cross-file references. One unrelated stat-only `Charm_3Elemental_ByRow`
component could not be parsed; no targeting claim is based on that component.
These findings describe this installed build, not all future items or addons.

## Evidence and verification

Installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Native source, asset names and EN/KO localization were inspected locally; game
binaries, decompiled source and extracted assets are not committed.

Executable fixture regressions remove the Ice Bat and Dark Cloud patches
individually to reproduce their blocked player-selection/combat behavior, then
pass after restoring the hooks. Coverage exercises off/on/zero, host/guest,
owner exclusion, monsters, departure/replacement, delayed selectors, squared
range, pooled identities, live homing loss, 25/100/200% proc damage, recursion and
exception cleanup. Warm battle, nearest and homing checks each allocate zero
bytes over 1,000 fixture cycles; this is not a Unity FPS measurement.

Installed-game IL tests validate every listed method/call count and the bat's
owned-frostbite/projectile path, including rejection after critical anchors are
removed. They do not simulate actual Unity/Mirror clients.

Final verification: Debug and Release builds passed without warnings/errors;
603 executable combat checks, 1,447 runtime integration checks, 69 Bat lifecycle
checks, 22 panel checks and the full portable/installed-game contract suite
(including 3,085 catalog checks) passed with `-p:DeployMod=false`. The shared
runtime performance probe retained zero bytes/tick in unchanged five-player
cases. Independent review found one test-fixture boundary mismatch, corrected
against native source and verified before completion; no production defect was
identified by that review.

**Remaining live checks:** With an unmodified guest, verify all three reported
effects with no magic book, using owner-applied frostbite for the bat (including
Echo of the Glacier). Compare with monsters and friendly fire off/zero. Exercise
parry, ink/electric spread, artifact homing and on-hit procs at 25/100/200%; toggle
while projectiles are flying, quit/rejoin, and start a second run. Confirm guest
FX and damage, preserved native safe areas, and unchanged normal healing/travel.
No game launch or deployment was performed for this change.

## Extending coverage

Start from the displayed asset and follow its native source, server damage and
RPC path. Determine whether it needs a selector, combat reader, scoped utility,
homing identity or synchronous proc entry. Add only the required registry entry
and validate its actual IL shape and native prerequisites. Add an executable
case with off/zero, stock guest authority, owner immunity, enemy behavior and
exception/pool/recursive cases as applicable. Avoid globally changing factions,
opening the nested-hit guard, or inferring an item from its script name alone.
