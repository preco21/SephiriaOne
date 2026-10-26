# Teleport the party to the host

Updated in **0.98.0**, 2026-10-27. Original action: 0.87.0; UI button: 0.89.4.

`/one teleportall` and **PvP Combat → Teleport all players**
(**PvP 전투 → 모든 플레이어 소환**) bring ready, living players to the host:

- On the same floor GUID, request native position teleport immediately.
- On another floor GUID, use the native floor-travel transaction with the host's
  captured floor, spawn-point ID and position. This recovers a guest remaining in
  a boss room after the host reaches the next stage. The native transaction owns
  preparation/loading and arrival positioning; no premature coordinate teleport
  or additional addon follow-up is sent.

The host is not moved. Eligibility is independent of PvP compatibility, selection,
or active match mode. English/Korean responses distinguish requested travel from
skips and failures. `/one teleportall help` describes the behavior; no preset or
new UI setting is required. The existing button uses the same command dispatcher.

## Eligibility and lifecycle

Require the actual local host connection, a ready/living host, finite destination,
an open current run, and no host floor transition. Never choose another roster
entry as a substitute host. Skip dead players (use `/one reviveall` separately),
unready/loading players, stale/disconnected connections and already queued native
travelers. Reject a pending `MovingFloorViaWorldmap` countdown on any party member
before sending requests, including between callbacks. It precedes loading/queue
state and later calls `MoveTogether`, which enqueues everyone. This does **not**
repair or cancel a broken in-progress native travel transaction. The new path addresses ready players stranded on a different floor.

Each target captures source floor, avatar/net ID, spawner, connection and identity.
Before each request, recheck membership, ownership, readiness and unchanged source
floor. Recheck the host floor, local connection, dungeon ID, run object and resource
generation too. A callback that replaces the run or moves the host aborts remaining
requests. Per-target exceptions do not prevent other eligible requests. Reentrant
commands are rejected, and duplicate roster entries are deduplicated.

For cross-floor requests, the destination must still exist in `generatedFloors`
and in a generated, server-owned, network-spawned `FloorGenerator`, whose
`DataOnServer` is the current dictionary entry. No new stage or progression is
created. A missing/incomplete destination fails only the cross-floor requests;
normal same-floor position teleport remains usable.

`NativeFloorTravel` reads the existing private `travelingTransactions` queue and
its `avatar` field, without mutation. It skips pending target or host travel even
if a loading indicator has been cleared. This is necessary because native
`LocalMoveFloor` logs an existing transaction but still adds another one. A
shape mismatch disables cross-floor recovery, logs once and returns localized
failure feedback; same-floor requests do not depend on this optional adapter.

All commands remain one-shot requests. Native travel is asynchronous, and remote
arrival is not independently acknowledged by the addon. The destination is the
host position captured when the command was issued, not a continuously followed
position. Once accepted, the native game owns travel completion and normal
player/object lifecycle; no addon coroutine, timeout, periodic scan, retry or
retained player list is added. Queue checks run only on explicit commands.

## Native transport and state

`CombatBehaviour.ReqSetPosition(Vector3, bool)` retains the existing same-floor
path: remote owners receive `TargetSetPosition`; its stock receiver calls
`SetPosition`, and normal owner-authoritative transform replication updates peers.
Native `TeleportTogether.UserCode_CmdTeleport__Vector3` uses this same API.
`teleport: true` preserves map-closing callbacks and follower relocation.

For other floors, call the **server** overload:

```text
DungeonManager.MoveFloor(player, hostFloor, hostSpawnPoint, loadingScreen: 0,
    recordHistory: true, allowSave: false, keepPrevFloor: false,
    randomPosition: false, leaveTrainingSchool: false,
    overridePosition: capturedHostPosition)
```

The server dispatch retains `overridePosition`; the client `CmdMoveFloor` overload
omits it. Stock `PlayerSpawner.Initialize` also uses the position override and
`allowSave: false`. Recovery does not ask a guest to issue a command or rewrite
host save/progression state. The spawn ID is the host's current one (or
`FLOORSTARTING` if empty), for native history/save consistency; the position
override means native spawn-point lookup is not used for this arrival.

`LocalMoveFloor` enqueues the transaction, lowers targetability, starts temporary
invulnerability, sets loading state and allocates the existing destination.
Loading type 0 uses its normal one-second travel wait, without cinematic delay.
`DungeonManager.Update` waits for `GenerateSuccess`, calls `ServerPrepareTravel`
and lets the native timer complete. It then applies the override with
`ReqSetPosition(..., teleport: true)`, updates safe mode, loading/targetability,
invulnerability, `eachPlayersPosition`, `NetworkcurrentFloorGuid`,
`NetworkcurrentSpawnPoint`, history and per-location counters. It releases floors
only through the native occupancy/pending-transaction rules. Native training-area
follower cleanup remains native; this command does not edit follower entities.

The floor and loading fields have initial/incremental SyncVar serialization.
`HookCurrentFloorValue` dispatches server/client entered-floor events, preserving
existing mod reconciliation and native client presentation. The stock guest owns
its normal loading/position behavior. No new RPC, client script or addon asset is
required by the inspected path.

## Evidence and verification

Installed Assembly-CSharp SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA managed evidence:
`ev_dfa5752e0448d915e876904e3d3f0db3814685e875e620e3b41333cab51b0a68`.
Matching local decompilation was used for full control flow. REA metadata/CIL
inspection is static, not observed gameplay; its unrelated #US terminal-flag
warnings do not establish runtime rejection.

Regression fixtures demonstrated the original action skipped a stranded player,
then cover mixed same/cross-floor calls, delayed native arrival, no premature
position writes, exact captured destination/spawn, repeated clicks, hidden pending
travel, invalid/unready/dead targets, destination drift, callback failures,
reentrancy and run/host/connection changes. Installed-game contracts verify the
server overload, adapter binding, native preparation/generation/occupancy cleanup,
position transport and floor/spawn/loading serialization.

Release build (zero warnings/errors), 14,611 runtime checks plus 69 Bat checks,
full installed compatibility checks, 4,869 catalog checks and panel/localization
checks passed with deployment disabled. Live host plus unmodified guest testing is still needed: boss-room
recovery into the next stage, slow guest loading, multiple stranded guests,
followers, and repeat use after arrival. Successful submission is not proof of
client rendering or final arrival. Nothing was deployed.
