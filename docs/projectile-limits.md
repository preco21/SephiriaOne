# Secondary projectile limits

Implemented in 0.79.0, 2026-10-21.

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

Ordinary melee, primary bullets, Minigun/held-fire Golem cadence, manually cast
primary magic and enemy attacks remain native. No global spawn gate or attack
interval change is applied.

Other inspected effects need separate adapters before safe inclusion:

- Plitvice swords, Ice Bat bullets, Growth Parry daggers and Guillotine use
  RPC-created client effects with distinct lifetimes. Limiting only host-local
  instances would diverge from unmodified guests.
- Air Slash, Frozen Bow, Voluspa and Blizzard Hammer have additional return-value
  mutations or active-object collections requiring individual validation.
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
