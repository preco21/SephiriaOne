# Offensive artifact hostility audit

## Autonomous damage cadence (`0.86.0`, 2026-10-22)

The new optional [autonomous artifact limit](pvp-autonomous-artifacts.md) applies
a per-source/opponent hard interval to seven audited emitters and composes with
Dark Cloud's existing strike clock. It retains the targeting and native PvE
paths documented below; secondary DoT controls remain separate.

Updated: 2026-10-16, addon `0.59.1`. This extends [friendly fire](friendly-fire.md)
using its existing host toggle and damage percentage; no additional setting,
preset field, asset or guest addon is introduced.

Follow-up (`0.47.1`, 2026-10-12): [Bolt and shared spell targeting](friendly-fire-spells.md)
extends the audited homing and offensive-search scope. Common nearest/homing
helpers now live in `FriendlyFireTargeting` / `FriendlyFireTargetingHooks`; the
artifact registry and bounded proc scopes retain their original behavior. The
spell exclusions below describe the earlier `0.43.2` audit boundary.

Follow-up (`0.60.3`, 2026-10-17): [Liberation](friendly-fire-liberation.md) extends
the same homing adapter to four audited weapon-special bullet prefabs. General
weapon homing remains outside the original artifact audit below.

## Recheck against the local installation (2026-10-16)

The repeated Plitvice report was investigated against repository `0.60.0`.
The local `AddOns/SephiriaOne/metadata.json` still reports `0.57.0`, the installed
DLL assembly version is `0.57.0.0`, and the latest local `Player.log` records
`[SephiriaOne] Loaded v0.57.0`. That installation predates the Plitvice and Flame
Sword fixes in `0.59.1` (`b1d6670`). Repository changes and non-deploying builds do
not update the game's installed addon. Use `0.59.1` or newer and fully restart
the game to activate those hooks; the current build is `0.60.0`. This recheck did
not deploy or launch the game.

The installed game assembly fingerprint still matches the audited build.
Rechecked `IceSword.Update`, its parent creation/server flag/MP damage path,
`ComboEffect_FlameSword.SearchTarget`, auto-sword stock restoration, falling and
return swords, secondary melee creation, and related Flame/Ember consumers.
The narrow collision/selection adapters remain installed in the current source;
no further player-filter defect was identified in these inspected paths. Native
bonus-MP damage, collision overlap, trigger requirements and hit cooldowns remain
intentional prerequisites, as described below.

An additional similarly named mask in `FanaticFlame.Update` belongs to
`Unit_Tentacle`'s `FanaticFlameCreator` path in the inspected source. It applies a
native ownerless damage buff and was not found on an audited artifact path;
broadly rewriting it would affect monster behavior and lose player attribution.
It remains unchanged, like the previously excluded monster `SwordSphere`.

Fresh verification passed: 3,995 executable combat checks, including Plitvice
damage/FX/cadence/participant/authority and Flame Sword selection cases; the full
installed-game contract suite, including secondary swords; and the 33-reference
Harmony 2.9 API compatibility audit. This adds deployment evidence and a source
recheck, not a claim of live Unity/stock-guest reproduction. No gameplay code
change was needed beyond the existing `0.59.1` fixes.

## Plitvice and secondary swords (0.59.1, 2026-10-16)

The reported “Drops of Water in Plitvice Lakes” is item **1248**, installed EN
name **Droplet of Plitvice**, KO **플리트비체의 물방울**, localization key
`Item_FrostMP_Name`. Its `resources.assets` ItemEntity references GameObject
68023 in `sharedassets0.assets`, with `Charm_IceSword` component 210149 and
`iceSwordPrefab` pointing to `IceSword` component 198238. The disabled 1261
variant shares its name; it is not the playable item used to establish this path.

`Charm_IceSword.OnFireFrostRelicGlobal` pays native MP and sends
`RpcCreateSwords`; each client's receiver creates the visual sword, with only
the host copy receiving `isServerObject = true`. `IceSword.Update` handles both
orbiting contact and the final outward flight. It resolves costume hitboxes via
`Hitbox.GetCombatBehaviour(0)`, but rejected player targets in an inline reverse
faction-mask check **before** calling the existing damage handler. That explains
why the visible spawned sword could fail despite ordinary friendly fire working.

`FriendlyFireArtifactHooks.RewriteIceSword` validates that exact owner/mask
instruction sequence and substitutes the shared `ItemTarget` policy. Native
non-unit collisions, radius, hit list, 0.1-second check timer, 0.5-second per-target
interval, MP-based damage calculation, Ice/Fire conversion, `FROSTRELIC` flag,
successful-hit FX/audio RPC and lifecycle stay native. A character without the
native bonus-MP damage input can still produce zero sword damage; this change
does not manufacture a new minimum damage or waive artifact prerequisites.

