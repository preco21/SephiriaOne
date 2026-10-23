# Projectile and effect performance audit

2026-10-23, addon `0.90.3`; installed Sephiria assembly fingerprint below.

## Result and scope

The current per-source cap of 12 is not an effect-performance budget. It covers
selected simultaneous networked missiles, not every artifact, child object,
client particle, emitter, or spawn per second. This audit confirms coverage and
cost gaps; it does not identify the measured cause of the reported stutters.
The user reports that only two or three players stutter, with one affected most.
That is consistent with differing client CPU/GPU/rendering load or per-recipient
network pressure, but does not distinguish them without guest evidence.

This release changes only limiter counter-storage allocation. It does not add
new attack suppression, alter caps, delete live entities, change networking,
or promise that the guest spikes are fixed. Broader admission controls below are
recommendations, not implemented features.

## What the current counters mean

- The 13 source settings in `ProjectileLimitCatalog` correspond to explicitly
  audited `Bullet.Pool.Spawn` call sites. Unknown sources stay native.
- Limits apply per connection receipt, not globally across the party. At the
  default total of 64, nine players can have 576 covered missiles simultaneously.
  Planet types share a setting but have independent source counters.
- Liberation's invisible schedulers have a separate ledger. With the default
  source cap of 12, nine players could also have 108 such controllers; they do
  not consume their own missile capacity. These are theoretical ceilings for
  those settings, not observed counts.
- Actual despawn releases capacity. Therefore 12 short-lived instances can be
  replaced repeatedly in one second. A simultaneous cap does not limit spawn
  throughput, RPC traffic, or synchronized bursts from multiple players.
- Native `ProjectileSpawner<T>.Spawn` uses `Object.Instantiate`, initialization,
  `NetworkServer.Spawn` and finalization. Despawn calls `NetworkServer.Destroy`.
  Despite the `Pool` name, this particular spawner does not recycle instances.

The existing gate runs before instantiation. Existing native entities finish
normally; reconnect/run invalidation retires counts, not network identities.
See [existing lifecycle and safety contracts](projectile-limits.md).

## Child and visual effects

The limiter does count damaging descendants created by the four audited
`BulletDestroyModule_CreateOtherBullet`, `Explode`, `HeavyExplode` and
`CreateBulletSpread` adapters **when the parent was tracked**. Each child inherits
the parent's connection/source and reserves its own slot. Provenance is captured
before parent teardown; the spread module spawns children after base destruction.
Those children are not an uncounted loophole in a tracked chain.

The same statement does not apply to:

- Descendants of an untracked primary projectile.
- RPC-created local attacks, separate ground-area records, and hit-triggered
  effects produced by a different artifact.
- `Bullet` creation/tile-impact FX, trail particles, `TrailRenderer` work and
  movement-module tail sprites/fragments.
- `BulletDestroyModule.CreateDestroyVisualOnClient`: one destruction notice can
  create `destroyFxCount` sprites and separately instantiate every fragment.
  `HeavyExplode` also instantiates another explosion object on the receiver.

Visual children are created on stock clients and may outlive the counted
projectile. The host cannot count those objects through `NetworkServer.spawned`.
One live missile is therefore not equivalent to one rendered object or one
unit of update cost. Lower alpha is not proof of reduced simulation/draw work.

## Entire Ice Weapons family

Fresh read-only inspection parsed 496 `ItemEntity` records across
resources/sharedassets0/1/2 with zero item-parser errors. The FROST category has
12 records, 11 non-disabled. **None of its six damaging emitters is in the current
limiter registry.** Ice Vine (`1139`, covered by `ice-vine`) is a separate source,
not proof of family coverage. Fire conversions share their original emission
paths and are also outside the counter.

