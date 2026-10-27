# Temporary PvP cover

Implemented in 0.100.0 (2026-10-28). Host addon only; uses a registered native
barrel prefab and its native collision/network behaviour. No guest installation
or additional assets. Static contracts and simulated lifecycle tests pass;
stock-guest gameplay/collision has not been exercised in a live session here.

## Controls

In **PvP 전투 / PvP Combat**, scroll the left column to **임시 엄폐물 / Temporary
cover**. Default **off**, requested count **12**, range **1–32 per host level**.

```text
/one friendlyfire cover on
/one friendlyfire cover count 20
/one friendlyfire cover off
/one friendlyfire cover count reset
/one friendlyfire cover reset
/one save
```

Changing the count does not stop a match. Lowering it immediately removes surplus
owned barrels; raising it continues the bounded placement search. Existing cover
is preserved where possible. `cover reset` disables it and restores count 12;
`count reset` only restores the count. Saved changes use preset v56; older
presets retain the default-off setting. This is independent of PvP balancing.

The option follows manual friendly fire and the shared Deathmatch, Tournament,
Play Tag and Zombie Survival lifecycle, including match countdowns. Pair changes,
conversions and automatic respawns do not rebuild cover. A new match/floor/run
gets a new placement scope. Ending/stopping PvP, disabling cover, host loading or
changing floors, session shutdown and addon unload remove owned roots.

## Placement and limitations

- Supports ordinary rooms in EnhancedProcedural, Fixed and Library tile-floor
  generators, plus supported native lobby rooms (including SingleRoom lobbies).
  Boss-only and unknown/special single-room layouts are skipped.
- The count is a maximum, not a quota forced onto an unsafe map. Narrow rooms,
  populated lobbies, geometry and protected interactions can reduce it to zero.
- Random candidates cycle through shuffled rooms. A room-edge margin, a clear
  5×5 movement square, and separation between barrels preserve a walkable bypass
  around every compact obstacle. This conservative local clearance replaces a
  whole-map connectivity search. There is no additional fixed per-room count
  cap: spacing naturally limits density while allowing useful cover in a large
  single-room lobby.
- Reserve four units around room teleport points, native floor spawn points,
  existing interactive props and current player positions, including corpses.
  Reject pits, water, walls, cliffs and native blocking colliders.
- The barrels use their original Tile-layer collider. They affect everyone in
  that level, including nonparticipants and monsters. They cannot selectively
  block only PvP participants on stock guests.
- Preserve native projectile rules: ordinary colliding projectiles can be
  blocked, but piercing attacks, area damage, phasing and attacks that ignore
  line of sight may bypass cover. It is not a universal damage barrier.
- Extensible scripts/mods that deliberately alter owned prop SyncVars or invoke
  raw break RPCs are outside the supported native lifecycle.

## Native ownership and cleanup

Only `DomitoryBarrel_0` (asset ID `4148004312`) is used initially. It has an
enabled non-trigger Tile-layer box collider, size `0.875 × 0.875`, offset
`(0, 0.4375)`. Position the root below the selected clear-square center by that
offset. Verify the prefab, stock spawn registration, collision and reward-free
metadata before spawning; never change shared prefab assets.

`NetworkCanBeBroken = false` is set **before** `NetworkServer.Spawn`, so the
initial state and late observers receive native indestructibility. These props
are server-owned, with no extra NetworkBehaviours. Register each root before
publication can fail, in both addon ownership and the native floor's `allProps`
and `floorRelatedNetworkObjects` lists.

Native `BreakForce` bypasses `CanBeBroken`; a narrow ownership guard prevents
that method and `SetCanBeBroken` from breaking only these barrels. Natural props
keep their original behaviour. Cleanup disables the host collider, calls native
`NetworkServer.Destroy`, and removes both floor-list entries. Never use
`BreakForce` or `RpcBreak` for cleanup: the native barrel has six client-local
fragments which otherwise detach from the network root. Preventing that break
path guarantees there are no broken remnants from this feature to strand.

Failed despawns remain owned and retry even when placement has faulted. Unload
does not remove the protection/controller while cleanup is still pending. An
asset/spawn failure is latched for that scope to avoid per-frame retries; off/on
or a new scope retries. Unexpected native destruction is pruned without an
endless replacement loop. Fresh joins use native spawn replication; no custom
per-client ownership or entity IDs are introduced.

## Performance and verification

Placement runs at most **two candidate probes/spawns per rendered host frame**,
with **1,024 attempts per scope**, and stops when the quota/search is complete.
Ground/collision queries and room/interaction collection do not run during
steady-state combat. Native graph erosion can touch the entire graph, so
placement updates are coalesced into one region refresh; reductions/cleanup
also coalesce removals. `PathGrid.UpdateRegion` queues changes during native
asynchronous scans; do not gate it on `IsBuilt` and silently lose those changes.
Late camera/room scans see the stock physical colliders normally.

Validation:

- Runtime command/preset suite: defaults, bounds, reset, v56 round trips and old
  schema rejection; pure placement separation, protected-region veto and bounded
  exhaustion.
- `tests/SephiriaOne.CoverTests`: production lifecycle with stubbed native calls;
  count changes, all shared match scopes, floor/load/run changes, partial spawn
  rollback, failed despawn retry, native destruction, shutdown, scan overlap and
  bounded per-frame work. Zero managed allocations over 1,000 stable fixture
  ticks; this is not a Unity frame-time benchmark.
- Installed-game suite: native breakability replication, force-break bypass,
  initial-spawn ordering, root-only destruction, queued graph updates, unload
  guard and compiled Harmony compatibility.
- Release build and test runs use `-p:DeployMod=false`. Nothing is deployed.

Live verification still needed: stock guests entering/rejoining while cover is
present; movement and representative projectiles on either side; ending a match
under heavy fire; changing floors mid-placement; repeated lobby matches; native
NPC/companion navigation and frame/network cost with a full room. Confirm no
owned root or visual remains after stopping PvP.

Native evidence and alternative candidates: [cover investigation](pvp-cover-investigation.md).
Agents should consult this implementation document before the older design.
