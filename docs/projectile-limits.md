# Secondary projectile limits

Implemented in 0.79.0, 2026-10-21.

## Bolt Magic coverage (`0.96.0`, 2026-10-26)

**Bolt Magic (all elements) / 볼트 마법 (모든 속성)** is now a source in the
Projectile Optimization selector. It uses the existing player total, default
source cap (12 unless changed), source override and independent creation-rate
budget. All elements and casting origins share one `bolt-magic` counter per
player. This includes manual books, automatic casts and staff-triggered Bolts;
switching elements cannot multiply the allowance. Settings are still host-owned
and apply to PvE and PvP, independently of friendly-fire participation.

```text
/one projectiles source bolt-magic 12
/one projectiles source bolt-magic reset
```

The source appears automatically through the shared registry; existing commands,
UI, reset and preset storage need no new schema. Enabling active caps or rate
limits uses the current settings; neither toggle/default was changed.

The installed `ActiveSkill_Bolt.OnServerUpdate` has two spawn sites, for its
multishot loop and single-shot branch. Each now uses the existing reserved-spawn
wrapper. A null result skips that shot's `OnCreateProjectile` callback and
`TurnOnHoming(bool, UnitAvatar)` call, while preserving loop increments, native
timers, boltCounter progression and skill completion/despawn. Admitted shots
retain all arguments, callbacks, damage, homing and native network delivery.
Denied attempts are consumed, not queued, refunded or replaced by early eviction.

The shared emitter guard now pairs each spawn with its own ordered configuration
tail, validates their count/order/exception boundaries, and emits a distinct
continuation for each. Existing one-site emitters retain their prior behavior.
All reflection, instruction scans and continuation arrays occur at hook setup;
runtime uses the existing constant-time ledger/rate admission and cleanup paths.

Existing in-flight bolts are adopted by their exact damage IDs:
`Skill_FireBolt` (also Water), `Skill_AirBolt`, `Skill_EarthBolt`,
`Skill_LightningBolt`, and `Skill_IceBolt`. Destruction children inherit tracked
parent provenance through the existing adapters. Native despawn, direct destroy,
disconnect/reconnect, activation and run resets reuse the same release-once
leases and connection lifetimes. Existing bolts are never force-destroyed.

The staff path uses `SpecialProjectile_RandomBolt.OnUpdateServer` to choose a
Bolt book and invoke `Charm_Magic.FireCasting`; subsequent emissions reach the
same `ActiveSkill_Bolt` boundary. Serialized `sharedassets0.assets` links
`Weapon_Staff_Rolling_T3Bolt` (GO85907) through
`Weapon_Staff_AttackRolling_Bolt(SP)` (179327) to `RandomBolt` (GO75560), whose
184563 component resolves to `SpecialProjectile_RandomBolt` in
`globalgamemanagers.assets`. This confirms the native rolling-staff Bolt path;
the reported 20–40 shots/second has not been measured here.

The short-lived RandomBolt casting controller, skill instances, casting circles,
and client cosmetic children are not Bullet instances and do not get a separate
new cap. This extension controls the resulting bolts and supported destruction
children, not every object created by casting. Other primary spells, ordinary
direct weapons and Minigun remain native. No extra network entities or guest
addon are introduced; native host spawns remain the replication boundary.

Validation: Release build with deployment disabled; 500 native hook checks in
63 cases (including stack checks and JIT preparation of rewritten methods),
135 runtime checks, 136 ledger checks, full installed-game contracts, and the
41-member API audit against co-installed HarmonyX 2.9. Mutations of spawn count,
guard ordering and exception boundaries are rejected. Runtime tests cover Bolt
adoption, mixed elements, descendants, rate refill, native destruction and reused
connections. Existing warmed rejected-admission fixtures remain allocation-free.
No live multiplayer, guest rendering or frame-time improvement is claimed.

Installed assembly SHA-256 remains
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
Native evidence and the focused plan are recorded in
[the Bolt implementation plan](superpowers/plans/2026-10-26-bolt-projectile-limits.md).
No game was launched and nothing was deployed.

## Earlier coverage and common behavior

The [0.91.0 follow-up](projectile-emission-controls.md) adds active/rate coverage
for Scabbard, Voluspa, Frozen Bow and Blizzard Hammer, including fire variants.
It also adds separate creation-rate controls for Plitvice/Guillotine and Frost
Flame Sword callbacks; their local objects are not counted by active caps.
The earlier [0.90.3 audit](projectile-performance-audit.md) records the original
gaps, client descendants, turnover and expensive low-count effects. Reject-new
remains the policy after the FIFO safety audit; no live entities are evicted.