| Item and native source | Spawned attacks/variants | Work beyond simple instance counting | Candidate safe admission boundary |
| --- | --- | --- | --- |
| 1014 Scabbard of Exorcism, `Charm_AirSlash` | Four Bullet variants: normal/mini, ice/fire | Charging amplification repeats emissions at 0.25s; retrigger can repeat them. | `AirSlashCoroutine` spawn with a null-safe continuation for `swingId`; preserve charge/delay progression. |
| 1137 Voluspa, `Charm_IceSpear` | Ice/fire spear Bullets, nested amplification × spear-count loop; installed counts reach three spears | Target search per spear; weapon direction search scans creatures. Ice spear tails emit at 0.042s while moving. | Gate each `FireSpearCoroutine` spawn and guard `swingId`, for both ordinary and weapon-triggered paths. |
| 1208 Blizzard Hammer, `Charm_IceHammer` | Six Bullet variants: full/75%-damage hammers × ice/fire, scythes × ice/fire | Scythes live 6s, stay-hit interval 0.33s; `activeBullets` receives manual target updates every 0.2s. | Spawn plus `swingId`, `activeBullets.Add` and `RpcCreateHammer` must be one admitted continuation. |
| 1247 Frozen Bow, `Charm_IceBow` | Ice/fire arrow Bullets | Installed stock seven, 0.75s reload, 0.1s fire interval; amplification expands each stored arrow and retrigger repeats stock. | Gate each `FireArrow` spawn; retain stock consumption, loop/delays and `arrowFiring` completion. |
| 1248 Plitvice sword artifact, `Charm_IceSword` / `IceSword` | `RpcCreateSwords` instantiates local swords; five orbit slots, 5s orbit then 1s outward flight, ice/fire appearance | Each active host-authoritative sword allocates a collision list every 0.1s while its owner is alive/effect enabled, checks overlaps/history and broadcasts hit FX. Installed hit interval is 0.75s, not code-default 0.5s. Guest movement/rendering are separate costs. | Admit before RPC; bind host-created local instance and release on actual destruction. Leaving an orbital slot is not death: the same sword still flies/damages. |
| 1303 Guillotine of Bitter Cold, `Charm_Guillotine` / `IceGuillotine` | Per-target creation RPC, local animation and direct damage callback; ice/fire share path | Four targets per installed base wave, amplified waves, shock-accelerated cooldown; local transition allocations and target following. | Admit each blade before its RPC and track actual host animation destruction; consume denied attempts without changing scheduling. |

These are adapter designs, not implemented or validated replacements. The first
four use the existing Bullet infrastructure but dereference spawn results; simply
adding a null-return gate is unsafe. Plitvice/Guillotine require separate local
instance lifetime tracking because they are not native networked Bullet objects.
Server and stock-client creation must remain coherent.

The remaining category records do not independently emit damaging missiles:

| Record | Role |
| --- | --- |
| 1063 Tiny Magic Conch | Frost damage/critical damage stats. |
| 1209 Frozen Heart | Faster charging, indirectly increasing spawn pressure. |
| 1210 Ice Star | Charging amplification increases emission count at the relevant level. |
| 1179 Ice Wings | Pocket Dimension Shop support item: amplification and native wing visuals. |
| 1259 Eternal Eclipse | Fire conversion and `FLAME_SWORD_CALLBACK_FROST`; see callback chain below. |
| 1261 disabled Plitvice variant | Stat-only `FROST_RELIC_MP_MAXMP_DAMAGE`; not playable sword emitter 1248. |

All 14 Bullet prefabs reached from the four Bullet families use
`BulletDestroyModule_DestroyImmediate`, and serialize `turnOnChainLightning=0`.
They have cosmetic destruction effects, not damaging destruction-child chains.
Plitvice's outward phase reuses its sword; Guillotine deals damage from animation.

There is still a separate hit-triggered chain: Eternal Eclipse enables
`ComboEffect_FlameSword.HandleAttackUnit` to respond to `FROSTRELIC` hits and call
`ServerFireSword`. Those Flame Sword attacks are not tracked Bullet descendants
and remain uncovered. That native callback also interpolates a `Debug.Log`
message before checking its cooldown; a narrowly validated removal of this
verbose diagnostic is another candidate, not a reason to suppress all game logs.

## Expensive paths outside or beyond a missile count

These are observed cost mechanisms, not a measured ranking of frame-time impact.

