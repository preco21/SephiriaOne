# PvP Burn duration implementation plan

**Goal:** Cap balanced player-vs-player Burn at two seconds per application/refresh, independent of duration bonuses, while preserving native PvE.

**Architecture:** Reuse the shared debuff origin receipt and pre-update boundary. Record spawn time and native refreshes, end eligible Burn through native `Destroy` before an overdue tick, and suppress further updates during native delayed despawn. Preserve native timer configuration, stacks and replication. No new setting, dictionary, loop or restore journal.

**Scope:** Normal and blue `CharacterDebuff_Burn`, current permitted player pairs including companion owners, all shared combat modes. Plasma Burn and other debuffs remain separate. Existing Burn uses the latest application/refresh age when balancing is enabled. Disabling balancing stops applying the cap to live effects; effects already retired by the cap/guard cannot revive. Per the user's confirmed preference, new applications may refresh an active Burn to two seconds; non-renewing stacks do not extend it.

**Workflow:** Execute inline with TDD and a final read-only review. Every dotnet command uses `-p:DeployMod=false`; no deployment. Commit and push the verified change using Conventional Commits.

- [x] Add executable regressions for extension/refresh, exact expiry, delayed despawn, independent applications, live toggles, PvE/ownership exclusions and stale connections.
- [x] Add the timestamp/ended flag to existing `DebuffOrigin`; implement the Burn-specific cap in a small combat module called only from the valid origin path. Track successful native refreshes, including those made while balancing is off.
- [x] Add installed native contracts for update-before-expiry ordering, native removal/despawn, refresh and Burn identity; validate integration and cleanup.
- [x] Run Release build, combat/runtime and installed-contract suites, request review, update docs/version.

Validation passed: zero-warning Release build; 3,936 combat checks; 2,254 runtime
and 69 Bat checks; installed-game contracts and 33-reference Harmony 2.9 API
audit. Independent review found no actionable issues. Final integration uses the
authorized Conventional Commit and push workflow; no deployment or game launch.

## Native evidence

`CharacterDebuff.InitializeAndSpawn` computes `buffTimer.time` from duration plus bonuses. Burn includes `DebuffDuration` and `BURNDURATION`. Renewing `AddStack` calls `RefreshDurationTimer` before its maximum-stack check; Burn inherits the base implementation that resets elapsed time to zero. `CharacterDebuff.Update` calls `OnUpdate_Server` before testing expiry and still calls it during the 0.5-second delayed despawn after `Destroy`. Enforce the latest two-second deadline before that damage callback, without altering unrelated effects or native timer values.