The related `ComboEffect_FlameSword.SearchTarget` had a second unhandled reverse
faction filter. It now belongs to the existing offensive selector registry (16
entries). Its auto-search coroutine retains the native 5-unit search, random
selection, targetability/height/death checks, sword stock and delays. Committed
swings still keep their native positions; central impact handling rechecks live
permission, so disabling friendly fire or changing participants blocks damage
even from already launched attacks.

Both changes share host authority, positive damage, current participant receipts,
owner immunity, Tournament opponents and the common damage/balancing/KDA path.
They apply to casual friendly fire, Deathmatch, Tournament and Play Tag without
new settings, cached targets, per-frame scans, packets, guest scripts or assets.
There is no new per-hit allocation. Unknown IL shapes use existing installation
rollback; no global faction, battle-state or creature-enumeration patch was added.

### Related artifact audit

`EMBER` (displayed **Ember**) and `FLAMESWORD` are distinct categories. This
follow-up inspected both and the related `FROST` artifacts using installed
ItemEntity-to-prefab references, serialized settings and native source. The old
Charm/Bullet-only reference walk below did not cover `IceSword` or the Flame
Sword combo; its breadth must not be treated as proof of every secondary effect.

| Items / path | Finding |
| --- | --- |
| Plitvice, including Eternal Eclipse's Fire conversion | Fixed the shared `IceSword.Update` collision veto. Both elements use the same native damage path. |
| Flame Sword category: Eternal Furnace, Meteorite Earrings/Pauldrons, Midday Whetstone, Sun of the Archives, Meteorite Mirror and the stat-only Solis/Anvil/Sunset items | Fixed combo auto-search. Ordinary and lucky strikes create native `MeleeCollision` after a delay. `FlameSwordReturn.Update` and `SpecialProjectile_FlameSword` already enter shared damage through direct or child melee hits, with no extra player-selection veto. |
| Ember: Red Snake Eyes, Phoenix Wing | Existing meteor/feather selectors, meteor combat gate and feather homing ID already cover hostile players; native projectile impacts and feather burn use shared damage/debuff handling. |
| Ember: Ohia Lehua | Its serialized class is `Charm_FrostiumRing`, damage ID `Charm_FireDamageGround`; already covered by the bounded synchronous proc adapter. |
| Ember: Flame Herb Root, Ruby Brooch, Red Planet Observation Log | Root's FURY-triggered child bullet reaches ordinary bullet collision; Brooch's cloud callback uses shared debuff handling. Planet sets `PLANETIGNITEDAMAGE`, consumed by `GreenBat` with existing battle/selector/homing coverage, including ignite-tail bullets. Native trigger requirements remain. |
| Ember stat/debuff items: Dragoncense, Oak Tree Coal, Ball of Red Yarn, Flaming Eye Patch, Magma Bead, Flame Bug, Scale of Conflict, Icebound Tejas, Plasma Helmet, Blacksmith's Tongs | Serialized stats or native on-hit modifiers/debuffs, not independent target searches. Existing damage/debuff policy and configurable DoT timing apply. |
| Ember magic books: Fire Bolt, Fire Arrow, Fire Circus, Meteor Shower | Existing spell audit covers their native auto-search/homing paths; Fire Arrow is not granted new homing. Tero's Spirit Powder uses existing companion owner/target rules. |
| Combustible Oil / flame-ground consumers | Hidden item inspected separately. Native flame-ground collection and server damage already call central `ApplyDamage`; no extra faction veto was found. |
| Related Frost: Scabbard of Exorcism, Voluspa, Blizzard Hammer, Frozen Bow, Guillotine of Bitter Cold | Existing selector/nearest adapters and native bullet/melee/`IceGuillotine.OnGuillotineHit` paths cover them. Manual scythe aim remains manual. Tiny Magic Conch, Frozen Heart, Ice Star and Ice Wings alter stats/charging/visuals rather than adding another enemy filter. |

No additional filter change was needed in those inspected paths. Native
`SwordSphere` has a similar mask, but belongs to monster AI, not these artifact
prefabs; it remains untouched. Arbitrary future/modded secondary effects are
not covered by inference. The assembly fingerprint below was rechecked unchanged.

### Verification and remaining live checks

The executable regression first reproduced the blocked Plitvice hit, then passed
with the narrow adapter. Tests cover successful-hit FX, single damage scaling,
repeat-hit cadence, off/zero, guest authority, selection/match restrictions,
retired/rejoined connections, self/monster targets and native target prerequisites.
Installed-game contracts verify the actual sword creation/server flag, collision,
outward-flight branch, faction equivalence, MP cost, RPCs and downstream Flame
Sword/Guillotine hits, rejecting removed owner/mask anchors and duplicate filters.

Final checks: Release build passed with zero warnings/errors; 3,900 executable
combat checks, 2,254 runtime/session checks plus 69 Bat lifecycle checks, and the
full catalog/installed-game contract suite passed with deployment disabled.
The compiled addon also passed the co-installed Harmony 2.9 API compatibility
check. Independent read-only review reported no actionable findings.