| Effect/path | Native work and counting boundary | Implication |
| --- | --- | --- |
| Fire/Ice Guillotine | `Charm_Guillotine` emits one reliable creation RPC per blade; receiver instantiates `IceGuillotine`, allocating transition data/delegates and later destroying the object. Serialized base: four targets, 0.06s blade stagger, 0.18s between amplified waves. | Entire attack-object lifecycle is outside the current counter. Client allocation/render bursts are a strong profiling candidate. |
| Snow Mountain Big-Eared Bat | `Charm_IceBat` scans creatures and their debuffs for owned Frostbite, then sends a reliable RPC per eligible victim. Each receiver coroutine creates two local bullets, 0.125s apart. | Source multiplies local instances by eligible victim count; not covered by the missile ledger. |
| Red Planet, normal/enhanced ignite-tail variants | Linked `GreenBat` bullet prefabs use client tail sprites every 0.075/0.05s, with nominal bullet lifetime 2.25s. These rates were not established for every Red Planet variant. | Parent is counted, tail sprites are not. At 12 continuously live parents the configured cadence permits approximately 160/240 tail-emission attempts per second per viewer, before other effects. This is arithmetic, not a measured runtime rate. |
| Native bullet impacts/trails | Creation and destruction sprites, fragments and trails live outside missile accounting; heavy explosions add a separate local object. | Low parent count can still produce substantial rendering and pool traffic. |
| Liberation | Separate scheduler ledger plus emitted missile ledger; native emission loop, movement/collision and spawn/despawn traffic remain. | Bounding missiles does not remove the controller's work or short-lived turnover. |
| Guillotine target/HUD updates | When ready without a target, `FindTargetsInRange(1)` allocates a list and scans `AllCreatures` each frame. Changing cooldown fill can send an owner-targeted unreliable string/float RPC each update. | Candidate optimization: exact ordered availability predicate without a list. HUD quantization/throttling is a separate presentation tradeoff, not identical behavior. |
| Sprite pools | `ObjectPoolingFactory` searches inactive entries, instantiates on growth and can recycle active entries at capacity. | Actual reuse still costs CPU and rendering. Tail calls omit the optional owner, so do not assume party-FX settings eliminate them. |

Do not attribute generic module capabilities to every prefab using the module.
All four audited Palas' Card FireworkHoming prefabs have **empty tail-FX arrays**;
they are not evidence for the Red Planet trail workload. GreenBat's controllers
and target queries also remain outside the cap on their emitted bullets.

### Ground fire: confirmed capability, unproven active item chain

`BulletMoveModule_UniformVector2_MakeGroundFire.OnMove` can create a ground-fire
area on every server movement update, without a time/distance gate. This emits
a reliable `RpcCreateFlameGround`; clients add area records and grid visuals.
Every 0.25s the host queries all areas and all clients age/remove them. Those
records are neither bullets nor separately counted children.

The shipped `FlameBall` prefab (sharedassets0 GameObject `58255`, movement
component `185145`) enables this with radius 0.25 and nominal lifetime 0.4s.
However, the bounded resources/sharedassets0 scan did not establish an active
item or weapon reference to it: no `Charm_FlameBall` component or selected
weapon-fire-data reference was found. Treat this as a capability to investigate,
**not an identified active artifact causing the user's lag**. The Fire Arrow
prefab has trail radius zero; Planet flame-tail bullets use the ordinary movement
module and must not be described as this ground-fire producer.

Separate native follow-up risk: `DungeonManager.Update` uses ten
`FlameAreaDamageRequest` slots, while collecting distinct victims across all
ground areas without a matching bound at insertion. The inspected control flow
can exceed that array with more than ten eligible distinct victims. No such
exception was observed for this report, and this audit does not patch it or
attribute stutters/disconnects to it.

## Low-risk optimization included

`ProjectileInstanceLedger` previously removed its owner counter when the last
lease released and created a new `OwnerState`, dictionary and backing storage
on the next isolated spawn. Short-lived projectiles repeatedly paid this cost.
It now keeps **one empty counter container** for reuse after all its leases have
released. No owner or lease reference is retained in that spare. States retired
by `Clear` or `Forget` are never recycled; old leases cannot debit new counters.
`Clear` also releases the spare. Lease identity/provenance is not pooled.

The .NET allocation fixture measures 10,000 warmed sequential admissions:

| Workload | Before | After |
| --- | ---: | ---: |
| Return to zero after every admission | 2,960,000 bytes | 480,000 bytes |
| Owner stays live through an anchor lease | 480,000 bytes | 480,000 bytes |
| Warmed full-cap runtime denials | 0 bytes | 0 bytes |

This removes about 84% of the measured **isolated ledger bookkeeping allocation**,
not 84% of total game allocations or frame time. Native Unity instantiation,
lease/admission objects, visual children and network work still exist. The change
adds no polling, timers, network messages or gameplay restrictions.