Since `0.89.2` (2026-10-23), the default per-source cap is **12**, down from 16.
Default/reset and presets with no explicit source-default row use 12. Saved
explicit values (including 16) and individual source overrides are preserved.

The host can limit active secondary weapon/artifact missiles in PvE and PvP.
This does not depend on friendly-fire settings, teams or participant selection.
Guests receive native network spawns and do not need the addon. Live multiplayer
and performance still need testing; fixtures do not establish frame-time gains.

## Controls

Since `0.88.1`, open the dedicated **Projectiles / 투사체** tab beside Spawns.
The enable toggle, total/default caps, source overrides and resets are grouped
there. Spawns contains only spawn-related controls; limiter behavior is unchanged.
Defaults: **off**, **64 per player**, **12 per source**. Both caps apply together;
each source can override its limit. Integer range: 1–4096.

```text
/one projectiles on
/one projectiles total 64
/one projectiles source default 12
/one projectiles source pallas-card 8
/one projectiles source liberation 12
/one projectiles source pallas-card reset
/one projectiles status
/one projectiles off
/one projectiles reset
/one save
```

Source reset removes that override. Full reset restores off/64/12 and removes
all overrides. `/one save` preserves configuration in preset v42, never instances
or counters. Older presets retain default-off behavior. EN/KO labels are included.

At either cap, a new eligible instance is not created. Existing projectiles
finish normally. Lowering a cap below the live count blocks further admissions
until it falls. Denied emission attempts are consumed, not queued for a burst.
Equipment changes cannot relabel a projectile or release its occupied slot.

## Audited coverage

| Source ID | Native emission entry point |
| --- | --- |
| `pallas-card` | `Charm_PallasCard.OnBeginAttackAnimation` |
| `liberation` | `SpecialProjectile_FuryMP.OnUpdateServer` |
| `bolt-magic` | `ActiveSkill_Bolt.OnServerUpdate` (single and multishot) |
| `ice-vine` | `Charm_CreateBulletOnSweep.CreateBullet` |
| `thunder-earring` | `Charm_ElectricEarring.AttackCoroutine` |
| `phoenix-wing` | `Charm_FireFeather.OnUpdate` |
| `meteor` | `Charm_FlameGround_Meteor.MeteorCoroutine` |
| `flame-root` | `Charm_FlamePlantRoot.CreateFlame` |
| `rock-elephant` | `Charm_RockElephant.FireStoneBulletCoroutine` |
| `kunai` | `Charm_Kunai.CreateKunai` |
| `planets` | `GreenBat.CreateBullet` (four spawn sites) |
| `weapon-spike` | `WeaponAddonGreatsword_FireNeedleBullet.HandleAttackUnit` |
| `flame-ball` | `Charm_FlameBall.HandleSwing` |
| `yearning-robe` | `Charm_NearMagicBullet.AddBulletAttackCallback` |
| `ice-air-slash` | `Charm_AirSlash.AirSlashCoroutine` |
| `ice-spear` | `Charm_IceSpear.FireSpearCoroutine` |
| `ice-bow` | `Charm_IceBow.FireArrow` |
| `ice-hammer` | `Charm_IceHammer.HammerCoroutine` |

The `planets` setting applies independently to each native planet damage ID;
different planet artifacts do not share a counter. Copies of the same artifact
share its counter. Elemental Liberation variants share the Liberation counter.
The total player cap includes all covered sources together.

Descendants from `BulletDestroyModule_CreateOtherBullet`, `Explode`,
`HeavyExplode`, and `CreateBulletSpread` inherit a tracked parent's owner/source.
Capture provenance before teardown: CreateBulletSpread creates children after
calling its base destruction implementation. Children of untracked primary
projectiles retain native behavior.

Liberation's invisible, non-damaging `SpecialProjectile_FuryMP` schedulers have a
separate ledger bounded by the same player/source limits. They do not consume
missile slots; otherwise a limit of one could let a scheduler block its own
missiles. Thus the missile cap is not a cap on every network object: at most that
many additional covered controllers can be active. Denied controller creation
does not refund native MP or reset the attack cycle.

### Exclusions and boundaries

Ordinary melee, other primary bullets, Minigun/held-fire Golem cadence, primary
magic outside Bolt Magic and enemy attacks remain native. No global spawn gate or attack
interval change is applied.

Other inspected effects need separate adapters before safe inclusion:

- Plitvice swords and Guillotine now have pre-send rate gates, but still lack
  active-instance accounting. Ice Bat and Growth Parry daggers remain outside
  coverage. These use local/RPC effects with distinct lifetimes; deleting only
  host-local instances would diverge from unmodified guests.
