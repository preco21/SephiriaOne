# Temporary PvP cover investigation

Investigated 2026-10-27 against the installed game. This records the initial
research/design; the original investigation added no runtime behaviour. The
subsequent **0.100.0 implementation** uses indestructible compact barrels with
a configurable count: see [current controls, lifecycle and limits](pvp-cover.md).
Agents should prefer that document for current behaviour.

## Conclusion

Host-only temporary cover is feasible with an **allowlist of existing registered
network prefabs that already have movement-blocking colliders on stock clients**.
The strongest initial candidates are dormitory barrels/tables made indestructible
and the debris-free `Grassland-Table` for destructible cover. Arbitrary decorative
props and all crates/book piles cannot be treated as interchangeable cover.

Strict cleanup includes broken objects. Some native props detach client-local
fragments or instantiate unrelated rubble; destroying their network root does
not remove those descendants. A first implementation should exclude those
props from destructible mode. Indestructible variants must not enter the native
break path. This meets the requirement through asset/behavior selection instead
of promising a nonexistent general remote debris-delete API.

The code/asset evidence establishes transport and collision contracts. Actual
owner movement, guest rendering, projectile contact and timing need live tests.

## Verified native transport

`PropDatabase.Initialize` loads `Resources/Prop`; `PropEntity` maps an ID to a
prefab and category. `FloorGenerator.CreateProp` distinguishes root
`NetworkIdentity` from local scenery:

- Networked props are server-created, instantiated without a floor Transform
  parent, initialized with random ID/options, then sent through
  `NetworkServer.Spawn`. The floor records them in `floorRelatedNetworkObjects`.
- Props without a NetworkIdentity are local floor-generation objects. Creating
  one only on the host does **not** publish it to guests.
- `DestroyProp` uses `NetworkServer.Destroy` for a networked root. Native floor
  release also destroys the entries in `floorRelatedNetworkObjects`.
- Both inspected `HorayNetworkManager` assets (sharedassets0 MonoBehaviour
  222029/222030) contain 2,297 spawn-prefab entries. The recommended barrel,
  small dormitory table and grassland table roots each appear once in both lists.
- Their network identities have `serverOnly=false`. Their transforms synchronize
  position server-to-client, with `onlySyncOnChange=true`. Static obstacles need
  no ongoing movement updates. Native initial spawn delivers packaged rendering
  and colliders; do not add custom NetworkBehaviours or change collision layers
  only on the host.

`BreakableProp.NetworkCanBeBroken` and `NetworkIsBroken` are stock SyncVars.
`ApplyDamage` runs only on the server, refuses intact indestructible props, and
publishes break state plus a native break RPC. Ordinary damage can therefore be
controlled by the host without guest scripts. `BreakForce` ignores CanBeBroken;
if using indestructible cover, explicitly reject that operation for owned props
as well. Existing calls found target boss-owned objects, but the invariant should
not depend on that remaining true in future game/addon versions.

Configure the new **instance** before publishing its spawn, with no owner
connection, stock scale/colliders and server-side loot/save restrictions. Mirror
the required native initialization/floor bookkeeping in a small adapter or add
an audited pre-publication boundary around the native factory. Do not temporarily
mutate shared prefabs or publish a mutable object and configure it later.

## Asset candidates

IDs below are native PropDatabase IDs; `Domitory` is the shipped spelling.
Footprints are native collider size, before safety margins.

| Prop | Root / network asset ID | Movement footprint | Cleanup assessment |
| --- | --- | --- | --- |
| `DomitoryBarrel_0` | sharedassets0 GO 78715 / 4148004312 | Tile-layer box 0.875 × 0.875 | Good indestructible cover; native break detaches 6 fragments. |
| `DomitoryBarrel_1` | GO 78705 / 251923738 | Tile-layer barrel | Same family; validate the precise footprint individually. |
| `DomitoryBarrel_2` | GO 78701 / 3412844843 | Tile-layer barrel | Same family; validate the precise footprint individually. |
| `DomitoryTable_1` | GO 78719 / 2839126442 | Tile-layer box 1.35 × 0.8 | Good compact indestructible table; native break detaches 5 fragments. |
| `DomitoryTable_0` | GO 78714 / 3982980687 | Tile-layer box 3 × 1 | Larger indestructible cover; requires substantially more clearance. Six break fragments. |
| `Grassland-Table` | GO 74670 / 311987810 | Tile-layer box 2 × 0.5 | Best audited destructible candidate: no detached fragments, break FX prefab or separate rubble. Broken appearance stays on its root. |
| `Box00`, `Barrel00`, `Library-Book001/003`, many dormitory/library boxes | Existing networked roots | Main collider on Blockable or Default, rather than Tile | Do not assume player-movement cover. Book/box examples also detach fragments. |
| `Library-Table`, `Library-Table_1`, stone walls | Local decorative roots | Some have Tile colliders | Cannot be dynamically published simply by host Instantiate; no native NetworkIdentity. |

