# Teleport the party to the host

Added in 0.87.0, 2026-10-22.

Since **0.89.4** (2026-10-23), **Combat → Teleport all players**
(**전투 → 모든 플레이어 소환**) provides the same one-click action below the
potion controls. The button dispatches `/one teleportall` through the existing
panel session guard and shared command service. The standard feedback area reports
requests, skips and failures. No additional confirmation or queued work is added.
Availability requires an active host session, independently of the friendly-fire
toggle, compatibility and participant list; the command revalidates the host,
destination and each player when clicked. Existing page/locale cleanup owns the
button and its listener. No new roster polling or snapshot data is needed.

`/one teleportall` immediately sends native teleport requests to ready, living
players on the host's current floor. `/one teleportall help` describes eligibility.
The host is already at the destination and is not moved. All session players are
considered, independently of PvP selection or match mode. English and Korean
command responses report requests, skips and failures.

The command requires the host's actual local server connection, a ready/living
host avatar, an open run and no host floor transition. It never falls back to the
first roster entry. Dead players, connecting/loading players and players on other
floors are skipped. Use `/one reviveall` separately for dead players. This action
does not change floor ownership, save progression or force native travel; merely
moving an avatar to another floor's coordinates would leave its floor state wrong.

## Native transport

Installed `CombatBehaviour.ReqSetPosition(Vector3, bool)` chooses the current
transform authority. On the server, a remote-owned player is sent the existing
`TargetSetPosition` TargetRpc. Its stock receiver calls `SetPosition`; normal
owner-authoritative transform replication then updates other peers. Native
`TeleportTogether.UserCode_CmdTeleport__Vector3` calls the same API for its party.
The command uses `teleport: true`, retaining native map-closing callbacks and
`UnitAvatar.SetPosition` follower relocation. No custom RPC or guest addon is
required by this path, and no direct transform write is introduced.

Remote movement is asynchronous. Success means the native request was sent, not
that every remote position has been independently acknowledged. There is no
repeated movement worker or retry that could snap a player back after they move.

Each target captures the avatar and connection. Current readiness, membership,
connection identity, floor and run generation are rechecked before every send.
Callbacks cannot recursively invoke the action, redirect a captured target to a
reconnected player, or carry the operation into another run. One failed request
is reported while remaining eligible targets are processed.

## Evidence and checks

Static inspection and matching local decompilation of installed Assembly-CSharp
SHA-256 `c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`,
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA identity evidence: `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`.
`DungeonManager.LocalMoveFloor` uses nonnegative `loadingScreenType` while moving;
completion restores -1. Cross-floor movement is a separate native transaction,
including floor allocation, invulnerability, history and training-area cleanup.

Runtime fixtures exercise command parsing, host authorization, exact destination,
dead/loading/other-floor exclusions, finite coordinates, run replacement,
reentrancy, individual failures and stale/duplicate connections. Installed-assembly
checks verify the stock party API and TargetRpc receiver. Live guest movement and
follower behavior remain to be verified in-game. Builds are non-deploying.