- Air Slash, Frozen Bow, Voluspa and Blizzard Hammer now have validated guards
  covering their post-spawn mutations and active-object collection updates.
- Dark Cloud/chain-lightning hit RPCs and cosmetic particles are not missile
  instances. Existing PvP tick controls remain independent.

This is an extensible explicit registry, not coverage of every offensive effect.
New adapters must validate spawn sites, safe denial and stock-guest lifetimes.

## Lifetime and performance

`ProjectileInstanceLedger<T>` maintains constant-time owner/source counts and
release-once reservations. Reserve before native `OnSpawn`, initialization or
network callbacks, including reentrant children. Failed unbound admissions
release in a finalizer.

Owner receipts use the actual spawner/connection, independent of costume,
equipment and PvP selection. Disconnect/removal retires receipts; reused
connection IDs or objects cannot revive old counts. New runs/session teardown
invalidate ledgers. Old cleanup cannot decrement a replacement owner's counters.

Native `Bullet.OnDespawn` and FuryMP teardown release slots. A server-local
`MonoBehaviour.OnDestroy` covers direct destruction and scene cleanup. No
component `Update`, extra network identity, or client addon state is needed.
Do not release on `DestroySelf` or temporary disable: delayed explosions can
remain active after a destruction request.

Activation/new-run setup inventories `NetworkServer.spawned` once and adopts
recognized instances even if over capacity. Previously tracked provenance also
covers delayed projectiles whose native owner was cleared. A pre-tracking
instance with native ownership already gone cannot safely be assigned to a
player and is left untouched.

No scene/roster scan, LINQ query or string construction runs per admission.
There is no new per-frame task. Changing caps while enabled does not rescan.
Ledger tests verify zero managed allocation for warmed denial/count queries.
The full runtime rejection path, including connection/CWT lookup, also allocates
zero managed bytes across 10,000 warmed calls. Successful native spawns
additionally own a lease and local lifetime component.

## Native evidence

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.

REA artifact evidence
`ev_026b08ae3701bcbf4fdb06d0b9bc2a96a1799eb550783ea54de4c740e546b039`
established identity; managed member analysis exceeded the provider budget.
Behavioral findings use focused ILSpy 11.1.0.9782 decompilation and installed IL
checks, not that incomplete REA member result. Exports remain outside Git.
See [Liberation resources](friendly-fire-liberation.md),
[artifact targeting](friendly-fire-artifacts.md) and
[projectile audit](friendly-fire-projectile-audit.md).

Despite its `Pool` name, `ProjectileSpawner<T>` instantiates/network-spawns fresh
objects. Some callers unconditionally dereference its result. Pallas and Yearning
skip only post-spawn configuration on denial; Liberation still increments its
emission counter. Native loops and cooldowns progress normally. Hook signatures,
spawn counts, nullable continuations and exclusions are validated. Incompatible
hooks are uninstalled together; off/reset remain available.

## Validation and live follow-up

Tests cover policy/presets, ledgers, runtime transitions and addon lifecycle.
Installed-native tests validate emitter rewrites and evaluation-stack branches,
emit/JIT rewritten methods, and exercise extracted admission branches without
launching Unity or the game.

Release validation: 147 policy checks, 99 ledger checks, 72 runtime-lifetime
checks, and 298 installed-native hook checks passed. Existing session/runtime,
friendly-fire, panel, localization, addon-lifecycle and native compatibility
suites passed. Builds explicitly used `-p:DeployMod=false`.

Live checks still needed:

1. Host plus stock guest: small caps with Pallas/Liberation stress.
2. Lower caps with live projectiles; off/on with delayed explosions; equipment
   changes, disconnect/rejoin, lobby/run/restarts.
3. Confirm ordinary attacks/Minigun retain cadence; compare frame time/entity
   counts in equivalent endgame builds.

Low caps intentionally reduce covered secondary attack output in PvE and PvP.

## Multiplayer disconnect audit (0.90.1, 2026-10-23)

The [October audit](disconnect-projectile-audit.md) rechecked admission before
native instantiation, NULL continuations, lease release, descendants, reused
connections, ownership and run reseeding. No live-network deletion or authority
mutation was found in the limiter. Runtime/ledger/native-contract checks passed.
This is a simultaneous-instance cap, not a bandwidth/spawn-rate limiter. Short
lived instances, direct attacks and RPC/client-only effects can still create
substantial traffic. Diagnostic snapshots now read cached per-connection counts
without refreshing/reseeding; transport-close entry captures them before Mirror
retires the ledger. No admission or cap behavior changed in this audit.
