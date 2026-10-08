# Random encounter probability multiplier

## Verified core design

Add a default-native host option using `/one events chance xN`, status/reset and
explicit `/one save`. Scale the two native optional-room cumulative thresholds
in `StageEntity_Choice.GenerateStage` and `StageEntity_GrasslandTown.GenerateStage`:
0.003 for two rooms, 0.043 for at least one. Clamp each threshold to 100%; x0
rejects even an exact-zero draw; x1/reset restores bit-for-bit native thresholds.
Keep the native RNG draw, seed, room pool, two-room limit and all other stage
instructions. Generated floor data and native room-count synchronization remain
the source of truth for stock clients, saved runs and reconnects.

This covers optional event rooms containing blood donation, obelisks, magic
fountains and the rest of the chapter's native random room pool. Scheduled
travelers and merchant floors use separate mechanisms. Their inclusion and the
identity of the requested "liver shop" are being clarified before modifying
those paths. The common probability/settings infrastructure is independent of
that scope decision.

Changes affect future stage generation, not already generated or saved floors.
No per-frame work, reroll/reconnect replay, custom network protocol or game
asset changes. Compatibility validation must reject changed native comparisons,
thresholds, RNG adjacency or room-count writes, with native fallback on failure.

## Work

- [x] Add failing pure policy/parser and executable Harmony boundary fixtures.
- [x] Implement settings and narrowly validated threshold hooks.
- [x] Wire host service, snapshots, v16 presets, help and Spawns panel selector.
- [x] Translate new UI/messages into EN/KO and document actual scope/timing.
- [x] Verify native IL, unchanged instruction/RNG flow, host/client behavior,
  zero/capping, repeated calls, resets, new scopes, saved settings and re-entry.
- [x] Independent review, Debug/Release builds and focused regression suites.
- Release procedure: conventional commit and push main after verification; never deploy to AddOns.

All dotnet commands use `-p:DeployMod=false`; Harmony fixtures use Release.

## Verification results

- Event hooks: 4,812 executable Release fixture checks.
- Shared runtime: 976 command/session integration checks; 69 Bat checks.
- Event parser/preset: 38 checks; bundled catalogs: 1,358 checks.
- Existing Jar hooks: 139 checks; localization engine: 48 checks.
- Debug and Release builds: zero warnings/errors, DeployMod=false.
- Installed-game IL contract suite: passed, including both real event generators,
  exact preservation of native instructions, FloorData serialization, floor
  generator initial/incremental count SyncVar and native room-pool consumption.
- Independent review found a compatibility-guard gap for branch destinations and
  zero initialization. Regression tests reproduced it before the fix; both the
  executable fixtures and independent re-review confirm rejection afterward.
- No live multiplayer/rendered UI test or AddOns deployment was performed.

Scope clarification has not been answered. The implemented default is the
recommended existing-random-probabilities-only option, limited to verified
optional event rooms. Scheduled Collin/travelers/merchants remain native, and
“liver shop” remains unidentified. See `docs/random-events.md` for user-facing
scope and timing. Additional encounters require an explicit scope decision or
identification; they are not silently claimed as supported.
