# Temporary PvP cover

Implemented in 0.100.0; varied props and faster placement in 0.101.0 (2026-10-29).
Host addon only; uses registered native props and their native collision/network
behaviour. No guest installation or additional assets. The user confirmed cover
appears in the lobby and on a second run with 0.100.2. The new prop mix and tighter
clearance have static/fixture coverage; stock-guest gameplay remains unverified here.

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
owned props; raising it continues the bounded placement search. Existing cover
is preserved where possible. `cover reset` disables it and restores count 12;
`count reset` only restores the count. Saved changes use preset v56; older
presets retain the default-off setting. This is independent of PvP balancing.

The option follows manual friendly fire and the shared Deathmatch, Tournament,
Play Tag and Zombie Survival lifecycle, including match countdowns. Pair changes,
conversions and automatic respawns do not rebuild cover. A new match/floor/run
gets a new placement scope. Ending/stopping PvP, disabling cover, host loading or
changing floors, session shutdown and addon unload remove owned roots.

## Placement and limitations

- Supports EnhancedProcedural, Fixed, Library and SingleRoom tile-floor layouts,
  including non-monster entrance/rest rooms as of 0.100.2.
  Since 0.100.1, also supports authored `FullyDesignedFloorGenerator` layouts
  with the audited ground paths used by MultiZone and TheRabbittown.
  Boss arenas, unsupported generators and layouts without verified ground are skipped.
- The count is a maximum, not a quota forced onto an unsafe map. Narrow rooms,
  populated lobbies, geometry and protected interactions can reduce it to zero.
- Random candidates cycle through shuffled rooms across the **whole host floor**,
  not only the currently occupied room. Each shuffled five-prop bag contains
  every variant once before starting the next bag.
- A 2.5-unit room-edge margin, a clear 3×3 movement square, and three-unit
  separation between prop centers preserve a walkable bypass
  around every compact obstacle. This conservative local clearance replaces a
  whole-map connectivity search. There is no additional fixed per-room count
  cap: spacing naturally limits density while allowing useful cover in a large
  single-room lobby.
- Reserve four units around room teleport points, native floor spawn points,
  existing interactive props and current player positions, including corpses.
  Reject pits, water, walls, cliffs and native blocking colliders. Damage hitboxes
  and grounded bodies/props on TopdownWorld_1F/Blockable must clear a 1.85×1.5
  footprint/margin; they
  do not need to clear the entire 3×3 walkable bypass like physical obstacles.
  Ground checks cover every tile touched by that square, including fractional
  room/grid offsets (nine cells when aligned, up to sixteen otherwise).
- The props use their original Tile-layer collider. They affect everyone in
  that level, including nonparticipants and monsters. They cannot selectively
  block only PvP participants on stock guests.
- Preserve native projectile rules: ordinary colliding projectiles can be
  blocked, but piercing attacks, area damage, phasing and attacks that ignore
  line of sight may bypass cover. It is not a universal damage barrier.
- Extensible scripts/mods that deliberately alter owned prop SyncVars or invoke
  raw break RPCs are outside the supported native lifecycle.

## Native ownership and cleanup

The compact catalog uses these stock props:

| Prop ID | Asset ID | Collider size | Offset Y |
|---|---:|---|---:|
| `DomitoryBarrel_0` | 4148004312 | 0.875 × 0.875 | 0.4375 |
| `DomitoryBarrel_2` | 3412844843 | 0.875 × 1 | 0.5 |
| `DomitoryBarrel_3_Potato` | 3401681619 | 0.875 × 0.875 | 0.4375 |
| `DomitoryBarrel_4_Carrot` | 1484013372 | 0.875 × 0.875 | 0.4375 |
| `DomitoryTable_1` | 2839126442 | 1.35 × 0.8 | 0.4 |

Each has one enabled non-trigger root Tile-layer box collider, offset X zero,
unit scale and no rotation. Spawn and cleanup use the selected collider's actual
offset. Validate every catalog entry and native spawn registration once per
placement scope before publication; never change shared prefab assets. Reject
changed identities/dimensions, rewards, interactables and `UpdateAstarProp`.

`NetworkCanBeBroken = false` is set **before** `NetworkServer.Spawn`, so the
initial state and late observers receive native indestructibility. These props
are server-owned, with no extra NetworkBehaviours. Register each root before
publication can fail, in both addon ownership and the native floor's `allProps`
and `floorRelatedNetworkObjects` lists.

Native `BreakForce` bypasses `CanBeBroken`; a narrow ownership guard prevents
that method and `SetCanBeBroken` from breaking only these owned props. Natural props
keep their original behaviour. Cleanup disables the host collider, calls native
`NetworkServer.Destroy`, and removes both floor-list entries. Never use
`BreakForce` or `RpcBreak` for cleanup: these props have five to nine client-local
fragments which otherwise detach from the network root. Preventing that break
path guarantees there are no broken remnants from this feature to strand.

