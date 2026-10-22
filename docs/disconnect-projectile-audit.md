# Disconnect and projectile lifetime audit

2026-10-23, addon changes `0.90.1`. Start with this note for the October incident;
the [September investigation](disconnect-investigation.md) is separate evidence.

## Observed incident, not yet a proven root cause

The available host `Player.log` records a timeout at **2026-10-23 01:00:53.896 KST**
(`2026-10-22T16:00:53.8968199Z`), connection 3, player netId 144:

- Steam state `ProblemDetectedLocally`, end reason `4001`, detail
  `Timeout; remote problem. Rx age server 10.5s relay 0.0s`.
- **2,089** failed native sends (`k_EResultLimitExceeded`) precede the timeout.
  Each produces both `Send issue` and `Could not send`; these are paired messages,
  not 4,178 separate failures. The old log does not identify recipient, channel,
  attempted buffer size or per-second rate. Do not assume all went to connection 3.
- No addon `server-requested close`, invalid-message/deserialize error,
  `NoConnection`/`InvalidParam` send result, host OOM or host exception immediately
  preceding this timeout was found. Existing diagnostics report no reconciliation
  fault and eight tracked players. This cannot rule out guest memory pressure,
  client exceptions, a blocked game loop, or physical/network problems.
- Host managed heap at removal was 391,536,640 bytes, about 373 MiB. This is one
  managed-heap sample, not a memory trend, process/GPU total or guest measurement.
- Other closures use `ClosedByPeer`, end reason `1000`, `Graceful disconnect`.
  This distinguishes their transport report from the timeout; it does not prove
  a user deliberately left or explain why the peer closed.

The strongest observed symptom is transport send pressure/failure followed by a
timeout. The native wrapper does **not** directly disconnect on `LimitExceeded`:
it logs and returns without retrying. Missing reliable application messages can
therefore leave a guest behind, but this log cannot determine whether congestion,
an unresponsive recipient, client state or something else initiated the episode.
No guest log or simultaneous client trace was available.

Version caveat: startup metadata says `0.85.0`, whereas the installed DLL inspected
on disk has assembly version `0.89.1.0`. That inspection does not prove which
assembly was loaded earlier in the process. Current source was `0.90.0` when the
audit began. Do not infer execution of newer features from stale metadata. This
patch aligns metadata with `0.90.1` and logs both assembly and metadata versions.
RaidRaid was also present; co-installation alone is not evidence of causality.

Private logs, settings, game binaries and decompiled source remain outside Git.

## Follow-up: reported v0.89.0 final-boss entry

The user subsequently identified the running version as **0.89.0** and the
incident as entering the endgame/final-boss room while running, without attacking
or an apparent error. Treat this as the user's runtime report; the later on-disk
version inspection above does not override it. An exact wall-clock time and the
affected guest's log are still unavailable, so the timeout is a matching candidate
incident rather than a recipient-confirmed identification.

Focused checks against `33930f2` (project version `0.89.0`) found:

- The projectile runtime/hooks, shared scope/run-generation handling, and
  starting-resource/budget departure hooks are unchanged through the pre-audit
  `0.90.0` source. Later projectile changes affected default limits/commands,
  not room-entry destruction or ownership. Independent Planet cadence and
  lower-bound damage assistance were added after the reported version and
  should not be blamed for a `0.89.0` incident.
- Of the 2,089 failed sends, **419 precede the first `QTemple_Boss3(Clone)`
  initialization log and 1,670 follow it**. Failures therefore were already being
  reported before that boss initialization was logged. The lines lack recipient
  IDs and event timestamps; they cannot establish the duration/rate of pressure,
  or prove every failure affected the disconnected player.
- The same log records both final-boss entities being initialized, native party
  movement, and RaidRaid detecting the final boss/preparing additional patterns.
  This is relevant reproduction context, not evidence that RaidRaid emitted
  excessive traffic or caused the disconnect.
- `ProjectileLimitRuntime.Active/Refresh` keys reseeding to the DungeonManager
  instance and shared run generation, **not each player's room/floor GUID**.
  `SessionSettings.OnStartSession` advances that generation; ordinary room entry
  does not invoke a custom projectile sweep or reset. Existing release callbacks
  observe native teardown without initiating it.