## Recommended next controls

1. **Complete Ice Weapons admission coverage.** Use source-specific adapters for
   local/RPC-backed attacks, with the same connection/source identity and lifecycle
   rules. Gate before the original creation/broadcast, rather than deleting
   objects later or hiding them only on the host. Keep existing effects alive.
2. **Add a separate creation-rate budget with a short burst allowance**, alongside
   active caps. Charge admitted covered secondary emissions and descendants to
   both source and player budgets. Denied attempts must advance native cooldowns
   and loops, not queue a later burst. Preserve direct weapons/Minigun exclusions.
   This intentionally reduces excessive secondary output; make it independently
   configurable instead of silently changing what “12 active instances” means.
3. **Treat RPC/local effects as separate cost classes.** Attack objects need
   native lifetime tracking; short cosmetic-only broadcasts can use a separate
   bounded emission budget. Never suppress a damage-bearing receiver as if it
   were merely decoration, and never replay damage to replace a visual.
4. **Profile expensive low-count effects before changing their timing.** Ground
   areas, trails, repeated target scans and overlapping sprites can dominate with
   few missiles. Host-only patches cannot alter the ongoing per-frame rendering
   code or prefab particle settings on unmodified guests. They can reduce
   compatible source emissions or cosmetic-only sends before they reach guests.

Do not globally patch `Instantiate`, `NetworkServer.Spawn/Destroy`, or all particle
creation. Do not pool native networked projectiles without auditing every reset,
SyncVar, observer and destruction contract. A global party cap may help extreme
rosters, but needs fair per-player admission so one build cannot starve others.

## Verification and profiling follow-up

Release build: zero warnings/errors, `DeployMod=false`. Ledger: 114 checks;
runtime projectile lifetime: 78 checks; installed projectile hooks: 298 checks;
shared runtime: 14,148 checks. Full policy/localization/installed-game contract
suite passed. Independent review found no counter-lifetime regressions.
No game launch, deployment, live FPS capture, or guest profiler session occurred.

For reproduction, capture the same room/build with host and affected guests:
record frame-time spikes separately from ping/transport failures, which effect
was newly emitted, active counts, and creation/destruction rates. Compare one
artifact family at a time, then combined builds and the same player count.
Guest CPU/GPU frame times and their `Player.log` timestamps are needed to separate
rendering/GC stalls from delivery backlog. The existing throttled transport
diagnostics can help correlate failures but cannot attribute bytes to artifacts.

## Evidence

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.

- REA selected spawner/Bullet/descendant metadata, CIL and call edges:
  `ev_9affeb8e472cc9b3d7dce694943e5715a8c86f27f0cbcdc1a9caf04f80715d8c`.
- REA destruction/visual/ground-trail selected members:
  `ev_f54ee7eb2116dae4a59b57cb3a1232c2344bbff43858f07284ec6100153b6a0c`.
- Non-Ice effect asset mapping: `sharedassets0.assets` SHA-256
  `73135788932f461054da76fde2bba1793c64748e77e9dfa3028a1ab6da47ee36`.
  Red Planet component `192905` references normal/enhanced ignite-tail bullet objects
  `58220`/`58364`, movement components `212015`/`212096`, tail prefab `79267`.
  Guillotine artifact object `85766`, component `194145`, establishes the
  serialized wave values. Asset IDs apply only to the inspected files.
- Fresh family item/prefab audit: REA identity summary
  `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`;
  parsed artifact components AirSlash `195216`, Spear `203907`, Hammer `197288`,
  Bow `197287`, sword charm `210149`, sword effect `198238`, Guillotine charm
  `194145`, Guillotine effect `194494`. All FROST ItemEntity records were inspected;
  that completeness does not extend to every unrelated game effect.
- Control-flow inspection uses the external ILSpy exports and focused current
  native/asset audits. Selected-type coverage is not a claim to have profiled
  every artifact or exhaustively followed every asset reference. Decompiled
  game source, private logs and asset exports remain outside Git.

Related: [projectile limits](projectile-limits.md),
[disconnect investigation](disconnect-projectile-audit.md),
[artifact targeting](friendly-fire-artifacts.md),
[performance review](performance-review.md).
