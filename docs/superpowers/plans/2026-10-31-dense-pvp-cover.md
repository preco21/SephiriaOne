# Dense PvP cover implementation plan

**Goal:** Fill the configured cover quota more densely, allow portal obstruction,
and move overlapping players to verified nearby ground before creating a prop.

**Design:** Remove broad interaction/portal exclusion and the 3x3 bypass rule.
Retain physical walls, hazards, positive ground and small scripted spawn/return
reservations. Use a 1.55x1.2 envelope for the audited maximum 1.35x1 prop plus
clearance. Sample half-unit positions with matching separation. The existing
count (default 20), per-frame spawn budget, host floor scope and cleanup remain.

Prefer native position requests over impulse pushes: pushes are asynchronous,
mass-dependent and cannot prove clearance. Never instantiate first and rely on
native depenetration. Plan distinct nearby walkable destinations for all affected
players before sending any request. Use actual movement-collider bounds and
offsets; check all current-floor occupants, not only selected PvP participants.
One bounded pending placement waits up to two seconds for guest movement to be
acknowledged and observed. Recheck geometry/occupancy immediately before spawn. Abort on scope,
setting, floor or connection change; never resend or force authority changes.

- [x] Add regression tests for dense ground/portal candidates and multiple
  players, asynchronous movement, timeout, re-entry and cleanup.
- [x] Refine `PvpCoverPlacement` and split reusable footprint/terrain queries
  from `PvpCoverRooms`; reuse buffers, keep bounded search work.
- [x] Add `PvpCoverDisplacement` beside the cover runtime. Integrate one pending
  placement and native request/observed-clearance phases before `Spawn`.
- [x] Verify installed native movement/RPC and collider contracts; run cover,
  runtime and installed-game tests plus non-deploying Release build.
- [x] Update current cover/domain findings, version and index; independent
  review, Conventional Commit and push. Never deploy.

Native evidence: assembly SHA256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`,
REA `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`.
`CombatBehaviour.ReqSetPosition` calls the owner TargetRpc on a host for remote
players; position synchronization is asynchronous. `UnitAvatar.SetPosition`
also relocates followers, so final occupancy must include their colliders.
Do not edit transforms or network ownership directly. Live stock-guest timing
and collision verification remains necessary after static/fixture checks.

Review follow-up: native RPC destinations stay reserved after timeout/cancel,
readiness/loading and floor changes, including reused world coordinates. Only
acknowledgement plus observed arrival or identity invalidation retires them;
unload detaches callbacks after removing cover. Fixed final-attempt pending
polling, native-callback scope invalidation, hazard-trigger filtering and large
body fallback. Independent re-review found no remaining important findings.

Final verification: Release build 0 warnings/errors; 15,295 cover lifecycle
checks (0 bytes/1,000 stable ticks), 17,830 runtime integration checks, 69 Bat
lifecycle checks, 50 localization checks, panel suites, 47 installed-game cover
contracts and 41 co-installed Harmony API references passed. No deployment or
live guest playtest.