- Native `DungeonManager.LocalMoveFloor` queues travel, applies loading state,
  and calls `FloorAlloc`. `Update` waits for generation/travel readiness, moves
  the avatar, updates floor SyncVars and invokes `ReleaseUnusedFloors`. The native
  release path uses `NetworkServer.Destroy` for floor-related objects and floors.
  No limiter patch replaces those travel/allocation/release methods. The native
  send-to-own-session path is separately gated by `requestLeaveOnHost` and leave
  preferences; no addon write/call to that path was found in this audit.
- Starting fruit/leaves departure work is gated to the first run departure;
  ordinary endgame entry does not replay those grants. Merchant placement rejects
  boss floors. Boss-owned projectiles are outside the per-player limiter scope.

Running without attacking narrows the player's direct-action triggers, but does
not imply an idle network: automatic artifacts, other participants, native boss
objects, movement and floor synchronization can still send messages. A preexisting
send backlog followed by transition load or a stalled receiver is consistent with
the evidence, but remains a hypothesis. Nothing here establishes invalid IDs,
missing host authority, or a new room-entry bug. The `0.90.1` diagnostics remain
the next way to identify which recipient failed before/during a transition; no
additional speculative gameplay/transport changes were made for this follow-up.

Native DungeonManager methods were inspected read-only against the same assembly
fingerprint, REA parent
`ev_0fce45c558348e40928baa5112018d285a177ce8992e2af1c1e4bee4a7e9e4d4`,
with focused local ILSpy decompilation. Source comparison and log counts were
rechecked; this follow-up changes documentation only, so no build or gameplay
test was repeated and nothing was deployed.

## Projectile limiter findings

See [coverage and native contracts](projectile-limits.md). The active saved
configuration had the limiter enabled, default 64/player and saved 12/source.
The two separately bounded pools are damaging bullets and Liberation schedulers;
64 is not a global cap on every network object or all visual effects.

| Concern | Audited behavior / limit of evidence |
| --- | --- |
| Suppression/destruction | Covered emission wrappers reject before native Instantiate/OnSpawn/NetworkServer.Spawn. Existing projectiles are never destroyed or unspawned to enforce a cap. |
| IDs / guest definitions | A denied spawn never receives a network ID. Native accepted objects retain their spawn/despawn serialization and stock prefab. The bookkeeping component is a MonoBehaviour, not a NetworkBehaviour; no component protocol index is added. |
| Authority | The limiter performs no ownership reassignment or authority transition. Recorded source provenance survives native owner transfer; companion sources charge their player's receipt. |
| Cap reentrancy | A lease is reserved before native callbacks, and bound before OnSpawn reentry. Failed unbound spawns roll it back; a still-live failed initialization retains its lease until real destruction. |
| Expiration / descendants | OnDespawn and actual OnDestroy release once. DestroySelf's delayed tail keeps capacity until teardown. Child provenance is captured before parent cleanup, and each admitted child reserves independently. |
| Disconnect/rejoin | Connection reference plus generation prevents old bullets/schedulers from spending a replacement connection's budget, including literal connection-object reuse. No Steam-account-only ownership key. |
| Equipment / run transitions | Equipment changes do not erase a live projectile's charge. Dungeon/run generation reseeds existing native live objects once; stale releases cannot debit the replacement ledger. |
| Settings changes | Lowering limits preserves existing valid instances; off/on adopts live instances, including over-cap ones. Ordinary refresh does not repeatedly reset counts. |
| Traffic / performance | Cached admission and ledger reads; warmed denied-spawn path allocates zero bytes in 10,000 calls. No per-frame scene scan. However, a concurrency cap is **not** a spawn-rate or bandwidth cap: rapid expiry/refill, excluded direct attacks, RPCs and client-only FX can still create substantial traffic. |

No confirmed limiter-induced network deletion, ownership corruption or ID/count
desynchronization was found. Static/fixture checks cannot prove a stock guest
will never stall or establish actual endgame bandwidth. No cap or emission-rate
behavior was changed speculatively.

## Other recent paths

Recent lower-bound assistance, Planet cadence histories, shared burst budgeting,
party teleport and lobby dice paths were reviewed. No explicit kick/disconnect
call or new client-required message schema was found. Combat math has finite-input
guards; pair/history state is bounded and connection-scoped. Teleport and dice use
existing native owner RPCs/SyncVars rather than custom guest state.