Failed despawns remain owned and retry even when placement has faulted. Unload
does not remove the protection/controller while cleanup is still pending. An
asset/spawn failure is latched for that scope to avoid per-frame retries; off/on
or a new scope retries. Unexpected native destruction is pruned without an
endless replacement loop. Fresh joins use native spawn replication; no custom
per-client ownership or entity IDs are introduced.

## Performance and verification

Placement runs at most **16 candidates and two native spawns per host frame**,
with **1,024 attempts per scope**, and stops when the quota/search is complete.
Each candidate uses at most two collision probes (static bypass, then occupancy),
so there are at most 32 collision probes per frame. Repeated same-frame
notifications cannot multiply this budget. A fully rejected search exhausts in
64 frames, versus 512 before 0.101.0; actual elapsed time depends on frame rate
and accepted spawns. Do not promise instant placement or a fixed wall-clock time.
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

## Lobby placement fix — 0.100.1

The user's Player.log confirmed 0.100.0, enabled cover and friendly fire, and
`MultiZone`/`TheRabbittown` waiting-area floors using `FullyDesignedFloorGenerator`.
That class derives directly from `FloorGenerator`, not `TileFloorGenerator`.
The original runtime cast rejected it before room search, prefab validation or
diagnostic output. The addon therefore silently created no lobby cover.

The runtime now retains the base floor type. The room adapter reads native
authored bounds, resolves explicit ground/upper/wall/water tilemaps, and preserves
the same positive-ground, hazard, collision and 5×5 bypass checks. Native authored
props have a separate `props` list; its interactions and native map teleport
destinations (including destination offsets) are protected too. Unknown layouts,
missing ground, incompatible grids and boss arenas still fail closed.

The UI and `/one friendlyfire status` show waiting/placement/actual-count/error
states. Player.log gets one completion/skip summary per scope/count: floor type,
room count, attempts and rejection counters. No repeated per-frame logs or
new steady-state physics queries. Compatibility failure has its own status.

Regression coverage now compiles the **production room adapter**, replacing the
earlier always-safe room stub. Tests cover the lobby hierarchy, world offsets,
missing cells, pits/water, authored interactions, teleport offsets, walls, missing
ground, incompatible scale and existing procedural room types. The original
lobby regression failed before the runtime fix. Lifecycle and native contract
suites pass; no live deployment or guest test was performed for this patch.

Evidence: unchanged installed DLL SHA-256 listed in the investigation; REA
selected-member evidence `ev_b4c3f1dc31c41d34a082c1fb9bf80e9f1e1057144449a3fa8cfe60b26bd90d41`
for the base/authored/tile floor types. Built-in asset hierarchy and tilemap
trees confirm `Grid/Ground` (MultiZone), `Grid/Pit/Ground` (TheRabbittown), unit
grids and matching wall/upper paths. Offline ground/wall-only clearance found
297 and 5,334 potential 5×5 centers respectively; these are **not** live spawn
counts and exclude other props/players/hazards. Root generator MonoBehaviour
typetree decoding failed; bounds and prop-list contracts come from the installed
managed code instead. Temporary asset inventories and private logs stay outside Git.

## Entrance/rest and occupancy follow-up — 0.100.2

The subsequent Player.log explicitly loaded 0.100.1. Cover was enabled and its
runtime ran, but reported:

- `Moleland_EntranceC6` (EnhancedProcedural): zero eligible rooms, zero attempts.
- `Moleland_Anvil_Peace` (SingleRoom): zero eligible rooms, zero attempts.
- `Moleland_HardBattle_2_Jail`: three rooms, 1,024 attempts, zero cover; 418
  protected-position rejections and 606 collider rejections (a second attempt
  reported 414/610). No terrain checks were reached on unreserved candidates.

The entrance/rest failures were caused by policy copied from hostile-monster
placement: require `Metadata.spawnMonster` outside a race lobby, and accept
SingleRoom only as a race lobby. PvP cover now uses these generators' actual room
bounds regardless of monster-spawn or race-lobby flags. The explicit native boss
threat gate and every ground/physical-clearance/progression safety check remain.
The obsolete lobby argument is removed from the room adapter.

The combined collision probe also treated damage hitboxes as full movement
obstacles. Native `CombatManager.BlockCharacterLayerMask` excludes
`TopdownWorld_1F` and `Blockable`; bundled furniture such as chairs, tables and
barricades have separate, larger layer-6 hurtboxes. Native `TopdownRigidbody.UpdateAirLayer`
also assigns grounded actor bodies to Blockable (layer 3), while its movement
queries use Tile/Cliff/Pit masks. Cover now keeps the full 5×5 bypass check for
Tile/Cliff/Pit/Pit_Water colliders, and checks TopdownWorld_1F/Blockable separately
within a 1.5×1.5 footprint (the barrel is 0.875×0.875). This avoids overlap while
allowing nonblocking hurtboxes in the bypass ring. Player exclusion remains four
units, and the 25-cell ground/hazard check is unchanged.