`Grassland-Table`'s IDLE/BROKEN animation set (178363) contains no frame or sound
events. Its plain BreakableProp has an empty save-counter ID, no item drops,
XP or sapphire, `destroyOnBroken=false`, and a native connected collider that
is disabled on the break RPC. The broken sprite/root still needs explicit
cleanup. Its default HP is 1, so destructible durability would need a deliberate
host-side value if it should survive more than one hit.

The candidate scan covers all plain Breakable-category PropEntities plus
selected table/book/rock/stone IDs, not every prop or animation in the game:
721 PropEntities and two network-manager records found across resources and
sharedassets0/1/2; 88 selected prefab hierarchies examined, with no prop-parser
errors. Other safe candidates may exist.

### Collision and attack limitations

The shipped layer map names layer 12 `Tile`, layer 3 `Blockable`, and layer 6
`TopdownWorld_1F` (damage hitboxes). Ordinary `TopdownRigidbody` movement builds
its obstacle mask from Tile/Cliff and relevant pit layers. A networked object
with a collider on another layer is not automatically a player-blocking wall.
AStar's obstacle mask also includes Blockable, which is a different question.

Tile-aware bullets run their native Destroy/Pierce/Bounce behavior on collision.
Breakable damage hitboxes additionally use `isBlockProjectile`. Cover will not
universally block every attack: piercing shots, ground/area effects, teleporting
attacks, and targeting logic without a line-of-sight requirement retain their
native behavior. Verify representative direct shots, homing, beams, planets and
area attacks; do not silently turn cover into a global projectile rewrite.

These are real world obstacles. They also affect nonparticipants and enemies in
the same level. Per-participant-only collision is not part of this host-only
proposal. Native phasing/dash abilities may bypass obstacles by design.

## Cleanup contract, including destroyed props

Own each spawned root under a cover epoch plus dungeon/run/floor object identity;
retain its object identity and netId, not just a reusable integer ID. Cleanup
must handle intact, broken, partially spawned and already-despawned entries.

1. Cancel pending spawn work when the epoch changes. Record ownership before
   publication; if construction/publication fails, clean up partial objects.
2. Destructible mode uses only audited debris-free props. Queue disposal after
   the native break callback returns, using `NetworkServer.Destroy`; avoid
   destroying/resetting the object halfway through ApplyDamage callbacks.
3. On PvP end, option off, mode interruption, floor change, fresh run, host
   departure or addon unload, remove **every remaining owned root**, including
   its broken visual form. Remove owned entries from native floor bookkeeping
   so later floor release does not repeat stale work. Never destroy natural props.
4. Clear expired entries idempotently. A native floor release/prop expiry may
   win the race; never issue a destroy against a reused ID or a replacement root.
5. Restore affected pathfinding after collider removal; do not leave stale
   blocked cells. Never call BreakForce as cleanup: that deliberately creates
   break presentation, callbacks and potentially rewards.

`OnRpcBreak` detaches fragments from the network root and can instantiate a
`brokenObjectPrefab` under a local floor. Those objects are not network identities.
Fragments normally expire via their own randomized timer/fade, but can outlive
PvP ending. Host-side changes to breakFxFragments or brokenObjectPrefab cannot
change stock guests' prefab behavior. No general remote removal route was found.
Thus destructible barrels with normal native debris fail the **strict immediate
cleanup** requirement. Suppressing the entire break presentation and despawning
instead could be a future separately audited alternative, with a visual tradeoff.

A further native edge case: IsBroken initial deserialization has no collider hook;
collider disabling is in the transient break RPC. A late observer of a retained
broken root may therefore miss that transition. Prompt post-break disposal of
owned debris-free props avoids depending on broken-root replay.

## Safe placement and lifecycle design

Recommended first scope: the host's current, fully generated level, with cover
in supported ordinary room interiors. Use a separate layout epoch for manual
friendly fire and reuse CombatMatchRuntime.MatchToken for sub-modes. A match
can span a floor change: CurrentScope does not include the floor GUID, so a
cover subsystem must explicitly track generator identity and floor GUID.

- Plan once at activation/countdown, not per attack/frame. Pick a deterministic
  seed from the cover epoch/floor seed so reconnects receive the same layout
  rather than generating another one.