Automatic-shield MP settlement can invoke native `OnMpUsedServerside` artifact
callbacks on hits that previously spent no MP. Its once-per-hit guard, current
admission checks, available-MP clamp and zero-spend skip prevent the obvious
duplicate settlement cases. Downstream artifact traffic still depends on actual
equipment; no observed exception or proven recursive/spawn loop linked it to
this timeout. This remains a useful stress-test combination, not an established bug.

## Confirmed separate stale-effect defect and repair

There are **22** `CharacterBuff_StatusInstance_FrostTouch.OnUpdate_Server`
`NullReferenceException` reports after a *later* departure, connection 9, around
01:03 KST. They occur at native IL `0x6b`, `NetworkTarget.transform`, after the
timeout above. Their timing does not support them as the initiating timeout cause.

An optional, independently installed lifetime guard replaces the derived
FrostTouch `isServer` FX gate with a check that also requires a live buff, a live
target and `!IsEndBuff`. It runs **after the unchanged base HUD update**, so a
target removed by a base callback cannot reach the subsequent FX transform access.
Native valid-target timing/RPCs, caller expiry and delayed despawn are preserved.
There is no extra despawn, network message or ownership change.

The rewrite validates the exact native base/gate/target/transform/RPC shape;
incompatibility disables this optional guard, not the other features. Exceptions
from unrelated native base work are not swallowed. A literal CLR-null target
already failing in the earlier base HUD method is outside this narrow repair.
The demonstrated destroyed-Unity-target FX exception is covered by a real Harmony
regression fixture and installed-method IL contract.

## New read-only diagnostics

`TransportSendDiagnostics` observes `NextCommon.SendSocket` results. Successful
sends return immediately without reading clocks, maps, snapshots or allocating
diagnostic state. A failed send records, per actual connection object:

- result and recipient connection ID;
- cumulative failed-send count and attempted send-buffer bytes;
- reliable/unreliable failure counts, latest buffer size, failure span;
- global native spawned count and cached per-recipient bullet/emitter counts.

The first failure is logged immediately; repeated failures are summarized at most
once per five seconds per connection. These counters describe attempted native
buffers **including the appended channel byte**, not delivered traffic or wire
protocol overhead. Native logging is unchanged. No message payload or Steam ID
is logged by these diagnostics. Snapshot failure leaves the core failure report
intact; logging failure cannot change the native result.

Capture live limiter counts in transport close prefixes: Mirror runs connection
Cleanup/RemoveConnection **before** the game-manager OnServerDisconnect callback,
and the limiter retires counts at RemoveConnection. The later player-removal
hook logs accumulated failure totals and forgets the connection receipt. Cached
snapshot reads never reconcile, reseed, spawn, retry or mutate gameplay state.
Fresh connection objects and removal/unload reset diagnostic receipts.

Native evidence: verified installed Assembly-CSharp SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
REA retained analyses:
`ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`
(identity),
`ev_8b1d84ab146a6b412cd527b6f5e44386f5643bd002c94809b9da9b34bbb8abe3`
(projectile/FrostTouch methods),
`ev_18b401043630eb0063d09edc342e2c100ccf18da51491934a81074e86b4a0ba3`
(Steam transport). Local ILSpy inspection confirmed send and cleanup ordering.

## Verification and next evidence

Release build passed with zero warnings/errors using `-p:DeployMod=false`.
Passed: 126 disconnect checks, 78 projectile runtime checks, 99 ledger checks,
298 projectile native-hook checks, 17 real Harmony FrostTouch checks, 45 Entry
lifecycle checks, the installed FrostTouch IL contract, and the shared
policy/installed compatibility suite. The original orphan-FX and missing-send-
diagnostic fixtures failed before their respective implementations. These are
executable fixtures and static native contracts, not live multiplayer.

During the independent recent-combat review, the existing friendly-fire suite
once failed its warm allocation assertion in `BalanceTests.cs`; the isolated
no-build rerun passed all 15,593 checks. No combat policy source was changed for
that measurement, and it is not evidence of a client memory leak or this timeout.

For another incident, retain both host and affected guest `Player.log` before
relaunch, record wall-clock time, frame freezes/game closure/error behavior,
and compare `send-failure`, `send-summary`, `transport-close` and `pre-close`
entries. A bounded Pallas/Liberation endgame reproduction with identical builds
and limiter on/off can separate packet pressure from admission behavior; avoid
changing multiple PvP settings simultaneously. No automatic retries or transport
buffer-size changes were introduced without evidence.
