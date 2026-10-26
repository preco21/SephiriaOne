# Cross-floor party recovery implementation plan

> For agentic workers: use `superpowers:executing-plans` for inline execution.

**Goal:** `/one teleportall` and its existing UI button recover living, ready
players stranded on another floor, using stock-client native travel.

**Architecture:** Preserve `ReqSetPosition` for same-floor players. For different
floor GUIDs, request `DungeonManager.MoveFloor` with the captured host floor,
spawn-point identity and `overridePosition`. Native travel owns generation wait,
loading/invulnerability, position request, floor/occupancy replication, history,
followers and release of unoccupied floors. No addon queue or update loop.
Use a small native adapter to check the existing private travel queue read-only:
`LocalMoveFloor` merely logs duplicates and still enqueues them. Never cancel or
rewrite an in-progress native transaction. A failed adapter leaves same-floor
movement usable, with explicit cross-floor failure feedback.

**Evidence:** Installed Assembly-CSharp SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`, REA
`ev_dfa5752e0448d915e876904e3d3f0db3814685e875e620e3b41333cab51b0a68`.
Matching local decompilation shows the server overload retains overridePosition;
the client command overload drops it. Use the server API only. Native Update waits
for destination GenerateSuccess and the travel timer, then requests movement and
synchronizes floor state. Stock PlayerSpawner initialization already uses this
overload and disables save side effects.

- [x] Extend runtime fixtures with delayed native travel; demonstrate the old
  same-floor filter fails to recover a ready stranded guest.
- [x] Add native travel adapter and extend TeleportAllAction. Capture source
  floors, host destination and connection identities; revalidate each request.
  Reject loading/pending travelers and unavailable destinations. Pass loading=0,
  recordHistory=true, allowSave=false, keepPrevFloor=false, randomPosition=false;
  leave training cleanup to native travel. Do not revive, alter stage progression,
  or emit a second position request before/after native completion.
- [x] Verify mixed same/cross-floor players, host transitions, repeated clicks,
  pending transactions even with negative loading flag, reentrant callbacks,
  disconnect/rejoin, invalid/dead/unready targets and per-target failures.
- [x] Verify installed native overload, queue fields, completion and replicated
  floor/position contracts. Update EN/KO help/feedback and shared docs/index.
- [x] Run non-deploying build, runtime, catalog and installed-contract checks;
  complete review. Conventional commit/push follows the repository workflow;
  do not run Deploy-Mod.

**Limits:** Native travel is asynchronous; report requests, not unobserved client
arrival. Already-loading players are skipped; this does not repair an existing
broken native transaction. Native lifecycle owns accepted requests if the host
moves again afterward. Actual stock-guest loading/visual completion needs a live
multiplayer check; static/fixture evidence does not establish it.

## Execution record

- Confirmed original cross-floor regression before implementation; preserved
  existing same-floor checks and native position transport.
- Ruling: reject any party member's `MovingFloorViaWorldmap` countdown, not only
  queued transactions. Review identified native countdown → MoveTogether as a
  second admission phase. The added regression failed before the guard and passed
  afterward; callbacks recheck this state before subsequent sends.
- Final Release build: zero warnings/errors. Runtime: 14,611 checks plus 69 Bat
  lifecycle checks. Full installed-game/caller/replication/Harmony contracts and
  4,869 bundled catalog checks pass. Panel checks and 50 localization checks pass.
- Independent review found no remaining actionable issues after the countdown
  fix. Actual unmodified-client arrival remains a live verification step.
