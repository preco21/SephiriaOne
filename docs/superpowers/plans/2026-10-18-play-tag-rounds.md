# Play Tag round count implementation plan

**Goal:** Configure 1–100 rounds (default 1) through the Play Tag panel and
`/one tag rounds N|reset`; save the setting with `/one save`.

**Approved behavior:** Each explosion ends one round. Keep the same captured
participants and accumulated K/D/A. Use existing automatic recovery before a
new three-second countdown and random carrier; reset the fuse and minimum-fuse
holding allowances each round. End after the configured number of explosions.

**Architecture:** Extend session policy/preset/UI patterns. Capture round count
at match start, like initial fuse duration. Reuse `Phase.BetweenRounds`, pending
respawns and current connection receipts; invalidate combat at each round boundary
without resetting scores. Mid-match setting changes affect the next match.

- [x] Add command/preset and multi-round runtime regressions; observe failure.
- [x] Add validated setting, v30 preset compatibility, command and compact UI row.
- [x] Add round progression and countdown/status localization in EN/KO. Preserve
      default single-round behavior, normal modes and native guest transport.
- [x] Cover loading/readiness, revival failure, selection/master changes,
      reconnects, new run/session and stale callbacks across round boundaries.
- [x] Run non-deploying build, runtime, pure bomb, tournament, combat and native
      contract suites as applicable; obtain independent review.
- [x] Update feature/index/history documentation and version.

Verification: Release build succeeded with no warnings or errors; 2,372 runtime,
20,680 pure bomb, 41,176 tournament and 4,858 friendly-fire checks passed. Portable
policy/preset/localization and installed-game contracts passed, including all
33 compiled Harmony references. Independent review found no actionable issues.
Live Unity/multiplayer behavior still requires an in-game check. Commit and push
follow the repository workflow after verification.

No deployment. All dotnet commands use `-p:DeployMod=false`. Round, carrier,
countdown, scores and minimum-fuse counters remain temporary, never preset data.