**The exact collider responsible for the reported Jail search is unknown.** The
old log recorded only a counter, not collider identity; the asset audit alone
cannot reproduce a live physics scene. Do not claim the hurtbox refinement proves
that specific map is fixed. Completion diagnostics now separate static collision
from footprint occupancy and record the first blocking collider's name/type/layer
once per search. No retries, global collision filtering, relaxed wall checks or
removal of existing map props were introduced to force a quota.

Regressions exercise the real production room adapter, including non-monster
entrances, non-lobby rest rooms, PvP enabled both before and after entry, repeated
off/on, boss exclusion, world-offset bounds, reserved teleport positions, nearby
hurtboxes versus actual obstacles, footprint occupancy and cleanup. New tests
failed before their respective fixes. Release build, cover/runtime fixtures and
installed-game contracts run without deployment. Host/stock-guest gameplay remains
unverified; the next live result should be checked using actual placed/requested
counts and the new collider diagnostics.

Native evidence: REA selected-member parent
`ev_8308ea305b068c332cec098fa2b0c60a5abff19f2c187de959162dcf751d544b`
(CombatManager, TileBasedRoomInstance, SingleRoomFloorGenerator), same installed
assembly SHA-256/MVID as the earlier investigation. Native SingleRoom generation
sets `floorInstance.size` to its tile dimensions and uses the floor transform as
origin. Built-in collider/transform trees for the reported Jail/Anvil prefabs and
67 referenced Jail props were inspected offline. `TopdownRigidbody` metadata
was additionally inspected in
`ev_471c77338f16ca6c4cb930c5dace46a6fb21ad0ee707383eda0b0fcf9c82f7cf`;
installed-game tests anchor the grounded body layer and movement masks. Root
generator typetree decoding still fails, so no full generator-asset decode or live collision execution is
claimed. Raw assets, private logs and temporary inventories remain outside Git.

## Varied props and placement responsiveness — 0.101.0

The user confirmed that 0.100.2 cover appears in the lobby and a second run, but
reported slow staged placement and overly sensitive clearance. They had initially
checked only the current dungeon room; distribution is floor-wide. The latest
logs confirm some placement rather than a solo/run-initialization failure:
EntranceC6 placed 2–4/20, MultiZone 3–6/20 and Rabbittown 12/20, exhausting 1,024
attempts. Wall/collider rejections dominated. These are observed counts for those
searches, not expected quotas on those maps.

The old 5×5 square was unnecessarily large for the audited compact props, and
two candidates per frame spread rejection-heavy searches over many frames. The
new 3×3 policy retains the four-unit portal/spawn/interaction/player exclusions,
hazard checks and boss exclusion. Small rooms can be considered from 6×6 instead
of 8×8, but still need verified ground and a full bypass. Count remains a maximum.

All 79 installed `PlayerAvatarCostume` assets have movement collider radius 0.25
and body scale 1. `PlayerAvatar` copies that radius onto its root movement circle;
the native movement box uses its bounds minus 0.03. Even the widest allowed prop
leaves 0.825 units of clear side bypass in a 3×3 square, compared with native player
diameter 0.5. This does not guarantee passage for another addon that enlarges
player collision. A regression also covers fractional tilemap origins: sampling
just the center and eight neighbors can miss the outer ground edge, so validation
now covers all intersected cells.

The five catalog roots appear exactly once in both inspected stock network-manager
spawn lists, with scene ID zero and serverOnly false. They use plain reward-free
`BreakableProp`, unchanged server-to-client position replication and single-frame
idle animations without events. Boxes/book props from the earlier audit lacked
verified movement colliders, while stones/Grassland tables carried their own
`UpdateAstarProp`; they remain excluded. Root-only cleanup and the force-break
guard stay essential to avoid their detached fragments.

Native evidence: REA parent
`ev_f9d2eaf89565cb8a705b892556aef737ac02b358469d7e36e27e3ac53adfdf80`
and matching installed assembly SHA-256 from the investigation. Built-in Unity
asset trees establish the catalog dimensions, registration, scripts and costume
radii. Temporary asset inventories and private logs remain outside Git.

Regression tests observed failures before fixes for rejection-heavy frame delay,
small safe areas, small open rooms, one-prop repetition and fractional grid
clearance. Cover/runtime suites and installed-game compatibility checks validate
the resulting code without deploying. Steady-state fixture ticks still allocate
zero managed bytes; this is not a Unity performance benchmark. Live confirmation
is still needed for the new mix, passage around tables, stock guest visibility
and clean removal after stopping PvP or changing levels.