Live Unity/Mirror testing remains necessary: host and unmodified guest should
each equip Plitvice with enough native bonus MP, activate a Frost relic, and test
orbit/outward hits, Fire conversion, Flame Sword search/returns and mid-flight
toggle/selection changes. Verify remote HP, hit FX/audio and unchanged monster
damage. Nothing was deployed or launched for this change.

## Dark Cloud player strike limit (0.51.2, 2026-10-13)

Follow-up (`0.52.0`): this limit now requires [PvP balancing](pvp-balancing.md),
an off-by-default host toggle. Balance edits invalidate its cooldown revision.
The behavior below describes the limit while balancing is on.

Follow-up (`0.55.2`, 2026-10-15): the strike cooldown is reduced from 0.5 to
0.25 seconds. Shared pair ownership, balance gating and lifecycle resets are
unchanged; the separate damage-budget refill still defaults to 70% HP/s.

Follow-up (`0.62.1`, 2026-10-18): the requested cooldown is now **0.5 seconds**.
This changes only the existing balanced PvP strike gate. Regression tests reject
0.25s and 0.499s attempts, admit exactly 0.5s, and reject another same-frame
attempt; damage, strike-triggered Burn and FX are suppressed together. Blocked
attempts do not extend the cooldown. The queued-owner-death and exception-retry
checks use the new interval. Other debuff intervals and budget settings are unchanged.
The new 0.25s rejection test failed with the old constant, then all 4,860 combat
fixture checks passed with the new value. Release build passed with zero warnings
or errors and deployment disabled; live gameplay was not tested.

During enabled, positive-damage friendly fire, the host permits at most one Dark
Cloud strike attempt per attacker-player/victim-player pair every **0.5 seconds**
of scaled game time. It uses the same selection, connection and Tournament-pair
policy as other friendly-fire effects. Casual friendly fire, Deathmatch and
Tournament share this limit; clients need no addon or new assets.

The installed game's `UseCloudCoroutine` issues multishots with a 0.133-second
wait; attack-speed and extra-discharge paths can also increase strike frequency.
That coroutine and targeted staff discharges (`UseCloudToTarget`) reach
`FireLightning(UnitAvatar, int)`. Its player-combo branch calls `ApplyDamage`,
then `OnAttackUnit`, then `TargetCreateFx`. Blazing Stormcloud subscribes to that
event to apply burn **even when damage was rejected**, so limiting only damage
would leave rapid burn applications active.

`FriendlyFireDarkCloud` prefixes this common strike boundary. Repeated strikes
inside the window skip damage, the strike callback and lightning FX together.
Admitted strikes keep their native damage, existing friendly-fire scaling and
callbacks. The native caller still handles cloud costs, random target choice,
`DARKCLOUDFIRED` notifications and its own timer. There is no refund or retargeting.
Existing damage-over-time ticks from an admitted strike are not separately
throttled. Native enemy targets, non-player combos, and off/zero settings keep
their previous paths and central impact guards.

The native coroutine can continue after its owner dies: its loop drops dead
targets but does not stop for owner death. Queued strikes from a current dead
owner therefore retain the same cap. The window is reserved before entering
native callbacks, so recursion and failed strikes cannot immediately retry.
Separate attackers/targets have independent windows; replacing a cloud component
does not reset its owner's window.

Storage uses weak attacker/victim connection receipts. Current participant and
KDA epochs invalidate windows on selection/match changes, score resets and
session transitions; new connection receipts, changed floor identities and a
reset game clock also start fresh. Unload clears the table. Before capturing the
KDA epoch, the hook aligns score enablement with the shared impact path, avoiding
a first-hit race after preset inheritance. No update loop, extra network packet
or persistent setting is introduced. Warmed admitted/rejected checks allocate
zero bytes in the executable fixture; this is not a Unity FPS measurement.

Compatibility validation checks the exact method signature and native owner,
player-combo, damage/event/FX anchors and ordering. `Networkavatar` and
`isPlayerCombo` must be resolved from their declaring `ComboEffectBase` type,
matching the installed IL's member identity. Changed contracts follow existing
friendly-fire installation rollback instead of leaving partial hooks active.

Executable coverage includes independent pairs, 0.5-second boundaries, burn/FX,
death during a burst, ordinary enemies, off/zero, client execution, selection,
Tournament exclusion, reconnect/run/floor/epoch changes, component pooling,
recursive callbacks, exceptions and warmed allocations. Installed-game checks
verify both native entry paths and reject missing/reordered anchors. The game
assembly fingerprint remains the one recorded in [domain findings](domain-findings.md).
Live balance, Unity pause behavior and stock-guest presentation still require
in-game testing; 0.5 seconds is a tuning value, not a measured optimum.

Verification: 1,002 executable combat checks, 1,905 runtime integration checks
(plus 69 Bat lifecycle checks), 41,176 Tournament scheduler checks, and the full
portable/installed-game compatibility suite passed. Debug and Release builds
had zero warnings/errors with deployment disabled. The runtime performance
fixture retained zero-allocation unchanged synchronization. No deployment or
live Unity/multiplayer test was performed.

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