- Reuse MerchantRooms' room adapters, full-footprint ground/water/pit/cliff checks
  and bounded sampling. Its existing single-point test is **not** sufficient
  proof that adding multiple solid obstacles preserves routes.
- Protect room passages/door rectangles, spawn/respawn positions, map teleport
  landing points, portals, stairs/exits, interactables and quest objects with
  explicit margins. Native room metadata supplies passageArea/hasPassage and
  teleportPoint; FloorGenerator also exposes spawnPoints. Check actual actors
  and colliders again immediately before placing each accepted obstacle.
- Keep cover away from room edges, narrow corridors and living/dead players.
  Treat corpse/automatic-respawn positions as occupied. Maintain generous gaps
  between obstacles, respecting the largest supported player footprint.
- Validate connectivity over a bounded occupancy map with candidate footprints
  inflated by player radius. Preserve previously reachable essential endpoints;
  reject layouts that close a passage or isolate a player/portal. Use bounded
  work or a multi-frame planning phase, not a full-level flood fill every frame.
- Spawn fewer objects if the requested target is unsafe. Never force the quota
  into a small room. Unsupported/boss/cutscene/progression maps should fail closed
  initially; lobby/open-map support needs explicit room/protected-area adapters.
- On a host level change: cancel the old queue, clean the old layout, and create
  one new layout only after the destination is ready. Do not clone cover into
  every participant's floor or reset it for each reconnect/conversion/respawn.

Suggested controls (not implemented): default-off toggle; total target count
(start with 12, hard cap 32 initially); per-room cap (3); optional density mode
with the same hard cap; style and destructible/indestructible choice from the
allowlist. Count is an upper bound. Regeneration should be explicit or happen
at a session/floor boundary, rather than replacing cover during every setting
edit or Tournament pairing. Tuning these values still requires gameplay tests.

## Performance and architecture

Keep the feature separate from projectile limits: stationary cover has different
lifetimes and must not be FIFO-evicted by offensive effects. Use a small catalog,
a bounded placement planner and one shared session-owned cover registry.

Spawns/despawns are normal reliable native lifecycle messages, once per object,
with initial state replay for late observers. Keep props stationary at their
packaged scale and use the native only-on-change transform configuration. Avoid
continuous object searches, regenerating layouts per player, frequent health
broadcasts, or repeated compatibility/asset inspection at runtime.

There is a specific pathfinding cost: `UpdateAstarProp.OnEnable/OnDisable` calls
`PathGrid.UpdateRegion`; that method may rerun erosion over the **whole grid**
when erosionIterations is positive. It is not always cheap local work. Barrels
without UpdateAstarProp need explicit host path refresh; tables with it may do
work independently on each guest. Batch host bounds updates where possible,
use a small per-frame spawn budget and modest hard caps, and measure guests.
Host batching cannot rewrite an unmodified guest's native OnEnable behavior.

The current project already has shared match/participant/reset infrastructure
and safe floor-room probing to reuse. A new registry must still own environmental
objects independently of a particular player's connection so one departure does
not strand objects. No per-hit hook is necessary except an owned-prop break/force
break boundary if destructibility needs control. Pooling across stock network
lifetimes is not justified for a few dozen stationary props.

## Evidence and validation required

Installed Assembly-CSharp SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA selected-member parent:
`ev_083708556bedcb23b7711475417951c57b168c176e929cae4ee1906c05533bb8`.
Selected types: BreakableProp, PropDatabase, PropEntity, FloorGenerator,
TileFloorGenerator. Metadata complete for that selection; three #US terminal
flag anomalies preserve readable strings and do not establish runtime issues.

Asset SHA-256:

- resources.assets: `906ba704cf4eefa8e300501c3b4aeefdeae91883885eb8fbe31ab754c60a3f9b`
- sharedassets0.assets: `73135788932f461054da76fde2bba1793c64748e77e9dfa3028a1ab6da47ee36`
- globalgamemanagers: `8924c809dcf3b93a1169b0dc2017c8543aee17ba03e41b675f7a3205d73549a5`

Managed decompilation and temporary UnityPy asset inventories remain outside Git.
No game execution, live spawn or client traffic capture was performed.

Before enabling a runtime feature, test with stock guests: root spawn/despawn,
owner collision, representative projectile types, destructible break and late
join, exit immediately after destruction, level change mid-spawn, host shutdown,
reconnect, repeated matches, failure rollback, blocked-layout rejection, and
frame/network cost on a large map with around nine players. Assert that no owned
root, broken visual, detached debris, drops or stale path obstruction remains.
