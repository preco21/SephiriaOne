# Secondary projectile instance limits

**Goal:** Optional host-owned active-instance caps for secondary weapon/artifact
projectiles in PvE and PvP; preserve normal weapon/magic attack cadence.

**Design:** Two simultaneous limits, total per connection-bound player and per
source for that player. Default off, 64 total / 16 per source; allow individual
source overrides. Unknown/native primary attacks remain unchanged. Add compact
Spawns-panel controls, `/one projectiles` commands, EN/KO and saved presets.

Use explicit audited native emitter call sites, not a generic null-returning
spawn prefix. Several callers dereference spawn results. Guard their post-spawn
configuration on denial, preserving loop progress, cooldowns and original
projectiles. Liberation consumes denied emission attempts instead of queuing
a backlog. Reserve capacity before native initialization/network callbacks.

Use a pure counter ledger with connection identity and release-once leases.
An owned local lifetime component releases on actual destruction; normal native
despawn releases immediately. Do not release on a delayed destroy request or
equipment change. Re-entry cannot reuse old counters. Capture provenance for
child projectiles before parent teardown. No per-frame scene scan: one native
spawn inventory on enabling covers existing eligible instances, followed by
constant-time counters and lifecycle callbacks. Lowering caps lets live instances
finish and blocks additional admissions.

Source catalog initially covers Palas' Card, Liberation, Ice Vine, Thunder's
Earring, Phoenix Wing, meteor, Flame Herb Root, Rock Elephant, Kunai, planet
families and critical weapon needles; additional audited nullable-result
adapters can use the same ledger. Explicit exclusions include ordinary melee,
primary bullets/casts, Minigun/Golem held-fire cadence, native enemy attacks,
cosmetic FX and unverified RPC-only effect lifetimes.

## Work packages

- [x] Settings, command/preset policy, UI, localization and pure policy tests.
  Own `ProjectileLimitSettings`, catalog and command; Session/Controls/UI wiring.
- [x] Pure ledger and independent executable tests: both caps, reservations,
  rollback, release twice, stale leases, old/new connection identities, lowering
  cap, inherited source and warmed hot-path allocations.
- [x] Native runtime/feature/hooks and executable/native contracts. Audit
  pre-instantiation gates, nullable callers, delayed children, destruction,
  enabling with live instances, disconnects and scope resets.
- [x] Integrate, review, document precise coverage/limitations, build and test
  with `-p:DeployMod=false`; conventional commit and push. Never deploy.

Runtime interfaces for parallel work: `ProjectileLimitFeature.Available`,
`Initialize()`, `Shutdown()`; `ProjectileLimitRuntime.Refresh()` and `Clear()`;
`SessionSettings.ProjectilesForUse` returns immutable settings;
`SessionPolicy.Projectiles` / `RecordProjectiles(settings)`. Commands and preset
loads call `Refresh` after committed settings; session clear calls `Clear`.
Native runtime never reads UI state or friendly-fire selection.

Validation includes installed-game method shape checks against assembly SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
Native exports stay outside Git. Live host/stock-guest performance is a separate
unverified follow-up; passing fixtures do not establish frame-time improvement.

Outcome: v0.79.0; scope and live follow-up in [projectile-limits.md](../../projectile-limits.md).
Review caught retired-owner provenance being lost during reseeding, including
connection reuse while the limiter was off. Persistent weak connection epochs
and retained rejection provenance now prevent stale bullets/controllers from
charging a rejoined player or creating uncounted descendants.
