# Secondary emission controls and FIFO decision

Implemented `0.91.0`, 2026-10-23 (Asia/Seoul). Follows the
[performance audit](projectile-performance-audit.md) and existing
[active-instance controls](projectile-limits.md). Host only; PvE and PvP;
unmodified guests use the native creation messages.

Since `0.96.0`, [Bolt Magic](projectile-limits.md#bolt-magic-coverage-0960-2026-10-26)
also shares these active and creation-rate mechanisms under `bolt-magic`,
including manual and staff-triggered casts. This extends the original
primary-magic exclusion only for the Bolt family.

## Result

- Four more Ice Weapon families now use both active-instance and creation-rate
  admission: Scabbard of Exorcism, Voluspa, Frozen Bow and Blizzard Hammer.
  Ice/fire variants share their respective source identity.
- Independent creation-rate controls are default off, **64/player/second** and
  **12/source/second**, with one second of stored capacity. Both budgets apply.
- Plitvice swords and Fire/Ice Guillotine creation messages are rate-limited
  before broadcasting. Eternal Eclipse's Frost-triggered Flame Sword attacks
  are gated per sword after native stock consumption and before their FX/coroutine.
  These additional paths are **rate-only**, not active-instance counters.
- **FIFO was not enabled.** The user explicitly preferred keeping current
  behavior if FIFO could introduce regression/performance risk. Concrete native
  teardown and stock-guest synchronization problems meet that condition.

## Controls and semantics

Projectiles contains independent active-cap and spawn-rate checkboxes, editable
player/source rates, and a rate-only reset. EN/KO controls and command replies
use the same service. Values are integers from 1 to 4096.

```text
/one projectiles rate on
/one projectiles rate total 64
/one projectiles rate source 12
/one projectiles rate off
/one projectiles rate reset
/one projectiles status
/one save
```

`/one projectiles on|off` controls active caps only. `rate reset` restores just
the rate fields to off/64/12; `/one projectiles reset` resets both mechanisms.
Preset v50 stores changed rate settings; older presets preserve their existing
active caps and leave rate limiting off.

Each admitted secondary creation spends one token from both its connection's
player bucket and source bucket. A rate of 12 has capacity 12 and refills at
12 tokens/second. This is a continuous token bucket, **not a strict rolling
one-second ceiling**: an initially full bucket can admit its stored burst plus
credit accrued during the next second. Longer idle periods never accumulate
more than one second of stored capacity. Refill uses Unity's unscaled elapsed
time, so game-speed changes cannot multiply the performance allowance.

Instance-cap rejection does not spend rate credit. Accepted creation spends
credit before native callbacks, including reentrant children. Despawn never
refunds creation credit. A native failure also retains its rate charge because
part of the creation may already have occurred. There is no retry queue.

Live rate edits settle accrued credit at the old rate and clamp it to the new
capacity; they do not grant a new burst. Toggling rate limiting off/on or starting
a fresh session/run resets rate history. Enabling active caps while rates remain
on preserves rate history and already-tracked instances. Disconnect/reconnect
uses a fresh connection receipt; old projectile provenance remains retired.

Liberation's non-damaging schedulers retain their separate active-instance cap.
They do not consume the missiles' rate credit; otherwise a rate of one could
let a scheduler prevent its own first attack. Rate limiting therefore does not
claim to bound every network object or every cosmetic particle.

## Source adapters

| Source | Native boundary | Denied attempt behavior |
| --- | --- | --- |
| `ice-air-slash` | `Charm_AirSlash.AirSlashCoroutine.MoveNext` | Skip spawn-result `swingId` write, retain delay/charge progression. |
| `ice-spear` | `Charm_IceSpear.FireSpearCoroutine.MoveNext` | Skip result write, retain nested amplification/spear loops and delays. |
| `ice-bow` | `Charm_IceBow.FireArrow` | Skip result write, retain loop increment and native stock/cadence outside this method. |
| `ice-hammer` | `Charm_IceHammer.HammerCoroutine.MoveNext` | Skip result configuration, `activeBullets.Add` and throw RPC together; retain yield/retrigger. |
| `ice-sword` (rate only) | `Charm_IceSword.RpcCreateSwords` sender | No creation packet; native MP cost and per-frame attempt flag remain consumed. No host-only deletion or delayed retry. |
| `ice-guillotine` (rate only) | `Charm_Guillotine.RpcCreateGuillotine` sender | No blade creation packet; wave/cooldown/stagger progression continues. |
| `frost-flame-sword` (rate only) | Frost branch of `HandleAttackUnit` → per-sword `FireSword` | Preserve successful `UseSword`, suppress coroutine/FX/damage for denied attempts; normal direct/magic/manual Flame Sword paths remain native. |

The four Bullet families have exact native damage IDs `Charm_AirSlash`,
`Charm_IceSpear`, `Charm_IceBow`, `Charm_IceHammer`. These also support adoption
of existing instances when limits are activated. All 14 previously audited
Bullet variants use these four emission paths.

The existing covered Bullet sources and tracked destruction children share the
same rate buckets. Planet artifacts retain independent native source IDs under
the shared player total. Copies of the same source do not gain extra capacity.
Normal/direct weapons, Minigun, other primary magic and native nonplayer attacks remain
outside the registry.

Plitvice's outward sword continues after leaving its orbit slot. Guillotine's
animation owns its lifetime. Mirror host RPC delivery is queued, not synchronous;
a reservation scoped only around the sender would expire before host creation.
Correct active accounting for these paths needs persistent send receipts and
actual host-object destruction, and still cannot assert exact guest visual counts.
This release deliberately adds coherent pre-send rate gates instead.

Flame Sword pickup/return objects, Ice Bat, other unregistered local effects,
ground areas, trails and particle descendants remain outside instance accounting.
Do not describe this as complete effect or particle coverage. No client-rendering
or global animation timing patch is included.

## Why global FIFO was retained as reject-new

An intrusive FIFO queue could make choosing the oldest object constant-time.
That does not make native eviction cheap or equivalent to natural expiry:

1. `Bullet.DestroySelf(false)` can apply explosion damage, execute attack
   callbacks and spawn child projectiles. Eviction could itself create more work.
2. `DestroySelf(true)` avoids those effects for several modules, but not all
   modules despawn immediately. DelayDespawn ignores the force flag; other
   delayed modules retain their timer. Already-destroying bullets can ignore a
   second destruction request, so their slot cannot safely be reused yet.
3. Calling `Pool.Despawn` or `NetworkServer.Destroy` directly bypasses different
   native cleanup responsibilities. Raw network destruction skips `OnDespawn`;
   Ice Vine uses that callback to remove and redistribute orbiters. Pool despawn
   alone omits `DestroySelf`'s owner-list cleanup.
4. Plitvice and Guillotine have no native selective instance-removal message.
   Deleting their host object would leave a guest's separately-created effect
   alive. Plitvice's clear-all message does not address departed outward swords.
5. Replacing every rejected attempt produces native instantiation, initialization,
   spawn/despawn traffic and guest allocation/rendering work. Zero-allocation
   queue bookkeeping cannot remove that churn. Existing full-cap rejection avoids it.

Thus existing instances finish naturally and new over-cap attempts remain
rejected. Lowering a cap waits for natural expiry; it does not delete the excess.
A future opt-in FIFO experiment would need exact prefab/module/callback
allowlisting, a traffic budget, creation-age adoption and live guest profiling.
No such experiment is silently enabled in this release.

## Performance and validation

`ProjectileRateLedger` does constant-time dictionary lookups and arithmetic on
admission. It adds state only for a new connection/source; there are no timers,
per-frame tasks, scene scans, LINQ or per-attempt strings. Rate changes settle
existing buckets once; scope teardown releases their dictionaries. Existing
active-ledger storage reuse remains intact.

Deterministic fixtures verify zero managed allocation for 10,000 warmed accepted
rate checks and 10,000 rate denials. The full runtime denial path also allocates
zero bytes before native instantiation. These figures describe addon bookkeeping,
not Unity frame time, guest FPS or total game allocations.

Tests cover independent toggles, live edits, clock errors, shared totals,
descendants, callback reentrancy, native failures, same-ID reconnection, run
resets, native creation guards and UI/preset/localization paths. Native rewritten
methods are stack-verified and JIT-prepared; signature/topology mutations are
rejected. Actual multiplayer performance and readability require live testing.

Final verification on the installed build:

- Release addon build with `DeployMod=false`: zero warnings/errors.
- Projectile ledger: 136 checks; runtime: 120 checks; native hooks: 468 checks
  across 59 cases. All passed, including unscaled-clock admission.
- Shared runtime: 14,152 checks. Full portable and installed-game contracts
  passed, including 293 projectile policy/preset and 4,636 catalog checks.
- Alternate HarmonyX 2.9 API audit passed for all 41 compiled member references.
  This is a metadata audit, not a live Mono detour test. New string comparisons
  deliberately avoid the newer `LoadsConstant(string)` helper.
- Independent review found no remaining actionable defects after that
  compatibility correction.

Installed assembly SHA-256 remains
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
Selected native evidence: retirement
`ev_5424855f316dcb4c5f5a14553bb9405c7ed45181e1906c5af79d445e721262e0`,
Ice iterators `ev_2387a4b95ba5f4bbc6bfe4148eacfecd4cfd8d641d2e93a973c3d1373d298702`,
and queued host delivery
`ev_b5db50e83be2dd784c4f50013af480570df1165967632ac062f70cfce1e90bae`.
Exports remain outside Git. No game launch, deployment or guest profiler capture.
Use the earlier audit's guest CPU/GPU/frame-time/network comparison procedure;
the reported uneven stuttering has not been attributed to a measured cause.
